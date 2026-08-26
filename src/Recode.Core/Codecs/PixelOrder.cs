namespace Recode.Core.Codecs;

/// <summary>
/// Swaps between RGBA, which every codec here exchanges, and BGRA, which is
/// what Windows imaging works in.
/// </summary>
internal static class PixelOrder
{
    /// <summary>
    /// Exchanges the first and third byte of every four byte pixel, in place.
    /// The operation is its own inverse, so one method covers both directions.
    /// </summary>
    internal static void SwapRedAndBlue(byte[] pixels, int length)
    {
        for (var i = 0; i < length; i += 4)
        {
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }
    }

    /// <summary>
    /// Drops the alpha channel, producing three bytes per pixel in BGR order.
    /// Used for encoders that will not accept an alpha channel. Transparency is
    /// expected to have been flattened already, so alpha is discarded here
    /// rather than composited.
    /// </summary>
    internal static byte[] RgbaToBgr24(byte[] rgba, int width, int height)
    {
        var result = new byte[checked(width * height * 3)];
        var source = 0;
        var dest = 0;

        for (var i = 0; i < width * height; i++)
        {
            result[dest]     = rgba[source + 2]; // B
            result[dest + 1] = rgba[source + 1]; // G
            result[dest + 2] = rgba[source];     // R
            source += 4;
            dest += 3;
        }

        return result;
    }
}
