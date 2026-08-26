namespace Recode.Core.Abstractions;

/// <summary>
/// Whether a backend can run, and if not, what is wrong in terms the user can
/// act on.
/// </summary>
/// <param name="Available">True when the backend is ready to use.</param>
/// <param name="MissingLibrary">
/// The file that could not be loaded, for example "heif.dll". Null when the
/// problem is something else.
/// </param>
/// <param name="ExpectedLocation">
/// Where that file was expected to be. The support window shows this so a
/// broken copy is a two second fix rather than a mystery.
/// </param>
/// <param name="Detail">A short sentence describing the failure.</param>
public sealed record BackendAvailability(
    bool Available,
    string? MissingLibrary = null,
    string? ExpectedLocation = null,
    string? Detail = null)
{
    public static BackendAvailability Ok { get; } = new(true);

    public static BackendAvailability Missing(string library, string expectedLocation, string detail) =>
        new(false, library, expectedLocation, detail);

    public static BackendAvailability Broken(string detail) =>
        new(false, null, null, detail);
}
