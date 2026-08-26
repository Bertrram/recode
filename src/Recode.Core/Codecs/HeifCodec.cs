using Recode.Core.Abstractions;
using Recode.Core.Formats;
using Recode.Core.Native;

namespace Recode.Core.Codecs;

/// <summary>
/// HEIC, HEIF and AVIF, through libheif.
/// </summary>
/// <remarks>
/// HEVC encoding goes through kvazaar, which is BSD licensed. x265 is not built
/// into the bundled library, deliberately: it is GPL, and linking it would pull
/// the whole project along with it. AV1 goes through libaom in both directions.
///
/// libheif applies container level rotation and mirroring itself during
/// decoding, so nothing extra is needed here for orientation.
/// </remarks>
public sealed unsafe class HeifCodec : IImageCodec
{
    public const string Id = "libheif";

    private readonly INativeLibraryLoader _loader;
    private readonly object _gate = new();

    private HeifNative? _native;
    private string? _loadError;
    private bool _attempted;

    public HeifCodec(INativeLibraryLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public string BackendId => Id;

    public bool CanDecode(ImageFormat format) => format.BackendId == Id && format.CanRead;

    public bool CanEncode(ImageFormat format) => format.BackendId == Id && format.CanWrite;

    public BackendAvailability CheckAvailability()
    {
        if (TryGetNative(out _, out var error))
        {
            return BackendAvailability.Ok;
        }

        return BackendAvailability.Missing(HeifNative.LibraryName, _loader.SearchDirectory, error ?? "unknown reason");
    }

    public string? TryGetVersion() => TryGetNative(out var native, out _) ? native!.GetVersion() : null;

    public RgbaImage Decode(string path, ImageFormat format)
    {
        var native = RequireNative();
        var data = File.ReadAllBytes(path);

        if (data.Length == 0)
        {
            throw new CodecException($"{Path.GetFileName(path)} is empty.");
        }

        var context = native.ContextAlloc();
        if (context == 0)
        {
            throw new CodecException("libheif could not allocate a context.");
        }

        nint handle = 0;
        nint image = 0;

        try
        {
            // The buffer stays pinned for as long as the context refers to it,
            // because read_from_memory_without_copy does not take a copy.
            fixed (byte* input = data)
            {
                var error = native.ReadFromMemory(context, input, (nuint)data.Length);
                if (!error.IsOk)
                {
                    throw new CodecException($"{Path.GetFileName(path)} could not be read: {error.Describe()}");
                }

                error = native.GetPrimaryImageHandle(context, out handle);
                if (!error.IsOk || handle == 0)
                {
                    throw new CodecException($"{Path.GetFileName(path)} contains no primary image: {error.Describe()}");
                }

                error = native.DecodeImage(handle, out image, HeifNative.ColorspaceRgb, HeifNative.ChromaInterleavedRgba);
                if (!error.IsOk || image == 0)
                {
                    throw new CodecException($"{Path.GetFileName(path)} could not be decoded: {error.Describe()}");
                }

                return CopyOut(native, image, path);
            }
        }
        finally
        {
            if (image != 0) native.ImageRelease(image);
            if (handle != 0) native.ImageHandleRelease(handle);
            native.ContextFree(context);
        }
    }

    private static RgbaImage CopyOut(HeifNative native, nint image, string path)
    {
        var width = native.GetWidth(image, HeifNative.ChannelInterleaved);
        var height = native.GetHeight(image, HeifNative.ChannelInterleaved);

        if (width <= 0 || height <= 0)
        {
            throw new CodecException($"{Path.GetFileName(path)} decoded to a size of {width}x{height}.");
        }

        var plane = native.GetPlaneReadonly(image, HeifNative.ChannelInterleaved, out var stride);
        if (plane is null)
        {
            throw new CodecException($"{Path.GetFileName(path)} decoded without a pixel plane.");
        }

        var result = RgbaImage.Allocate(width, height);
        var rowBytes = width * 4;

        // libheif pads rows to its own alignment, so rows are copied one at a
        // time rather than as one block.
        for (var y = 0; y < height; y++)
        {
            var source = new ReadOnlySpan<byte>(plane + (long)y * stride, rowBytes);
            source.CopyTo(result.Pixels.AsSpan(y * rowBytes, rowBytes));
        }

        return result;
    }

    public void Encode(RgbaImage image, ImageFormat format, string path, EncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);
        var native = RequireNative();

        var compression = format.Compression?.ToLowerInvariant() switch
        {
            "hevc" => HeifNative.CompressionHevc,
            "av1" => HeifNative.CompressionAv1,
            _ => throw new CodecException(
                $"Format '{format.Id}' does not say which compression libheif should use. Check the 'compression' field in formats.json.")
        };

        // An alpha plane costs space, so it is only added when the image
        // actually has transparency to carry.
        var withAlpha = format.SupportsAlpha && image.HasTransparency();
        var chroma = withAlpha ? HeifNative.ChromaInterleavedRgba : HeifNative.ChromaInterleavedRgb;
        var channels = withAlpha ? 4 : 3;

        nint context = 0;
        nint heifImage = 0;
        nint encoder = 0;

        try
        {
            var error = native.ImageCreate(image.Width, image.Height, HeifNative.ColorspaceRgb, chroma, out heifImage);
            if (!error.IsOk || heifImage == 0)
            {
                throw new CodecException($"libheif could not create an image buffer: {error.Describe()}");
            }

            error = native.ImageAddPlane(heifImage, HeifNative.ChannelInterleaved, image.Width, image.Height, 8);
            if (!error.IsOk)
            {
                throw new CodecException($"libheif could not allocate a pixel plane: {error.Describe()}");
            }

            FillPlane(native, heifImage, image, channels);

            context = native.ContextAlloc();
            if (context == 0)
            {
                throw new CodecException("libheif could not allocate a context.");
            }

            error = native.GetEncoder(context, compression, out encoder);
            if (!error.IsOk || encoder == 0)
            {
                throw new CodecException(
                    $"No encoder for {format.DisplayName} is compiled into {HeifNative.LibraryName}: {error.Describe()}");
            }

            error = native.SetLossyQuality(encoder, options.Quality);
            if (!error.IsOk)
            {
                throw new CodecException($"libheif rejected quality {options.Quality}: {error.Describe()}");
            }

            error = native.EncodeImage(context, heifImage, encoder);
            if (!error.IsOk)
            {
                throw new CodecException($"{format.DisplayName} encoding failed: {error.Describe()}");
            }

            using var stream = File.Create(path);
            error = native.Write(context, stream);
            if (!error.IsOk)
            {
                throw new CodecException($"The {format.DisplayName} file could not be written: {error.Describe()}");
            }
        }
        finally
        {
            if (encoder != 0) native.EncoderRelease(encoder);
            if (heifImage != 0) native.ImageRelease(heifImage);
            if (context != 0) native.ContextFree(context);
        }
    }

    private static void FillPlane(HeifNative native, nint heifImage, RgbaImage image, int channels)
    {
        var plane = native.GetPlane(heifImage, HeifNative.ChannelInterleaved, out var stride);
        if (plane is null)
        {
            throw new CodecException("libheif returned no writable pixel plane.");
        }

        var source = image.Pixels;

        for (var y = 0; y < image.Height; y++)
        {
            var row = plane + (long)y * stride;
            var sourceIndex = y * image.Stride;

            if (channels == 4)
            {
                source.AsSpan(sourceIndex, image.Stride).CopyTo(new Span<byte>(row, image.Stride));
                continue;
            }

            // Three channels: drop alpha. It has already been flattened by the
            // conversion step if the format could not carry it.
            for (var x = 0; x < image.Width; x++)
            {
                var s = sourceIndex + x * 4;
                var d = row + x * 3;
                d[0] = source[s];
                d[1] = source[s + 1];
                d[2] = source[s + 2];
            }
        }
    }

    private HeifNative RequireNative()
    {
        if (TryGetNative(out var native, out _))
        {
            return native!;
        }
        throw new BackendUnavailableException(Id, CheckAvailability());
    }

    private bool TryGetNative(out HeifNative? native, out string? error)
    {
        lock (_gate)
        {
            if (!_attempted)
            {
                _attempted = true;
                HeifNative.TryCreate(_loader, out _native, out _loadError);
            }

            native = _native;
            error = _loadError;
            return _native is not null;
        }
    }
}
