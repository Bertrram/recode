namespace Recode.Core.Formats;

/// <summary>
/// A decoding and encoding backend as described by formats.json.
/// </summary>
public sealed record BackendDefinition
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    /// <summary>
    /// True when the backend needs native libraries shipped next to the
    /// executable. False for WIC, which is part of Windows.
    /// </summary>
    public required bool Bundled { get; init; }

    /// <summary>
    /// File names the backend loads at run time. Used both for loading and for
    /// telling the user which file is missing when loading fails.
    /// </summary>
    public required IReadOnlyList<string> Libraries { get; init; }
}
