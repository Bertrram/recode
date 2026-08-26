using System.Runtime.CompilerServices;
using Recode.Core.Native;

namespace Recode.Core.Codecs;

/// <summary>
/// The handful of libwebp entry points Recode uses.
/// </summary>
/// <remarks>
/// libwebp has a simple C API with a stable ABI, and no structs are passed
/// across the boundary, so this binding is a thin one. Everything is resolved
/// through <see cref="INativeLibrary"/> rather than [DllImport] so that a
/// missing DLL surfaces as a message instead of an unhandled exception at the
/// first conversion.
/// </remarks>
internal sealed unsafe class WebpNative
{
    public const string LibraryName = "libwebp.dll";

    private readonly delegate* unmanaged[Cdecl]<byte*, nuint, int*, int*, int> _getInfo;
    private readonly delegate* unmanaged[Cdecl]<byte*, nuint, int*, int*, byte*> _decodeRgba;
    private readonly delegate* unmanaged[Cdecl]<byte*, int, int, int, float, byte**, nuint> _encodeRgba;
    private readonly delegate* unmanaged[Cdecl]<byte*, int, int, int, byte**, nuint> _encodeLosslessRgba;
    private readonly delegate* unmanaged[Cdecl]<void*, void> _free;
    private readonly delegate* unmanaged[Cdecl]<int> _decoderVersion;

    private WebpNative(INativeLibrary library)
    {
        Library = library;

        _getInfo            = (delegate* unmanaged[Cdecl]<byte*, nuint, int*, int*, int>)library.GetExport("WebPGetInfo");
        _decodeRgba         = (delegate* unmanaged[Cdecl]<byte*, nuint, int*, int*, byte*>)library.GetExport("WebPDecodeRGBA");
        _encodeRgba         = (delegate* unmanaged[Cdecl]<byte*, int, int, int, float, byte**, nuint>)library.GetExport("WebPEncodeRGBA");
        _encodeLosslessRgba = (delegate* unmanaged[Cdecl]<byte*, int, int, int, byte**, nuint>)library.GetExport("WebPEncodeLosslessRGBA");
        _free               = (delegate* unmanaged[Cdecl]<void*, void>)library.GetExport("WebPFree");
        _decoderVersion     = (delegate* unmanaged[Cdecl]<int>)library.GetExport("WebPGetDecoderVersion");
    }

    public INativeLibrary Library { get; }

    /// <summary>
    /// Loads libwebp and resolves its exports, or explains why it could not.
    /// </summary>
    public static bool TryCreate(INativeLibraryLoader loader, out WebpNative? native, out string? error)
    {
        native = null;

        if (!loader.TryLoad(LibraryName, out var library, out error) || library is null)
        {
            return false;
        }

        try
        {
            native = new WebpNative(library);
            return true;
        }
        catch (NativeLibraryException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Version as major.minor.patch, for the support window.</summary>
    public string GetVersion()
    {
        var packed = _decoderVersion();
        return $"{(packed >> 16) & 0xFF}.{(packed >> 8) & 0xFF}.{packed & 0xFF}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetInfo(byte* data, nuint size, out int width, out int height)
    {
        int w, h;
        var ok = _getInfo(data, size, &w, &h) != 0;
        width = w;
        height = h;
        return ok;
    }

    public byte* DecodeRgba(byte* data, nuint size, out int width, out int height)
    {
        int w, h;
        var result = _decodeRgba(data, size, &w, &h);
        width = w;
        height = h;
        return result;
    }

    public nuint EncodeRgba(byte* rgba, int width, int height, int stride, float quality, byte** output) =>
        _encodeRgba(rgba, width, height, stride, quality, output);

    public nuint EncodeLosslessRgba(byte* rgba, int width, int height, int stride, byte** output) =>
        _encodeLosslessRgba(rgba, width, height, stride, output);

    public void Free(void* pointer) => _free(pointer);
}
