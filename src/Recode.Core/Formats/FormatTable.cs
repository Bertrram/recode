using System.Diagnostics.CodeAnalysis;

namespace Recode.Core.Formats;

/// <summary>
/// The format matrix, loaded from formats.json.
/// </summary>
/// <remarks>
/// Every part of the program that needs to know which formats exist asks this
/// object: the command line parser, the context menu generator, the support
/// window and the backend selector. Nothing else holds a list of formats.
/// </remarks>
public sealed class FormatTable
{
    private readonly Dictionary<string, ImageFormat> _byExtension;
    private readonly Dictionary<string, ImageFormat> _byId;
    private readonly Dictionary<string, BackendDefinition> _backendsById;

    public FormatTable(IReadOnlyList<ImageFormat> formats, IReadOnlyList<BackendDefinition> backends)
    {
        Formats = formats ?? throw new ArgumentNullException(nameof(formats));
        Backends = backends ?? throw new ArgumentNullException(nameof(backends));

        _byId = new Dictionary<string, ImageFormat>(StringComparer.OrdinalIgnoreCase);
        _byExtension = new Dictionary<string, ImageFormat>(StringComparer.OrdinalIgnoreCase);
        _backendsById = new Dictionary<string, BackendDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var backend in backends)
        {
            if (!_backendsById.TryAdd(backend.Id, backend))
            {
                throw new FormatTableException($"Backend id '{backend.Id}' appears more than once.");
            }
        }

        foreach (var format in formats)
        {
            if (!_byId.TryAdd(format.Id, format))
            {
                throw new FormatTableException($"Format id '{format.Id}' appears more than once.");
            }

            if (!_backendsById.ContainsKey(format.BackendId))
            {
                throw new FormatTableException(
                    $"Format '{format.Id}' names backend '{format.BackendId}', which is not defined.");
            }

            if (format.Extensions.Count == 0)
            {
                throw new FormatTableException($"Format '{format.Id}' has no extensions.");
            }

            foreach (var extension in format.Extensions)
            {
                if (!extension.StartsWith('.'))
                {
                    throw new FormatTableException(
                        $"Extension '{extension}' on format '{format.Id}' must start with a dot.");
                }

                if (!_byExtension.TryAdd(extension, format))
                {
                    throw new FormatTableException(
                        $"Extension '{extension}' is claimed by both '{_byExtension[extension].Id}' and '{format.Id}'.");
                }
            }
        }
    }

    public IReadOnlyList<ImageFormat> Formats { get; }

    public IReadOnlyList<BackendDefinition> Backends { get; }

    /// <summary>Every extension that may appear as a conversion source.</summary>
    public IEnumerable<string> ReadableExtensions =>
        Formats.Where(f => f.CanRead).SelectMany(f => f.Extensions);

    /// <summary>
    /// Every conversion destination, one per writable extension. JPEG appears
    /// twice here, once as .jpg and once as .jpeg.
    /// </summary>
    public IEnumerable<TargetFormat> WritableTargets =>
        Formats.Where(f => f.CanWrite).SelectMany(f => f.Extensions.Select(e => new TargetFormat(f, e)));

    public BackendDefinition GetBackend(string backendId)
    {
        if (_backendsById.TryGetValue(backendId, out var backend))
        {
            return backend;
        }
        throw new FormatTableException($"No backend named '{backendId}'.");
    }

    public BackendDefinition GetBackendFor(ImageFormat format) => GetBackend(format.BackendId);

    public bool TryGetById(string id, [NotNullWhen(true)] out ImageFormat? format) =>
        _byId.TryGetValue(id, out format);

    /// <summary>
    /// Looks a format up by file extension. Accepts ".png" and "png" alike, and
    /// is case insensitive, because both forms turn up: one from file paths and
    /// one from the command line.
    /// </summary>
    public bool TryGetByExtension(string extension, [NotNullWhen(true)] out ImageFormat? format)
    {
        format = null;
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        var normalised = Normalise(extension);
        return _byExtension.TryGetValue(normalised, out format);
    }

    /// <summary>
    /// Resolves what the user typed after --to, or what a file path ends with,
    /// into a concrete destination.
    /// </summary>
    public bool TryResolveTarget(string token, [NotNullWhen(true)] out TargetFormat? target)
    {
        target = null;
        if (!TryGetByExtension(token, out var format) || !format.CanWrite)
        {
            return false;
        }

        var normalised = Normalise(token);
        target = new TargetFormat(format, normalised);
        return true;
    }

    /// <summary>Resolves the source format of a file from its path.</summary>
    public bool TryGetForPath(string path, [NotNullWhen(true)] out ImageFormat? format)
    {
        format = null;
        var extension = Path.GetExtension(path);
        return !string.IsNullOrEmpty(extension) && TryGetByExtension(extension, out format);
    }

    private static string Normalise(string extension)
    {
        var trimmed = extension.Trim();
        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed;
        }
        return trimmed.ToLowerInvariant();
    }
}

public sealed class FormatTableException : Exception
{
    public FormatTableException(string message) : base(message) { }
    public FormatTableException(string message, Exception inner) : base(message, inner) { }
}
