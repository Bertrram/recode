namespace Recode.Core.Abstractions;

/// <summary>
/// A file could not be decoded or encoded.
/// </summary>
/// <remarks>
/// Thrown per file, caught per file. One unreadable image in a selection of
/// forty must not take the other thirty nine down with it.
/// </remarks>
public class CodecException : Exception
{
    public CodecException(string message) : base(message) { }

    public CodecException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// A backend could not be used at all, usually because a bundled library is
/// missing or will not load.
/// </summary>
/// <remarks>
/// Separate from <see cref="CodecException"/> because the remedy is different.
/// A corrupt file is the file's problem. A missing DLL is an installation
/// problem, and the support window can point straight at it.
/// </remarks>
public sealed class BackendUnavailableException : CodecException
{
    public BackendUnavailableException(string backendId, BackendAvailability availability)
        : base(Describe(backendId, availability))
    {
        BackendId = backendId;
        Availability = availability;
    }

    public string BackendId { get; }

    public BackendAvailability Availability { get; }

    private static string Describe(string backendId, BackendAvailability availability)
    {
        if (availability.MissingLibrary is not null && availability.ExpectedLocation is not null)
        {
            return $"The {backendId} backend is unavailable: {availability.MissingLibrary} was not found. " +
                   $"It belongs in {availability.ExpectedLocation}.";
        }

        return $"The {backendId} backend is unavailable: {availability.Detail ?? "unknown reason"}.";
    }
}
