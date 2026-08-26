using Recode.Core.Abstractions;
using Recode.Core.Codecs;
using Recode.Core.Formats;
using Recode.Core.Native;

namespace Recode.Core.Conversion;

/// <summary>
/// Holds one codec per backend and hands out the right one.
/// </summary>
/// <remarks>
/// The only place in the program that knows which concrete codec classes exist.
/// Everything downstream sees <see cref="IImageCodec"/>, which is what lets the
/// conversion logic stay unaware of whether a file is going through WIC or
/// through a bundled library.
/// </remarks>
public sealed class CodecRegistry
{
    private readonly Dictionary<string, IImageCodec> _byBackendId;

    public CodecRegistry(IEnumerable<IImageCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);

        _byBackendId = new Dictionary<string, IImageCodec>(StringComparer.OrdinalIgnoreCase);
        foreach (var codec in codecs)
        {
            if (!_byBackendId.TryAdd(codec.BackendId, codec))
            {
                throw new ArgumentException($"Two codecs claim backend '{codec.BackendId}'.", nameof(codecs));
            }
        }
    }

    /// <summary>The standard set: WIC for what Windows handles, plus the two bundled libraries.</summary>
    public static CodecRegistry CreateDefault(INativeLibraryLoader? loader = null)
    {
        loader ??= new NativeLibraryLoader();

        return new CodecRegistry(new IImageCodec[]
        {
            new WicCodec(),
            new HeifCodec(loader),
            new WebpCodec(loader)
        });
    }

    public IEnumerable<IImageCodec> Codecs => _byBackendId.Values;

    public IImageCodec GetForBackend(string backendId)
    {
        if (_byBackendId.TryGetValue(backendId, out var codec))
        {
            return codec;
        }

        throw new ConversionNotSupportedException(
            $"No codec is registered for backend '{backendId}'. formats.json names a backend this build does not have.");
    }

    public IImageCodec GetDecoder(ImageFormat format)
    {
        var codec = GetForBackend(format.BackendId);
        if (!codec.CanDecode(format))
        {
            throw new ConversionNotSupportedException($"The {codec.BackendId} backend cannot read {format.DisplayName}.");
        }
        return codec;
    }

    public IImageCodec GetEncoder(ImageFormat format)
    {
        var codec = GetForBackend(format.BackendId);
        if (!codec.CanEncode(format))
        {
            throw new ConversionNotSupportedException($"The {codec.BackendId} backend cannot write {format.DisplayName}.");
        }
        return codec;
    }
}
