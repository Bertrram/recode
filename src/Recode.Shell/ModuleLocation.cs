using System.Runtime.InteropServices;

namespace Recode.Shell;

/// <summary>
/// Finds the folder this DLL was loaded from.
/// </summary>
/// <remarks>
/// recode.exe sits next to this file, and the extension has to be able to start
/// it. Neither of the usual answers works here. AppContext.BaseDirectory gives
/// the directory of the host process, which is the COM surrogate in
/// System32. Assembly location is empty in an ahead of time compiled build.
///
/// So the module handle is looked up from the address of a function inside this
/// DLL, which is the one thing that is always true about where the code lives.
/// </remarks>
internal static class ModuleLocation
{
    private const uint FromAddress = 0x00000004;
    private const uint UnchangedRefCount = 0x00000002;

    private static string? _directory;

    internal static string Directory => _directory ??= Resolve();

    private static unsafe string Resolve()
    {
        delegate* unmanaged<void> anchor = &Anchor;

        if (!GetModuleHandleExW(FromAddress | UnchangedRefCount, (nint)anchor, out var module) || module == 0)
        {
            return AppContext.BaseDirectory;
        }

        var buffer = stackalloc char[32768];
        var length = GetModuleFileNameW(module, buffer, 32768);

        if (length == 0)
        {
            return AppContext.BaseDirectory;
        }

        var path = new string(buffer, 0, (int)length);
        return Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
    }

    /// <summary>
    /// Exists only so that its address can be taken. Any function in this
    /// assembly would do.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void Anchor()
    {
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetModuleHandleExW(uint flags, nint address, out nint module);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe uint GetModuleFileNameW(nint module, char* fileName, uint size);
}
