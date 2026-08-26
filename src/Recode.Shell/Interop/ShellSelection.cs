namespace Recode.Shell.Interop;

/// <summary>
/// Reads the file paths out of the selection Explorer hands to a command.
/// </summary>
internal static class ShellSelection
{
    /// <summary>
    /// Explorer will hand over an enormous selection if the user makes one.
    /// Reading every path to decide whether to show a menu entry is wasted work,
    /// so the checks stop once they have seen enough.
    /// </summary>
    private const uint InspectionLimit = 64;

    /// <summary>
    /// Full paths of the selected files. Items with no file system path, such
    /// as a search result or a library, are skipped rather than guessed at.
    /// </summary>
    internal static List<string> GetPaths(nint itemArray, uint limit = uint.MaxValue)
    {
        var paths = new List<string>();

        using var array = Com.Wrap<IShellItemArray>(itemArray);
        if (array.Value is null)
        {
            return paths;
        }

        if (array.Value.GetCount(out var count) != Hr.Ok)
        {
            return paths;
        }

        var take = Math.Min(count, limit);

        for (uint i = 0; i < take; i++)
        {
            if (array.Value.GetItemAt(i, out var itemPointer) != Hr.Ok || itemPointer == 0)
            {
                continue;
            }

            try
            {
                using var item = Com.Wrap<IShellItem>(itemPointer);
                if (item.Value is null)
                {
                    continue;
                }

                if (item.Value.GetDisplayName(Sigdn.FileSysPath, out var namePointer) != Hr.Ok)
                {
                    continue;
                }

                var path = Com.ReadAndFreeString(namePointer);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.Release(itemPointer);
            }
        }

        return paths;
    }

    /// <summary>
    /// True when the selection contains at least one file Recode can read.
    /// </summary>
    /// <remarks>
    /// Decides whether the entry appears at all. Right clicking a folder full
    /// of text files should not offer to convert them, and every millisecond
    /// here is a millisecond before the menu appears.
    /// </remarks>
    internal static bool ContainsConvertibleFile(nint itemArray)
    {
        var table = FormatCatalogue.Table;

        foreach (var path in GetPaths(itemArray, InspectionLimit))
        {
            if (table.TryGetForPath(path, out var format) && format.CanRead)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The format shared by every convertible file in the selection, or null
    /// when they differ.
    /// </summary>
    /// <remarks>
    /// Used to leave a format out of its own submenu. With a mixed selection
    /// there is no single source format to exclude, so everything is offered.
    /// </remarks>
    internal static string? CommonSourceFormatId(nint itemArray)
    {
        var table = FormatCatalogue.Table;
        string? common = null;

        foreach (var path in GetPaths(itemArray, InspectionLimit))
        {
            if (!table.TryGetForPath(path, out var format) || !format.CanRead)
            {
                continue;
            }

            if (common is null)
            {
                common = format.Id;
            }
            else if (!string.Equals(common, format.Id, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return common;
    }
}
