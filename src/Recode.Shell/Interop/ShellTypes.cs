namespace Recode.Shell.Interop;

/// <summary>
/// HRESULT values used by this extension.
/// </summary>
internal static class Hr
{
    public const int Ok = 0;
    public const int False = 1;
    public const int NoInterface = unchecked((int)0x80004002);
    public const int Fail = unchecked((int)0x80004005);
    public const int InvalidArg = unchecked((int)0x80070057);
    public const int OutOfMemory = unchecked((int)0x8007000E);
    public const int ClassNotAvailable = unchecked((int)0x80040111);
    public const int NoAggregation = unchecked((int)0x80040110);
}

/// <summary>
/// EXPCMDSTATE. Whether an entry is shown, greyed out or hidden.
/// </summary>
[Flags]
internal enum ExpCmdState : uint
{
    Enabled = 0x00,
    Disabled = 0x01,
    Hidden = 0x02,
    CheckBox = 0x04,
    Checked = 0x08,
    Radio = 0x10
}

/// <summary>
/// EXPCMDFLAGS. Describes the shape of an entry to Explorer.
/// </summary>
[Flags]
internal enum ExpCmdFlags : uint
{
    Default = 0x00,

    /// <summary>The entry opens a submenu, which Explorer fetches with EnumSubCommands.</summary>
    HasSubCommands = 0x01,

    HasSplitButton = 0x02,
    HideLabel = 0x04,

    /// <summary>Draw a line instead of an entry.</summary>
    IsSeparator = 0x08,

    HasLucbUi = 0x10,
    SeparatorBefore = 0x20,
    SeparatorAfter = 0x40,
    IsDropDown = 0x80
}

/// <summary>
/// SIGDN. Which form of a shell item's name to ask for.
/// </summary>
internal enum Sigdn : uint
{
    /// <summary>The full path, which is what a command line needs.</summary>
    FileSysPath = 0x80058000,

    NormalDisplay = 0x00000000,
    Url = 0x80068000
}
