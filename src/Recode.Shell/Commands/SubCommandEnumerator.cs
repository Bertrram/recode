using System.Runtime.InteropServices.Marshalling;
using Recode.Shell.Interop;

namespace Recode.Shell.Commands;

/// <summary>
/// Builds the list of entries in the "Convert to" submenu.
/// </summary>
internal static class SubCommandBuilder
{
    /// <summary>
    /// Every writable format, plus the support entry.
    /// </summary>
    /// <param name="sourceFormatId">
    /// Left over from the registry menu, where the source format is excluded
    /// when the menu is generated. Here the submenu is built once and each
    /// entry hides itself, because Explorer caches the enumeration but asks for
    /// state on every showing. Passing null builds the full list.
    /// </param>
    internal static List<CommandBase> Build(string? sourceFormatId)
    {
        var commands = new List<CommandBase>();
        var table = FormatCatalogue.Table;

        foreach (var target in table.WritableTargets)
        {
            if (sourceFormatId is not null &&
                string.Equals(target.Format.Id, sourceFormatId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            commands.Add(new ConvertTargetCommand(target));
        }

        commands.Add(new SupportCommand());
        return commands;
    }
}

/// <summary>
/// Hands the submenu entries to Explorer one batch at a time.
/// </summary>
[GeneratedComClass]
internal sealed partial class SubCommandEnumerator : IEnumExplorerCommand
{
    private readonly List<CommandBase> _commands;
    private int _index;

    internal SubCommandEnumerator(List<CommandBase> commands, int startIndex = 0)
    {
        _commands = commands;
        _index = startIndex;
    }

    public unsafe int Next(uint count, nint commands, nint fetched)
    {
        var written = 0u;

        try
        {
            if (commands == 0)
            {
                return Hr.InvalidArg;
            }

            var slots = (nint*)commands;

            while (written < count && _index < _commands.Count)
            {
                var pointer = Com.CreateInterface(_commands[_index], ShellIds.IExplorerCommand);

                if (pointer == 0)
                {
                    // Release whatever was handed out before giving up, so the
                    // failure does not also leak.
                    for (var i = 0u; i < written; i++)
                    {
                        System.Runtime.InteropServices.Marshal.Release(slots[i]);
                        slots[i] = 0;
                    }

                    WriteFetched(fetched, 0);
                    return Hr.Fail;
                }

                slots[written] = pointer;
                written++;
                _index++;
            }

            WriteFetched(fetched, written);

            // S_FALSE means fewer items were returned than asked for, which is
            // how the enumeration ends.
            return written == count ? Hr.Ok : Hr.False;
        }
        catch (Exception)
        {
            WriteFetched(fetched, written);
            return Hr.Fail;
        }
    }

    public int Skip(uint count)
    {
        _index = (int)Math.Min(_index + (long)count, _commands.Count);
        return Hr.Ok;
    }

    public int Reset()
    {
        _index = 0;
        return Hr.Ok;
    }

    public int Clone(out nint copy)
    {
        copy = 0;

        try
        {
            var clone = new SubCommandEnumerator(_commands, _index);
            copy = Com.CreateInterface(clone, ShellIds.IEnumExplorerCommand);
            return copy == 0 ? Hr.Fail : Hr.Ok;
        }
        catch (Exception)
        {
            copy = 0;
            return Hr.Fail;
        }
    }

    /// <summary>
    /// Writes the count only when the caller supplied somewhere to put it.
    /// Asking for a single item and passing null is allowed, and Explorer does.
    /// </summary>
    private static unsafe void WriteFetched(nint fetched, uint value)
    {
        if (fetched != 0)
        {
            *(uint*)fetched = value;
        }
    }
}
