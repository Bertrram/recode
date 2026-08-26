using Recode.Core.Formats;

namespace Recode.Core.Conversion;

/// <summary>
/// Turns whatever the user typed after --quality into a value an encoder will accept.
/// </summary>
/// <remarks>
/// Clamping rather than rejecting. Someone who types --quality 150 wants the
/// best the format offers, and refusing the whole batch over it would be
/// unhelpful. Someone who types 0 wants the smallest file. Both get what they
/// meant.
/// </remarks>
public static class QualityRange
{
    public const int Min = 1;
    public const int Max = 100;

    public static int Clamp(int value) => value < Min ? Min : value > Max ? Max : value;

    /// <summary>
    /// Resolves the quality to use for a target format. A format that ignores
    /// quality reports its own default, so nothing downstream has to special
    /// case PNG against JPEG.
    /// </summary>
    public static int Resolve(int? requested, ImageFormat target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!target.SupportsQuality)
        {
            return Clamp(target.DefaultQuality);
        }

        return requested.HasValue ? Clamp(requested.Value) : Clamp(target.DefaultQuality);
    }
}
