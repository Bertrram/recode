namespace Recode.App.Cli;

public enum CommandMode
{
    /// <summary>Convert the given files. The normal case.</summary>
    Convert,

    /// <summary>Print the format table and which backend handles each entry.</summary>
    List,

    /// <summary>Open the support window.</summary>
    About,

    /// <summary>Print usage.</summary>
    Help,

    /// <summary>
    /// Print the context menu registry layout as JSON. Used by the install
    /// script so that the keys come from the format table rather than from a
    /// second copy of the list kept in PowerShell.
    /// </summary>
    EmitRegistry
}

public sealed record CommandLineOptions
{
    public required CommandMode Mode { get; init; }

    /// <summary>What followed --to, before it has been checked against the format table.</summary>
    public string? TargetToken { get; init; }

    public int? Quality { get; init; }

    public bool Force { get; init; }

    public string? OutputDirectory { get; init; }

    public IReadOnlyList<string> Inputs { get; init; } = Array.Empty<string>();

    /// <summary>Set when the command line could not be understood.</summary>
    public string? Error { get; init; }

    public bool HasError => Error is not null;

    public static CommandLineOptions Invalid(string error) =>
        new() { Mode = CommandMode.Help, Error = error };
}
