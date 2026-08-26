using Recode.Core.Abstractions;

namespace Recode.Core.Conversion;

/// <summary>
/// Composites transparency onto a solid background.
/// </summary>
/// <remarks>
/// Needed whenever the target format has no alpha channel. Without it a
/// transparent PNG converted to JPEG comes out with black where the
/// transparency was, because the colour channels under a fully transparent
/// pixel are usually zero.
///
/// White is the default background. It is the safe assumption for the common
/// case, which is a logo or a screenshot heading for a document or an email.
/// </remarks>
public static class AlphaFlattener
{
    public static readonly (byte R, byte G, byte B) White = (255, 255, 255);

    /// <summary>
    /// Flattens in place. The source is assumed to be straight, not
    /// premultiplied, alpha, which is what every codec here produces.
    /// </summary>
    public static void Flatten(RgbaImage image, (byte R, byte G, byte B) background)
    {
        ArgumentNullException.ThrowIfNull(image);

        var pixels = image.Pixels;
        var length = image.Width * image.Height * 4;

        for (var i = 0; i < length; i += 4)
        {
            var alpha = pixels[i + 3];

            if (alpha == 255)
            {
                continue;
            }

            if (alpha == 0)
            {
                pixels[i]     = background.R;
                pixels[i + 1] = background.G;
                pixels[i + 2] = background.B;
                pixels[i + 3] = 255;
                continue;
            }

            // out = src * a + background * (1 - a), with a in 0..255.
            // The +127 rounds to nearest instead of truncating, which keeps a
            // 50 percent grey from drifting a level darker on every pass.
            var inverse = 255 - alpha;
            pixels[i]     = (byte)((pixels[i]     * alpha + background.R * inverse + 127) / 255);
            pixels[i + 1] = (byte)((pixels[i + 1] * alpha + background.G * inverse + 127) / 255);
            pixels[i + 2] = (byte)((pixels[i + 2] * alpha + background.B * inverse + 127) / 255);
            pixels[i + 3] = 255;
        }
    }

    public static void FlattenOntoWhite(RgbaImage image) => Flatten(image, White);
}
