using Recode.Core.Diagnostics;

namespace Recode.App.Ui;

/// <summary>
/// One row of the support table, shaped for binding.
/// </summary>
public sealed class SupportRowView
{
    public required string Format { get; init; }

    public required string Read { get; init; }

    public required string Write { get; init; }

    public required string Backend { get; init; }

    /// <summary>Drives the row colour. False turns the row red.</summary>
    public required bool Available { get; init; }

    public static SupportRowView From(FormatSupportRow row)
    {
        return new SupportRowView
        {
            Format = row.DisplayName,
            Read = Mark(row.CanRead && row.Available),
            Write = Mark(row.CanWrite && row.Available),
            Backend = row.Backend,
            Available = row.Available
        };
    }

    private static string Mark(bool value) => value ? "✓" : "✕";
}
