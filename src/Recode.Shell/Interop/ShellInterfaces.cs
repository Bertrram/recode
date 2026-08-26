using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Recode.Shell.Interop;

/// <summary>
/// The COM interfaces the Windows 11 context menu is built from.
/// </summary>
/// <remarks>
/// Every method is declared with PreserveSig and returns an HRESULT directly.
/// The alternative, letting the marshaller turn exceptions into HRESULTs, hides
/// which value the shell actually receives, and the shell treats several of
/// these as meaningful rather than as pass or fail. GetState returning S_FALSE
/// is not the same as it returning E_FAIL.
///
/// This is also why the extension is compiled ahead of time. It is loaded by a
/// COM surrogate on every right click, where starting a runtime would be felt.
/// </remarks>
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
internal partial interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(nint outerUnknown, in Guid riid, out nint instance);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool @lock);
}

/// <summary>
/// A single entry in the context menu.
/// </summary>
[GeneratedComInterface]
[Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9")]
internal partial interface IExplorerCommand
{
    /// <summary>The label. Allocated with CoTaskMemAlloc; the shell frees it.</summary>
    [PreserveSig]
    int GetTitle(nint items, out nint title);

    /// <summary>
    /// An icon reference in the form "path,index", or S_FALSE for none.
    /// </summary>
    [PreserveSig]
    int GetIcon(nint items, out nint resourceString);

    [PreserveSig]
    int GetToolTip(nint items, out nint infotip);

    /// <summary>A stable identity for the command. Not required to be unique.</summary>
    [PreserveSig]
    int GetCanonicalName(out Guid guid);

    /// <summary>
    /// Whether to show, grey out or hide the entry. Called on every right
    /// click, so it has to be quick.
    /// </summary>
    [PreserveSig]
    int GetState(nint items, [MarshalAs(UnmanagedType.Bool)] bool okToBeSlow, out ExpCmdState state);

    /// <summary>Runs the command. The user has clicked.</summary>
    [PreserveSig]
    int Invoke(nint items, nint bindContext);

    [PreserveSig]
    int GetFlags(out ExpCmdFlags flags);

    /// <summary>
    /// Returns the submenu, or E_NOTIMPL for a leaf entry.
    /// </summary>
    [PreserveSig]
    int EnumSubCommands(out nint commandEnum);
}

/// <summary>
/// The submenu behind an entry that declares HasSubCommands.
/// </summary>
[GeneratedComInterface]
[Guid("a88826f8-186f-4987-aade-ea0cef8fbfe8")]
internal partial interface IEnumExplorerCommand
{
    /// <summary>
    /// Fills an array of IExplorerCommand pointers.
    /// </summary>
    /// <param name="fetched">
    /// A raw pointer rather than an out parameter on purpose. The IEnum
    /// contract allows a caller asking for one item to pass null here, and
    /// Explorer does. An out parameter would be written unconditionally.
    /// </param>
    [PreserveSig]
    int Next(uint count, nint commands, nint fetched);

    [PreserveSig]
    int Skip(uint count);

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int Clone(out nint copy);
}

/// <summary>
/// The files the user right clicked.
/// </summary>
[GeneratedComInterface]
[Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
internal partial interface IShellItemArray
{
    [PreserveSig]
    int BindToHandler(nint bindContext, in Guid handler, in Guid riid, out nint result);

    [PreserveSig]
    int GetPropertyStore(uint flags, in Guid riid, out nint result);

    [PreserveSig]
    int GetPropertyDescriptionList(nint keyType, in Guid riid, out nint result);

    [PreserveSig]
    int GetAttributes(uint attributeFlags, uint mask, out uint attributes);

    [PreserveSig]
    int GetCount(out uint count);

    [PreserveSig]
    int GetItemAt(uint index, out nint item);

    [PreserveSig]
    int EnumItems(out nint enumShellItems);
}

/// <summary>
/// One file.
/// </summary>
[GeneratedComInterface]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
internal partial interface IShellItem
{
    [PreserveSig]
    int BindToHandler(nint bindContext, in Guid handler, in Guid riid, out nint result);

    [PreserveSig]
    int GetParent(out nint parent);

    /// <summary>Allocated with CoTaskMemAlloc; the caller frees it.</summary>
    [PreserveSig]
    int GetDisplayName(Sigdn name, out nint displayName);

    [PreserveSig]
    int GetAttributes(uint mask, out uint attributes);

    [PreserveSig]
    int Compare(nint other, uint hint, out int order);
}
