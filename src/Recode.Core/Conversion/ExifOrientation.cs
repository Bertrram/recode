using Recode.Core.Abstractions;

namespace Recode.Core.Conversion;

/// <summary>
/// Applies the EXIF orientation tag to decoded pixels.
/// </summary>
/// <remarks>
/// Orientation is baked into the pixels rather than copied to the output file.
/// That is the only choice that works across the whole format matrix: PNG, BMP
/// and GIF have nowhere to put an orientation tag, so a photograph taken
/// sideways would come out sideways if the tag were merely forwarded.
///
/// The cost is that a JPEG to JPEG recompression rewrites the pixels rather
/// than the tag. The result looks identical, which is what matters.
///
/// Values follow EXIF tag 274. A pure function on purpose, so the geometry can
/// be tested without a decoder anywhere near it.
/// </remarks>
public static class ExifOrientation
{
    public const int Normal = 1;

    /// <summary>True when the tag calls for anything other than leaving pixels alone.</summary>
    public static bool IsTransforming(int orientation) => orientation is >= 2 and <= 8;

    /// <summary>True when the tag swaps width and height.</summary>
    public static bool SwapsAxes(int orientation) => orientation is 5 or 6 or 7 or 8;

    /// <summary>
    /// Returns an image with the orientation applied. Returns the input
    /// unchanged for orientation 1, or for any value outside the range, since
    /// a corrupt tag is not a reason to refuse a file.
    /// </summary>
    public static RgbaImage Apply(RgbaImage source, int orientation)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!IsTransforming(orientation))
        {
            return source;
        }

        var sourceWidth = source.Width;
        var sourceHeight = source.Height;

        var swaps = SwapsAxes(orientation);
        var destWidth = swaps ? sourceHeight : sourceWidth;
        var destHeight = swaps ? sourceWidth : sourceHeight;

        var result = RgbaImage.Allocate(destWidth, destHeight);
        var src = source.Pixels;
        var dst = result.Pixels;

        for (var dy = 0; dy < destHeight; dy++)
        {
            var destRow = dy * destWidth * 4;

            for (var dx = 0; dx < destWidth; dx++)
            {
                var (sx, sy) = MapToSource(orientation, dx, dy, sourceWidth, sourceHeight);

                var srcIndex = (sy * sourceWidth + sx) * 4;
                var dstIndex = destRow + dx * 4;

                dst[dstIndex]     = src[srcIndex];
                dst[dstIndex + 1] = src[srcIndex + 1];
                dst[dstIndex + 2] = src[srcIndex + 2];
                dst[dstIndex + 3] = src[srcIndex + 3];
            }
        }

        return result;
    }

    /// <summary>
    /// Given a destination pixel, returns the source pixel it comes from.
    /// Iterating the destination rather than the source means every output
    /// pixel is written exactly once, with no gaps to fill in afterwards.
    /// </summary>
    internal static (int X, int Y) MapToSource(int orientation, int dx, int dy, int sourceWidth, int sourceHeight)
    {
        return orientation switch
        {
            2 => (sourceWidth - 1 - dx, dy),                          // mirror horizontal
            3 => (sourceWidth - 1 - dx, sourceHeight - 1 - dy),       // rotate 180
            4 => (dx, sourceHeight - 1 - dy),                         // mirror vertical
            5 => (dy, dx),                                            // transpose
            6 => (dy, sourceHeight - 1 - dx),                         // rotate 90 clockwise
            7 => (sourceWidth - 1 - dy, sourceHeight - 1 - dx),       // transverse
            8 => (sourceWidth - 1 - dy, dx),                          // rotate 90 anticlockwise
            _ => (dx, dy)
        };
    }
}
