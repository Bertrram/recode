using Recode.Core.Abstractions;
using Recode.Core.Formats;
using Recode.Core.Native;

namespace Recode.Core.Codecs;

/// <summary>
/// WebP, through libwebp.
/// </summary>
public sealed unsafe class WebpCodec : IImageCodec
{
    public const string Id = "libwebp";

    private readonly INativeLibraryLoader _loader;
    private readonly object _gate = new();

    private WebpNative? _native;
    private string? _loadError;
    private bool _attempted;

    public WebpCodec(INativeLibraryLoader loader)
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

        return BackendAvailability.Missing(WebpNative.LibraryName, _loader.SearchDirectory, error ?? "unknown reason");
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

        fixed (byte* input = data)
        {
            if (!native.GetInfo(input, (nuint)data.Length, out var width, out var height))
            {
                throw new CodecException($"{Path.GetFileName(path)} is not a valid WebP file.");
            }

            if (width <= 0 || height <= 0)
            {
                throw new CodecException($"{Path.GetFileName(path)} reports a size of {width}x{height}.");
            }

            var decoded = native.DecodeRgba(input, (nuint)data.Length, out width, out height);
            if (decoded is null)
            {
                throw new CodecException($"{Path.GetFileName(path)} could not be decoded by libwebp.");
            }

            try
            {
                var image = RgbaImage.Allocate(width, height);
                new ReadOnlySpan<byte>(decoded, image.Pixels.Length).CopyTo(image.Pixels);
                return image;
            }
            finally
            {
                // libwebp allocated this with its own allocator, so it has to
                // free it. Freeing it any other way corrupts the heap.
                native.Free(decoded);
            }
        }
    }

    public void Encode(RgbaImage image, ImageFormat format, string path, EncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);
        var native = RequireNative();

        byte* output = null;
        nuint length;

        fixed (byte* pixels = image.Pixels)
        {
            // Quality 100 goes to the lossless encoder rather than to lossy at
            // its highest setting. A user asking for 100 is asking for no loss,
            // and lossless WebP is usually smaller than lossy at 100 anyway.
            length = options.Quality >= 100
                ? native.EncodeLosslessRgba(pixels, image.Width, image.Height, image.Stride, &output)
                : native.EncodeRgba(pixels, image.Width, image.Height, image.Stride, options.Quality, &output);
        }

        if (length == 0 || output is null)
        {
            throw new CodecException(
                $"libwebp could not encode {image.Width}x{image.Height} pixels. The image may exceed the WebP limit of 16383 pixels per side.");
        }

        try
        {
            using var stream = File.Create(path);
            var span = new ReadOnlySpan<byte>(output, checked((int)length));
            stream.Write(span);
        }
        catch (IOException ex)
        {
            throw new CodecException($"The WebP file could not be written: {ex.Message}", ex);
        }
        finally
        {
            native.Free(output);
        }
    }

    private WebpNative RequireNative()
    {
        if (TryGetNative(out var native, out _))
        {
            return native!;
        }
        throw new BackendUnavailableException(Id, CheckAvailability());
    }

    private bool TryGetNative(out WebpNative? native, out string? error)
    {
        lock (_gate)
        {
            if (!_attempted)
            {
                _attempted = true;
                WebpNative.TryCreate(_loader, out _native, out _loadError);
            }

            native = _native;
            error = _loadError;
            return _native is not null;
        }
    }
}
