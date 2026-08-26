namespace Recode.Core.Registry;

/// <summary>
/// Inputs to the context menu layout.
/// </summary>
/// <remarks>
/// Everything here is a value rather than something read from the environment,
/// so the generator is a pure function of its arguments and the format table.
/// That is what makes the registry layout testable.
/// </remarks>
public sealed record ContextMenuOptions
{
    /// <summary>Full path to recode.exe. Used for both the command and the icon.</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Where the menu lives. HKCU means no administrator rights are needed,
    /// which is the whole reason for choosing it.
    /// </summary>
    public string ClassesRoot { get; init; } = @"HKCU\Software\Classes";

    /// <summary>
    /// Key name for the top level verb. Prefixed so that anything belonging to
    /// Recode is recognisable in the registry at a glance, and so uninstalling
    /// can find it without a stored list.
    /// </summary>
    public string VerbKeyName { get; init; } = "Recode.ConvertTo";

    public string MenuLabel { get; init; } = "Convert to";

    public string SupportLabel { get; init; } = "Format support";

    /// <summary>
    /// Icon reference written to each key, normally "path\to\recode.exe,0".
    /// Explorer draws it beside every entry.
    /// </summary>
    public string IconReference => $"{ExecutablePath},0";
}
