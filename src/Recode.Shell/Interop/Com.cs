using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Recode.Shell.Interop;

/// <summary>
/// The small amount of COM plumbing this extension needs.
/// </summary>
/// <remarks>
/// One ComWrappers instance for the whole DLL. Creating more than one would
/// hand out different wrappers for the same native object, which is the sort of
/// thing that produces a reference counting bug six months later.
/// </remarks>
internal static class Com
{
    private static readonly StrategyBasedComWrappers Wrappers = new();

    /// <summary>
    /// Wraps a managed object as a COM pointer of the requested interface.
    /// </summary>
    /// <returns>A pointer the caller owns and must release, or zero.</returns>
    internal static nint CreateInterface(object instance, Guid iid)
    {
        var unknown = Wrappers.GetOrCreateComInterfaceForObject(instance, CreateComInterfaceFlags.None);

        try
        {
            var hr = Marshal.QueryInterface(unknown, ref iid, out var result);
            return hr == Hr.Ok ? result : 0;
        }
        finally
        {
            // CreateInterface returned a reference of its own. The caller keeps
            // the one from QueryInterface.
            Marshal.Release(unknown);
        }
    }

    /// <summary>
    /// Wraps a native pointer owned by somebody else so it can be called.
    /// </summary>
    /// <remarks>
    /// UniqueInstance rather than a cached wrapper, because these pointers come
    /// from Explorer and live only for the duration of one call. The returned
    /// handle must be disposed, which releases the reference the wrapper took.
    /// </remarks>
    internal static ComScope<T> Wrap<T>(nint pointer) where T : class
    {
        if (pointer == 0)
        {
            return default;
        }

        var wrapper = Wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
        return new ComScope<T>(wrapper as T, wrapper as IDisposable);
    }

    /// <summary>
    /// Copies a string into memory the shell will free with CoTaskMemFree.
    /// </summary>
    internal static nint AllocString(string value) => Marshal.StringToCoTaskMemUni(value);

    internal static string? ReadAndFreeString(nint pointer)
    {
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }
}

/// <summary>
/// A borrowed COM object and the wrapper that has to be released afterwards.
/// </summary>
internal readonly struct ComScope<T> : IDisposable where T : class
{
    private readonly IDisposable? _wrapper;

    internal ComScope(T? value, IDisposable? wrapper)
    {
        Value = value;
        _wrapper = wrapper;
    }

    internal T? Value { get; }

    internal bool IsValid => Value is not null;

    public void Dispose() => _wrapper?.Dispose();
}
