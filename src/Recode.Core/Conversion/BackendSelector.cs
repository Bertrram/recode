using Recode.Core.Formats;

namespace Recode.Core.Conversion;

/// <summary>
/// Which backend decodes and which encodes, for a given pair of formats.
/// </summary>
/// <param name="DecoderBackendId">Backend that reads the source.</param>
/// <param name="EncoderBackendId">Backend that writes the target.</param>
public readonly record struct BackendPair(string DecoderBackendId, string EncoderBackendId)
{
    /// <summary>True when one backend handles both ends, as in PNG to JPEG.</summary>
    public bool IsSingleBackend =>
        string.Equals(DecoderBackendId, EncoderBackendId, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Picks backends from the format table.
/// </summary>
/// <remarks>
/// Pure, and separate from the code that owns codec instances, so the choice
/// can be asserted in tests without a single native library being present.
/// The selection itself is uninteresting by design: each format names its
/// backend in formats.json and this reads it. That is the whole point of having
/// the table.
/// </remarks>
public sealed class BackendSelector
{
    private readonly FormatTable _table;

    public BackendSelector(FormatTable table)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
    }

    /// <summary>
    /// Resolves the backend pair for a conversion.
    /// </summary>
    /// <exception cref="ConversionNotSupportedException">
    /// The source cannot be read or the target cannot be written.
    /// </exception>
    public BackendPair Select(ImageFormat source, ImageFormat target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (!source.CanRead)
        {
            throw new ConversionNotSupportedException($"{source.DisplayName} cannot be used as a source format.");
        }

        if (!target.CanWrite)
        {
            throw new ConversionNotSupportedException($"{target.DisplayName} cannot be used as a target format.");
        }

        // Both sides are validated against the table so that a typo in
        // formats.json fails here, with a clear message, rather than later as a
        // missing codec.
        _ = _table.GetBackendFor(source);
        _ = _table.GetBackendFor(target);

        return new BackendPair(source.BackendId, target.BackendId);
    }
}

public sealed class ConversionNotSupportedException : Exception
{
    public ConversionNotSupportedException(string message) : base(message) { }
}
