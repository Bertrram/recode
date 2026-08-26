namespace Recode.Shell;

/// <summary>
/// Identities that must never change once a build has shipped.
/// </summary>
/// <remarks>
/// The CLSID appears in AppxManifest.xml as well. If the two disagree, Explorer
/// silently shows no menu at all, with nothing in any log to say why, so they
/// are checked against each other by tools/build-package.ps1 rather than by
/// anyone remembering.
/// </remarks>
internal static class ShellIds
{
    /// <summary>The class Explorer instantiates. Also written in AppxManifest.xml.</summary>
    internal const string ConvertCommandClsidText = "018E5409-E5B6-4961-8779-67741A425A20";

    internal static readonly Guid ConvertCommandClsid = new(ConvertCommandClsidText);

    /// <summary>
    /// Canonical names. Explorer uses these to identify a command across
    /// sessions, for instance when a user pins or reorders entries.
    /// </summary>
    internal static readonly Guid ConvertCanonicalName = new("706A91BB-2D91-493E-8A13-5C92107CE0D4");

    internal static readonly Guid SupportCanonicalName = new("2A41BEFE-7E86-4B2B-B558-34878BFDA8B7");

    internal static readonly Guid IUnknown = new("00000000-0000-0000-C000-000000000046");

    internal static readonly Guid IClassFactory = new("00000001-0000-0000-C000-000000000046");

    internal static readonly Guid IExplorerCommand = new("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9");

    internal static readonly Guid IEnumExplorerCommand = new("A88826F8-186F-4987-AADE-EA0CEF8FBFE8");
}
