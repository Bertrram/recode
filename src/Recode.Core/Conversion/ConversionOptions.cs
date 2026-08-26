namespace Recode.Core.Conversion;

/// <summary>
/// Settings that apply to a whole run.
/// </summary>
/// <param name="Quality">
/// What the user asked for, or null to use the target format's default.
/// Unclamped here; <see cref="QualityRange"/> deals with that.
/// </param>
/// <param name="Force">
/// Replace an existing file instead of finding a free name. Also what makes
/// recompressing to the same format overwrite the original rather than write a
/// numbered copy beside it.
/// </param>
/// <param name="OutputDirectory">
/// Where results go, or null to write beside each input. Created if missing.
/// </param>
public sealed record ConversionOptions(
    int? Quality = null,
    bool Force = false,
    string? OutputDirectory = null)
{
    public static ConversionOptions Default { get; } = new();
}
