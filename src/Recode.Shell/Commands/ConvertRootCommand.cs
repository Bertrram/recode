using System.Runtime.InteropServices.Marshalling;
using Recode.Shell.Interop;

namespace Recode.Shell.Commands;

/// <summary>
/// The "Convert to" entry, which Explorer instantiates for every right click.
/// </summary>
/// <remarks>
/// This is the class named by the CLSID in AppxManifest.xml, and the only one
/// Explorer creates directly. Everything below it comes from EnumSubCommands.
/// </remarks>
[GeneratedComClass]
internal sealed partial class ConvertRootCommand : CommandBase, IExplorerCommand
{
    protected override string Title => "Convert to";

    protected override Guid CanonicalName => ShellIds.ConvertCanonicalName;

    protected override ExpCmdFlags Flags => ExpCmdFlags.HasSubCommands;

    /// <summary>
    /// Decides whether the entry appears at all.
    /// </summary>
    /// <remarks>
    /// Hidden rather than disabled when there is nothing to convert. A greyed
    /// out entry in a menu this crowded is noise, and Windows 11 users already
    /// complain the menu is too long.
    /// </remarks>
    public override int GetState(nint items, bool okToBeSlow, out ExpCmdState state)
    {
        state = ExpCmdState.Hidden;

        try
        {
            if (!Launcher.IsAvailable || !FormatCatalogue.IsUsable)
            {
                return Hr.Ok;
            }

            if (items == 0)
            {
                return Hr.Ok;
            }

            if (ShellSelection.ContainsConvertibleFile(items))
            {
                state = ExpCmdState.Enabled;
            }

            return Hr.Ok;
        }
        catch (Exception)
        {
            state = ExpCmdState.Hidden;
            return Hr.Ok;
        }
    }

    public override int EnumSubCommands(out nint commandEnum)
    {
        commandEnum = 0;

        try
        {
            var commands = SubCommandBuilder.Build(sourceFormatId: null);

            var enumerator = new SubCommandEnumerator(commands);
            commandEnum = Com.CreateInterface(enumerator, ShellIds.IEnumExplorerCommand);

            return commandEnum == 0 ? Hr.Fail : Hr.Ok;
        }
        catch (Exception)
        {
            commandEnum = 0;
            return Hr.Fail;
        }
    }

    /// <summary>
    /// Never called. Explorer opens the submenu instead of invoking a command
    /// that declares HasSubCommands.
    /// </summary>
    public override int Invoke(nint items, nint bindContext) => Hr.Ok;
}
