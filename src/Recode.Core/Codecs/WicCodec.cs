using System.Windows.Media;
using System.Windows.Media.Imaging;
using Recode.Core.Abstractions;
using Recode.Core.Conversion;
using Recode.Core.Formats;

namespace Recode.Core.Codecs;

/// <summary>
/// JPEG, PNG, BMP, TIFF and GIF, through the Windows Imaging Component.
/// </summary>
/// <remarks>
/// WIC is reached through System.Windows.Media.Imaging, which is the managed
/// wrapper Windows already ships as part of WPF. That avoids both a page of
/// hand written COM interop and a NuGet dependency, and it is the same
/// underlying component either way.
///
/// This backend needs nothing bundled, which is why five of the nine formats
/// work even if every native DLL is deleted.
/// </remarks>
public sealed class WicCodec : IImageCodec
{
    public const string Id = "wic";

    /// <summary>
    /// Where WIC keeps the EXIF orientation tag, which differs by container.
    /// Tried in order; the first query that yields a number wins.
    /// </summary>
    private static readonly string[] OrientationQueries =
    {
        "/app1/ifd/{ushort=274}",   // JPEG
        "/ifd/{ushort=274}",        // TIFF
        "/app1/{ushort=0}/{ushort=274}"
    };

    public string BackendId => Id;

    public bool CanDecode(ImageFormat format) =>
        format.BackendId == Id && format.CanRead;

    public bool CanEncode(ImageFormat format) =>
        format.BackendId == Id && format.CanWrite;

    /// <summary>Always available. WIC is part of Windows.</summary>
    public BackendAvailability CheckAvailability() => BackendAvailability.Ok;

    public RgbaImage Decode(string path, ImageFormat format)
    {
        BitmapFrame frame;

        try
        {
            using var stream = File.OpenRead(path);

            // OnLoad rather than OnDemand, so the frame does not hold the file
            // open. That matters when the output overwrites the input.
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count == 0)
            {
                throw new CodecException($"{Path.GetFileName(path)} contains no image frames.");
            }

            frame = decoder.Frames[0];
        }
        catch (CodecException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CodecException($"{Path.GetFileName(path)} could not be decoded: {ex.Message}", ex);
        }

        var image = ToRgba(frame, path);
        var orientation = ReadOrientation(frame);
        return ExifOrientation.Apply(image, orientation);
    }

    public void Encode(RgbaImage image, ImageFormat format, string path, EncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);

        var encoder = CreateEncoder(format, options);
        var source = CreateSource(image, format.SupportsAlpha);

        encoder.Frames.Add(BitmapFrame.Create(source));

        try
        {
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        catch (Exception ex)
        {
            throw new CodecException($"{format.DisplayName} could not be written: {ex.Message}", ex);
        }
    }

    private static BitmapEncoder CreateEncoder(ImageFormat format, EncodeOptions options)
    {
        return format.Id switch
        {
            "jpeg" => new JpegBitmapEncoder { QualityLevel = options.Quality },
            "png"  => new PngBitmapEncoder(),
            "bmp"  => new BmpBitmapEncoder(),
            "tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Zip },
            "gif"  => new GifBitmapEncoder(),
            _ => throw new CodecException(
                $"The WIC backend has no encoder for '{format.Id}'. formats.json and this backend disagree.")
        };
    }

    private static BitmapSource CreateSource(RgbaImage image, bool withAlpha)
    {
        if (withAlpha)
        {
            var bgra = (byte[])image.Pixels.Clone();
            PixelOrder.SwapRedAndBlue(bgra, image.Width * image.Height * 4);

            return BitmapSource.Create(
                image.Width, image.Height, 96, 96,
                PixelFormats.Bgra32, null, bgra, image.Stride);
        }

        var bgr = PixelOrder.RgbaToBgr24(image.Pixels, image.Width, image.Height);
        return BitmapSource.Create(
            image.Width, image.Height, 96, 96,
            PixelFormats.Bgr24, null, bgr, image.Width * 3);
    }

    private static RgbaImage ToRgba(BitmapSource frame, string path)
    {
        BitmapSource converted = frame.Format == PixelFormats.Bgra32
            ? frame
            : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

        var width = converted.PixelWidth;
        var height = converted.PixelHeight;

        if (width <= 0 || height <= 0)
        {
            throw new CodecException($"{Path.GetFileName(path)} reports a size of {width}x{height}.");
        }

        var stride = width * 4;
        var pixels = new byte[checked((long)stride * height)];
        converted.CopyPixels(pixels, stride, 0);

        PixelOrder.SwapRedAndBlue(pixels, pixels.Length);

        return new RgbaImage(width, height, pixels);
    }

    /// <summary>
    /// Reads EXIF tag 274. Returns 1 when the tag is absent or unreadable,
    /// because a missing orientation is the overwhelmingly common case and is
    /// not an error.
    /// </summary>
    private static int ReadOrientation(BitmapFrame frame)
    {
        if (frame.Metadata is not BitmapMetadata metadata)
        {
            return ExifOrientation.Normal;
        }

        foreach (var query in OrientationQueries)
        {
            try
            {
                if (!metadata.ContainsQuery(query))
                {
                    continue;
                }

                var value = metadata.GetQuery(query);
                if (value is ushort orientation && orientation is >= 1 and <= 8)
                {
                    return orientation;
                }
            }
            catch (Exception)
            {
                // Some containers throw rather than answer for queries that do
                // not apply to them. Not worth reporting: the next query, or
                // the default, handles it.
            }
        }

        return ExifOrientation.Normal;
    }
}
