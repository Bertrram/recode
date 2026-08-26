namespace Recode.Core.Formats;

/// <summary>
/// A conversion destination: a format together with the specific extension the
/// output file will carry.
/// </summary>
/// <remarks>
/// This pairing is what makes "--to jpg" and "--to jpeg" behave the way a user
/// expects. Both select the JPEG encoder, but the file lands with the extension
/// that was asked for rather than a canonical one chosen on the user's behalf.
/// </remarks>
public sealed record TargetFormat(ImageFormat Format, string Extension)
{
    /// <summary>The token accepted on the command line, for example "jpeg".</summary>
    public string Token => Extension.TrimStart('.');

    /// <summary>The label shown in the context menu, for example "JPEG".</summary>
    public string MenuLabel => Format.MenuLabelFor(Extension);

    public override string ToString() => Token;
}
