using Recode.Shell.Interop;

namespace Recode.Shell.Commands;

/// <summary>
/// Shared behaviour for the entries this extension contributes.
/// </summary>
/// <remarks>
/// Nothing here throws. Every method is called by Explorer while a menu is
/// being drawn, and an exception crossing back into the shell is how a context
/// menu extension takes Explorer down with it.
/// </remarks>
internal abstract class CommandBase
{
    protected abstract string Title { get; }

    protected virtual Guid CanonicalName => Guid.Empty;

    protected virtual ExpCmdFlags Flags => ExpCmdFlags.Default;

    public int GetTitle(nint items, out nint title)
    {
        try
        {
            title = Com.AllocString(Title);
            return title == 0 ? Hr.OutOfMemory : Hr.Ok;
        }
        catch (Exception)
        {
            title = 0;
            return Hr.Fail;
        }
    }

    /// <summary>
    /// Points at recode.exe, so every entry carries the application icon.
    /// </summary>
    public int GetIcon(nint items, out nint resourceString)
    {
        try
        {
            if (!Launcher.IsAvailable)
            {
                resourceString = 0;
                return Hr.False;
            }

            resourceString = Com.AllocString($"{Launcher.ExecutablePath},0");
            return resourceString == 0 ? Hr.OutOfMemory : Hr.Ok;
        }
        catch (Exception)
        {
            resourceString = 0;
            return Hr.False;
        }
    }

    public int GetToolTip(nint items, out nint infotip)
    {
        // Explorer draws no tooltip for context menu entries, and returning
        // E_NOTIMPL is how you say so.
        infotip = 0;
        return unchecked((int)0x80004001);
    }

    public int GetCanonicalName(out Guid guid)
    {
        guid = CanonicalName;
        return Hr.Ok;
    }

    public int GetFlags(out ExpCmdFlags flags)
    {
        flags = Flags;
        return Hr.Ok;
    }

    public virtual int GetState(nint items, bool okToBeSlow, out ExpCmdState state)
    {
        state = ExpCmdState.Enabled;
        return Hr.Ok;
    }

    public virtual int EnumSubCommands(out nint commandEnum)
    {
        commandEnum = 0;
        return unchecked((int)0x80004001);
    }

    public abstract int Invoke(nint items, nint bindContext);
}
