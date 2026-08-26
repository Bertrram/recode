using System.Runtime.InteropServices.Marshalling;
using Recode.Core.Formats;
using Recode.Shell.Interop;

namespace Recode.Shell.Commands;

/// <summary>
/// One target format in the submenu, for example "PNG".
/// </summary>
[GeneratedComClass]
internal sealed partial class ConvertTargetCommand : CommandBase, IExplorerCommand
{
    private readonly TargetFormat _target;

    internal ConvertTargetCommand(TargetFormat target)
    {
        _target = target;
    }

    protected override string Title => _target.MenuLabel;

    /// <summary>
    /// Hides the entry when the selection is entirely of this format, so a
    /// folder of PNG files does not offer to convert them to PNG.
    /// </summary>
    /// <remarks>
    /// Decided here rather than when the submenu is built, because Explorer
    /// caches the submenu but calls GetState for each entry on every showing.
    /// A mixed selection has no single source format, so nothing is hidden.
    /// </remarks>
    public override int GetState(nint items, bool okToBeSlow, out ExpCmdState state)
    {
        state = ExpCmdState.Enabled;

        try
        {
            if (items == 0)
            {
                return Hr.Ok;
            }

            var sourceFormatId = ShellSelection.CommonSourceFormatId(items);

            if (sourceFormatId is not null &&
                string.Equals(sourceFormatId, _target.Format.Id, StringComparison.OrdinalIgnoreCase))
            {
                state = ExpCmdState.Hidden;
            }

            return Hr.Ok;
        }
        catch (Exception)
        {
            state = ExpCmdState.Enabled;
            return Hr.Ok;
        }
    }

    public override int Invoke(nint items, nint bindContext)
    {
        try
        {
            var paths = ShellSelection.GetPaths(items);
            Launcher.Convert(_target.Token, paths);
            return Hr.Ok;
        }
        catch (Exception)
        {
            // A failure to start is reported by recode.exe if it got as far as
            // running. Anything before that is not worth a dialog from inside
            // the shell.
            return Hr.Ok;
        }
    }
}

/// <summary>
/// The "Format support" entry at the bottom of the submenu.
/// </summary>
[GeneratedComClass]
internal sealed partial class SupportCommand : CommandBase, IExplorerCommand
{
    protected override string Title => "Format support";

    protected override Guid CanonicalName => ShellIds.SupportCanonicalName;

    /// <summary>A rule above it, separating it from the formats.</summary>
    protected override ExpCmdFlags Flags => ExpCmdFlags.SeparatorBefore;

    public override int Invoke(nint items, nint bindContext)
    {
        try
        {
            Launcher.ShowSupportWindow();
            return Hr.Ok;
        }
        catch (Exception)
        {
            return Hr.Ok;
        }
    }
}
