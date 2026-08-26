namespace Recode.Core.Abstractions;

/// <summary>
/// Encoder settings that survive the trip across the backend boundary.
/// </summary>
/// <param name="Quality">
/// One to a hundred. Already clamped and already resolved against the target
/// format's default by the time a codec sees it, so codecs never guess.
/// Formats that ignore quality simply ignore it.
/// </param>
public readonly record struct EncodeOptions(int Quality)
{
    public static EncodeOptions Default => new(85);
}
