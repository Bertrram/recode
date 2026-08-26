namespace Recode.Core.Formats;

/// <summary>
/// One image format as described by formats.json.
/// </summary>
/// <remarks>
/// A format owns one or more extensions. JPEG owns both .jpg and .jpeg and TIFF
/// owns both .tif and .tiff. They share an encoder but appear separately in the
/// context menu, because the extension the user picks is the extension they get.
/// </remarks>
public sealed record ImageFormat
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Lower case, leading dot. The first entry is the primary one.</summary>
    public required IReadOnlyList<string> Extensions { get; init; }

    public required string BackendId { get; init; }

    /// <summary>Library that performs decoding. Display only.</summary>
    public required string Decoder { get; init; }

    /// <summary>Library that performs encoding. Display only.</summary>
    public required string Encoder { get; init; }

    public required bool CanRead { get; init; }

    public required bool CanWrite { get; init; }

    public required bool SupportsQuality { get; init; }

    public required int DefaultQuality { get; init; }

    /// <summary>
    /// When false, transparency is composited onto white before encoding rather
    /// than silently turning into black or garbage.
    /// </summary>
    public required bool SupportsAlpha { get; init; }

    /// <summary>
    /// Compression family for the libheif backend: "hevc" or "av1". Null for
    /// every other backend.
    /// </summary>
    public string? Compression { get; init; }

    public string PrimaryExtension => Extensions[0];

    /// <summary>
    /// Label for a menu entry that targets a specific extension of this format.
    /// Formats with a single extension use their display name, so WebP stays
    /// "WebP" rather than becoming "WEBP". Formats with several extensions use
    /// the extension, so the user can tell .jpg from .jpeg.
    /// </summary>
    public string MenuLabelFor(string extension)
    {
        return Extensions.Count == 1
            ? DisplayName
            : extension.TrimStart('.').ToUpperInvariant();
    }

    /// <summary>
    /// Human readable backend summary, for example "libheif (libde265, kvazaar)".
    /// Built from data so the support window has nothing hard coded in it.
    /// </summary>
    public string DescribeBackend(BackendDefinition backend)
    {
        var parts = new List<string>();
        if (!string.Equals(Decoder, backend.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(Decoder);
        }
        if (!string.Equals(Encoder, backend.DisplayName, StringComparison.OrdinalIgnoreCase) &&
            !parts.Contains(Encoder, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(Encoder);
        }

        return parts.Count == 0
            ? backend.DisplayName
            : $"{backend.DisplayName} ({string.Join(", ", parts)})";
    }
}
