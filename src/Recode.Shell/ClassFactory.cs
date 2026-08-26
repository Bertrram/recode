using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Recode.Shell.Commands;
using Recode.Shell.Interop;

namespace Recode.Shell;

/// <summary>
/// Creates the root command when the shell asks for it.
/// </summary>
[GeneratedComClass]
internal sealed partial class ConvertCommandFactory : IClassFactory
{
    public int CreateInstance(nint outerUnknown, in Guid riid, out nint instance)
    {
        instance = 0;

        // Aggregation is a COM feature nothing here supports, and saying so is
        // required rather than optional.
        if (outerUnknown != 0)
        {
            return Hr.NoAggregation;
        }

        try
        {
            var command = new ConvertRootCommand();
            var iid = riid;

            instance = Com.CreateInterface(command, iid);
            return instance == 0 ? Hr.NoInterface : Hr.Ok;
        }
        catch (Exception)
        {
            instance = 0;
            return Hr.Fail;
        }
    }

    /// <summary>
    /// The surrogate decides when to unload, so there is nothing to count here.
    /// </summary>
    public int LockServer(bool @lock) => Hr.Ok;
}

/// <summary>
/// The exports that make this DLL a COM server.
/// </summary>
/// <remarks>
/// Written by hand rather than through COM hosting, because the hosting support
/// in the SDK produces a managed assembly plus a native shim and expects a
/// runtime to be present. This is compiled ahead of time into a single DLL with
/// no runtime dependency, which is what a component loaded on every right click
/// should be.
/// </remarks>
internal static class Exports
{
    [UnmanagedCallersOnly(EntryPoint = "DllGetClassObject")]
    internal static unsafe int DllGetClassObject(Guid* rclsid, Guid* riid, nint* ppv)
    {
        if (ppv is null)
        {
            return Hr.InvalidArg;
        }

        *ppv = 0;

        if (rclsid is null || riid is null)
        {
            return Hr.InvalidArg;
        }

        if (*rclsid != ShellIds.ConvertCommandClsid)
        {
            return Hr.ClassNotAvailable;
        }

        try
        {
            var factory = new ConvertCommandFactory();
            var pointer = Com.CreateInterface(factory, *riid);

            if (pointer == 0)
            {
                return Hr.NoInterface;
            }

            *ppv = pointer;
            return Hr.Ok;
        }
        catch (Exception)
        {
            return Hr.Fail;
        }
    }

    /// <summary>
    /// Always S_FALSE. Unloading a runtime out of a live surrogate buys nothing
    /// and risks a great deal.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "DllCanUnloadNow")]
    internal static int DllCanUnloadNow() => Hr.False;
}
