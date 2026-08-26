namespace Recode.Core.Conversion;

public enum ConversionStatus
{
    Converted,
    Failed,
    Skipped
}

/// <summary>
/// The outcome of converting one file.
/// </summary>
/// <remarks>
/// A result rather than an exception, because a batch keeps going after a
/// failure. The caller decides what to print and what to exit with.
/// </remarks>
public sealed record ConversionResult
{
    public required string InputPath { get; init; }

    public required ConversionStatus Status { get; init; }

    /// <summary>Set when the status is Converted.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Why it failed or was skipped. Written for a person to read.</summary>
    public string? Message { get; init; }

    public bool Succeeded => Status == ConversionStatus.Converted;

    public static ConversionResult Converted(string input, string output) =>
        new() { InputPath = input, Status = ConversionStatus.Converted, OutputPath = output };

    public static ConversionResult Failed(string input, string message) =>
        new() { InputPath = input, Status = ConversionStatus.Failed, Message = message };

    public static ConversionResult Skipped(string input, string message) =>
        new() { InputPath = input, Status = ConversionStatus.Skipped, Message = message };
}

/// <summary>Results for a whole run.</summary>
public sealed record ConversionSummary(IReadOnlyList<ConversionResult> Results)
{
    public int ConvertedCount => Results.Count(r => r.Status == ConversionStatus.Converted);

    public int FailedCount => Results.Count(r => r.Status == ConversionStatus.Failed);

    public int SkippedCount => Results.Count(r => r.Status == ConversionStatus.Skipped);

    /// <summary>
    /// True when nothing failed. A skipped file counts as a failure, because
    /// the user asked for a conversion and did not get one.
    /// </summary>
    public bool AllSucceeded => Results.Count > 0 && Results.All(r => r.Succeeded);
}
