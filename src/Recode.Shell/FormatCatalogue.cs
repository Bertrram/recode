using Recode.Core.Formats;

namespace Recode.Shell;

/// <summary>
/// The format table, loaded once per surrogate process.
/// </summary>
/// <remarks>
/// The same formats.json that drives the converter and the registry menu, so
/// the packaged menu cannot offer a format the program does not have.
///
/// Loading is lazy and never throws out of this class. A shell extension that
/// throws during a right click takes the menu with it, and a user whose
/// Explorer menu is broken has a worse problem than a missing format.
/// </remarks>
internal static class FormatCatalogue
{
    private static readonly Lazy<FormatTable> Lazy = new(Load, isThreadSafe: true);

    internal static FormatTable Table => Lazy.Value;

    /// <summary>True when the table loaded and contains something to offer.</summary>
    internal static bool IsUsable
    {
        get
        {
            try
            {
                return Table.Formats.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    private static FormatTable Load()
    {
        try
        {
            return FormatTableLoader.LoadEmbedded();
        }
        catch (Exception)
        {
            // An empty table means no entries appear. That is the right failure
            // for a broken build: nothing, rather than a menu that misbehaves.
            return new FormatTable(Array.Empty<ImageFormat>(), Array.Empty<BackendDefinition>());
        }
    }
}
