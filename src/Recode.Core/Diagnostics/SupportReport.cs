using Recode.Core.Formats;

namespace Recode.Core.Diagnostics;

/// <summary>
/// One row of the support table: a single extension and what can be done with it.
/// </summary>
/// <remarks>
/// Rows are per extension rather than per format, so .jpg and .jpeg each get a
/// line. That matches what the context menu offers, and it means the table
/// answers the question a user actually has, which is about a file on their
/// disk rather than about a codec.
/// </remarks>
public sealed record FormatSupportRow
{
    public required string DisplayName { get; init; }

    public required string Extension { get; init; }

    public required bool CanRead { get; init; }

    public required bool CanWrite { get; init; }

    public required string Backend { get; init; }

    /// <summary>False when the backend could not be loaded.</summary>
    public required bool Available { get; init; }

    /// <summary>What is wrong, when anything is. Null in the normal case.</summary>
    public string? Problem { get; init; }
}

/// <summary>Status of one backend.</summary>
public sealed record BackendStatus
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public required bool Bundled { get; init; }

    public required bool Available { get; init; }

    /// <summary>Version reported by the library, when it could be asked.</summary>
    public string? Version { get; init; }

    public string? MissingLibrary { get; init; }

    public string? ExpectedLocation { get; init; }

    public string? Problem { get; init; }
}

/// <summary>
/// Everything the support window and <c>--list</c> need to say.
/// </summary>
public sealed record SupportReport
{
    public required IReadOnlyList<FormatSupportRow> Rows { get; init; }

    public required IReadOnlyList<BackendStatus> Backends { get; init; }

    /// <summary>
    /// True in the normal case, because every backend is either part of Windows
    /// or shipped alongside the executable. False means a file is missing.
    /// </summary>
    public bool Healthy => Backends.All(b => b.Available);

    public IEnumerable<BackendStatus> BrokenBackends => Backends.Where(b => !b.Available);
}
