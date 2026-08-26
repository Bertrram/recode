using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recode.Core.Formats;

/// <summary>
/// Reads formats.json into a <see cref="FormatTable"/>.
/// </summary>
/// <remarks>
/// Deserialisation goes through a source generated context rather than
/// reflection. The shell extension is compiled ahead of time, where reflection
/// based serialisation either fails outright or survives only by keeping the
/// whole reflection stack alive.
/// </remarks>
public static class FormatTableLoader
{
    private const string ResourceName = "Recode.Core.formats.json";

    private static FormatTable? _embedded;

    /// <summary>
    /// The table compiled into this assembly. Cached, because every entry point
    /// asks for it and parsing it twice would be pointless.
    /// </summary>
    public static FormatTable LoadEmbedded()
    {
        return _embedded ??= LoadEmbeddedCore();
    }

    private static FormatTable LoadEmbeddedCore()
    {
        using var stream = typeof(FormatTableLoader).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new FormatTableException(
                $"The embedded resource '{ResourceName}' is missing. The build did not include formats.json.");

        return Load(stream);
    }

    public static FormatTable Load(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);

        DocumentDto? document;
        try
        {
            document = JsonSerializer.Deserialize(json, FormatTableJsonContext.Default.DocumentDto);
        }
        catch (JsonException ex)
        {
            throw new FormatTableException($"formats.json is not valid JSON: {ex.Message}", ex);
        }

        if (document is null)
        {
            throw new FormatTableException("formats.json is empty.");
        }

        return Build(document);
    }

    public static FormatTable Load(string json)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return Load(stream);
    }

    private static FormatTable Build(DocumentDto document)
    {
        if (document.Formats is null || document.Formats.Count == 0)
        {
            throw new FormatTableException("formats.json declares no formats.");
        }
        if (document.Backends is null || document.Backends.Count == 0)
        {
            throw new FormatTableException("formats.json declares no backends.");
        }

        var backends = document.Backends.Select(b => new BackendDefinition
        {
            Id = Require(b.Id, "backend id"),
            DisplayName = b.DisplayName ?? Require(b.Id, "backend id"),
            Description = b.Description ?? string.Empty,
            Bundled = b.Bundled,
            Libraries = (IReadOnlyList<string>?)b.Libraries ?? Array.Empty<string>()
        }).ToList();

        var formats = document.Formats.Select(f => new ImageFormat
        {
            Id = Require(f.Id, "format id"),
            DisplayName = f.DisplayName ?? Require(f.Id, "format id"),
            Extensions = NormaliseExtensions(f),
            BackendId = Require(f.Backend, $"backend on format '{f.Id}'"),
            Decoder = f.Decoder ?? string.Empty,
            Encoder = f.Encoder ?? string.Empty,
            CanRead = f.CanRead,
            CanWrite = f.CanWrite,
            SupportsQuality = f.SupportsQuality,
            DefaultQuality = f.DefaultQuality is >= 1 and <= 100 ? f.DefaultQuality : 85,
            SupportsAlpha = f.SupportsAlpha,
            Compression = f.Compression
        }).ToList();

        return new FormatTable(formats, backends);
    }

    private static IReadOnlyList<string> NormaliseExtensions(FormatDto dto)
    {
        if (dto.Extensions is null || dto.Extensions.Count == 0)
        {
            throw new FormatTableException($"Format '{dto.Id}' declares no extensions.");
        }

        return dto.Extensions
            .Select(e => e.Trim().ToLowerInvariant())
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToList();
    }

    private static string Require(string? value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatTableException($"formats.json is missing a required value: {what}.");
        }
        return value;
    }

    internal sealed class DocumentDto
    {
        public int SchemaVersion { get; set; }
        public List<BackendDto>? Backends { get; set; }
        public List<FormatDto>? Formats { get; set; }
    }

    internal sealed class BackendDto
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public bool Bundled { get; set; }
        public List<string>? Libraries { get; set; }
    }

    internal sealed class FormatDto
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public List<string>? Extensions { get; set; }
        public string? Backend { get; set; }
        public string? Decoder { get; set; }
        public string? Encoder { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool SupportsQuality { get; set; }
        public int DefaultQuality { get; set; }
        public bool SupportsAlpha { get; set; }
        public string? Compression { get; set; }
    }
}

/// <summary>
/// Source generated deserialisation for formats.json.
/// </summary>
/// <remarks>
/// PropertyNameCaseInsensitive so that the file can stay in the camelCase it is
/// written in. ReadCommentHandling because formats.json carries an explanatory
/// header, and a format table nobody can annotate is a format table nobody
/// maintains.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(FormatTableLoader.DocumentDto))]
internal sealed partial class FormatTableJsonContext : JsonSerializerContext
{
}
