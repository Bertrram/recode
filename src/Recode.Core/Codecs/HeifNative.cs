using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Recode.Core.Native;

namespace Recode.Core.Codecs;

/// <summary>
/// heif_error as returned by value from most libheif entry points.
/// </summary>
/// <remarks>
/// Sixteen bytes and blittable, so the runtime returns it the way the platform
/// ABI requires on both x64 and ARM64 without any marshalling help.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct HeifError
{
    public int Code;
    public int SubCode;
    public nint Message;

    public readonly bool IsOk => Code == HeifNative.ErrorOk;

    public readonly string Describe()
    {
        var text = Message != 0 ? Marshal.PtrToStringAnsi(Message) : null;
        return string.IsNullOrWhiteSpace(text) ? $"libheif error {Code}.{SubCode}" : text;
    }
}

/// <summary>
/// The writer libheif calls back into while serialising a file.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct HeifWriter
{
    public int WriterApiVersion;
    public delegate* unmanaged[Cdecl]<nint, void*, nuint, void*, HeifError> Write;
}

/// <summary>
/// The libheif entry points Recode uses, for HEIC, HEIF and AVIF.
/// </summary>
/// <remarks>
/// One backend covers three formats because libheif handles the ISOBMFF
/// container for all of them and only the compression differs: HEVC through
/// kvazaar and libde265, AV1 through libaom. Routing AVIF through libheif
/// rather than through libavif keeps this file as the only place in the project
/// that has to track a C struct layout.
///
/// Files are read from memory and written through a callback rather than by
/// handing libheif a path. libheif takes paths as char*, which on Windows goes
/// through the ANSI code page, so a path containing characters outside it would
/// fail. Keeping all file access on the .NET side sidesteps that entirely.
/// </remarks>
internal sealed unsafe class HeifNative
{
    public const string LibraryName = "heif.dll";

    // heif_error_code
    public const int ErrorOk = 0;

    // heif_colorspace
    public const int ColorspaceRgb = 1;

    // heif_chroma
    public const int ChromaInterleavedRgb = 10;
    public const int ChromaInterleavedRgba = 11;

    // heif_channel
    public const int ChannelInterleaved = 10;

    // heif_compression_format
    public const int CompressionHevc = 1;
    public const int CompressionAv1 = 4;

    private static readonly nint WriteFailureMessage =
        Marshal.StringToHGlobalAnsi("The output stream rejected the data written by libheif.");

    private readonly delegate* unmanaged[Cdecl]<nint> _contextAlloc;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _contextFree;
    private readonly delegate* unmanaged[Cdecl]<nint, void*, nuint, nint, HeifError> _readFromMemory;
    private readonly delegate* unmanaged[Cdecl]<nint, nint*, HeifError> _getPrimaryImageHandle;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _imageHandleRelease;
    private readonly delegate* unmanaged[Cdecl]<nint, nint*, int, int, nint, HeifError> _decodeImage;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _imageRelease;
    private readonly delegate* unmanaged[Cdecl]<nint, int, int> _imageGetWidth;
    private readonly delegate* unmanaged[Cdecl]<nint, int, int> _imageGetHeight;
    private readonly delegate* unmanaged[Cdecl]<nint, int, int*, byte*> _imageGetPlaneReadonly;
    private readonly delegate* unmanaged[Cdecl]<nint, int, int*, byte*> _imageGetPlane;
    private readonly delegate* unmanaged[Cdecl]<int, int, int, int, nint*, HeifError> _imageCreate;
    private readonly delegate* unmanaged[Cdecl]<nint, int, int, int, int, HeifError> _imageAddPlane;
    private readonly delegate* unmanaged[Cdecl]<nint, int, nint*, HeifError> _getEncoderForFormat;
    private readonly delegate* unmanaged[Cdecl]<nint, void> _encoderRelease;
    private readonly delegate* unmanaged[Cdecl]<nint, int, HeifError> _encoderSetLossyQuality;
    private readonly delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint*, HeifError> _encodeImage;
    private readonly delegate* unmanaged[Cdecl]<nint, HeifWriter*, void*, HeifError> _contextWrite;
    private readonly delegate* unmanaged[Cdecl]<byte*> _getVersion;

    private HeifNative(INativeLibrary library)
    {
        Library = library;

        _contextAlloc           = (delegate* unmanaged[Cdecl]<nint>)library.GetExport("heif_context_alloc");
        _contextFree            = (delegate* unmanaged[Cdecl]<nint, void>)library.GetExport("heif_context_free");
        _readFromMemory         = (delegate* unmanaged[Cdecl]<nint, void*, nuint, nint, HeifError>)library.GetExport("heif_context_read_from_memory_without_copy");
        _getPrimaryImageHandle  = (delegate* unmanaged[Cdecl]<nint, nint*, HeifError>)library.GetExport("heif_context_get_primary_image_handle");
        _imageHandleRelease     = (delegate* unmanaged[Cdecl]<nint, void>)library.GetExport("heif_image_handle_release");
        _decodeImage            = (delegate* unmanaged[Cdecl]<nint, nint*, int, int, nint, HeifError>)library.GetExport("heif_decode_image");
        _imageRelease           = (delegate* unmanaged[Cdecl]<nint, void>)library.GetExport("heif_image_release");
        _imageGetWidth          = (delegate* unmanaged[Cdecl]<nint, int, int>)library.GetExport("heif_image_get_width");
        _imageGetHeight         = (delegate* unmanaged[Cdecl]<nint, int, int>)library.GetExport("heif_image_get_height");
        _imageGetPlaneReadonly  = (delegate* unmanaged[Cdecl]<nint, int, int*, byte*>)library.GetExport("heif_image_get_plane_readonly");
        _imageGetPlane          = (delegate* unmanaged[Cdecl]<nint, int, int*, byte*>)library.GetExport("heif_image_get_plane");
        _imageCreate            = (delegate* unmanaged[Cdecl]<int, int, int, int, nint*, HeifError>)library.GetExport("heif_image_create");
        _imageAddPlane          = (delegate* unmanaged[Cdecl]<nint, int, int, int, int, HeifError>)library.GetExport("heif_image_add_plane");
        _getEncoderForFormat    = (delegate* unmanaged[Cdecl]<nint, int, nint*, HeifError>)library.GetExport("heif_context_get_encoder_for_format");
        _encoderRelease         = (delegate* unmanaged[Cdecl]<nint, void>)library.GetExport("heif_encoder_release");
        _encoderSetLossyQuality = (delegate* unmanaged[Cdecl]<nint, int, HeifError>)library.GetExport("heif_encoder_set_lossy_quality");
        _encodeImage            = (delegate* unmanaged[Cdecl]<nint, nint, nint, nint, nint*, HeifError>)library.GetExport("heif_context_encode_image");
        _contextWrite           = (delegate* unmanaged[Cdecl]<nint, HeifWriter*, void*, HeifError>)library.GetExport("heif_context_write");
        _getVersion             = (delegate* unmanaged[Cdecl]<byte*>)library.GetExport("heif_get_version");
    }

    public INativeLibrary Library { get; }

    public static bool TryCreate(INativeLibraryLoader loader, out HeifNative? native, out string? error)
    {
        native = null;

        if (!loader.TryLoad(LibraryName, out var library, out error) || library is null)
        {
            return false;
        }

        try
        {
            native = new HeifNative(library);
            return true;
        }
        catch (NativeLibraryException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public string GetVersion()
    {
        var pointer = _getVersion();
        return pointer is null ? "unknown" : Marshal.PtrToStringAnsi((nint)pointer) ?? "unknown";
    }

    public nint ContextAlloc() => _contextAlloc();

    public void ContextFree(nint context) => _contextFree(context);

    public HeifError ReadFromMemory(nint context, void* data, nuint size) =>
        _readFromMemory(context, data, size, 0);

    public HeifError GetPrimaryImageHandle(nint context, out nint handle)
    {
        nint result;
        var error = _getPrimaryImageHandle(context, &result);
        handle = result;
        return error;
    }

    public void ImageHandleRelease(nint handle) => _imageHandleRelease(handle);

    public HeifError DecodeImage(nint handle, out nint image, int colorspace, int chroma)
    {
        nint result;
        var error = _decodeImage(handle, &result, colorspace, chroma, 0);
        image = result;
        return error;
    }

    public void ImageRelease(nint image) => _imageRelease(image);

    public int GetWidth(nint image, int channel) => _imageGetWidth(image, channel);

    public int GetHeight(nint image, int channel) => _imageGetHeight(image, channel);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte* GetPlaneReadonly(nint image, int channel, out int stride)
    {
        int result;
        var plane = _imageGetPlaneReadonly(image, channel, &result);
        stride = result;
        return plane;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte* GetPlane(nint image, int channel, out int stride)
    {
        int result;
        var plane = _imageGetPlane(image, channel, &result);
        stride = result;
        return plane;
    }

    public HeifError ImageCreate(int width, int height, int colorspace, int chroma, out nint image)
    {
        nint result;
        var error = _imageCreate(width, height, colorspace, chroma, &result);
        image = result;
        return error;
    }

    public HeifError ImageAddPlane(nint image, int channel, int width, int height, int bitDepth) =>
        _imageAddPlane(image, channel, width, height, bitDepth);

    public HeifError GetEncoder(nint context, int compression, out nint encoder)
    {
        nint result;
        var error = _getEncoderForFormat(context, compression, &result);
        encoder = result;
        return error;
    }

    public void EncoderRelease(nint encoder) => _encoderRelease(encoder);

    public HeifError SetLossyQuality(nint encoder, int quality) => _encoderSetLossyQuality(encoder, quality);

    /// <summary>Encodes into the context. The resulting handle is not needed, so none is requested.</summary>
    public HeifError EncodeImage(nint context, nint image, nint encoder) =>
        _encodeImage(context, image, encoder, 0, null);

    /// <summary>
    /// Serialises the context into a managed stream.
    /// </summary>
    public HeifError Write(nint context, Stream destination)
    {
        var handle = GCHandle.Alloc(destination);
        try
        {
            var writer = new HeifWriter
            {
                WriterApiVersion = 1,
                Write = &WriteChunk
            };

            return _contextWrite(context, &writer, (void*)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static HeifError WriteChunk(nint context, void* data, nuint size, void* userData)
    {
        try
        {
            var handle = GCHandle.FromIntPtr((nint)userData);
            if (handle.Target is not Stream stream)
            {
                return new HeifError { Code = 6, SubCode = 0, Message = WriteFailureMessage };
            }

            stream.Write(new ReadOnlySpan<byte>(data, checked((int)size)));
            return default;
        }
        catch (Exception)
        {
            // Nothing may escape into native code. The error is carried back as
            // a heif_error and turned into a CodecException on the way out.
            return new HeifError { Code = 6, SubCode = 0, Message = WriteFailureMessage };
        }
    }
}
