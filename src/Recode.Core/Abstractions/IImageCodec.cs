using Recode.Core.Formats;

namespace Recode.Core.Abstractions;

/// <summary>
/// One decoding and encoding backend.
/// </summary>
/// <remarks>
/// WIC, libheif and libwebp all sit behind this. The conversion logic holds a
/// decoder and an encoder and never learns which is which, so adding a backend
/// means adding an implementation and a row in formats.json, and touching
/// nothing else.
///
/// Implementations are responsible for format specific correctness that cannot
/// be expressed in RGBA. In particular a decoder must apply EXIF orientation
/// itself, so that callers always receive pixels the right way up.
/// </remarks>
public interface IImageCodec
{
    /// <summary>Matches <see cref="BackendDefinition.Id"/>.</summary>
    string BackendId { get; }

    bool CanDecode(ImageFormat format);

    bool CanEncode(ImageFormat format);

    /// <summary>
    /// Decodes a file into RGBA, with EXIF orientation already applied.
    /// </summary>
    /// <exception cref="CodecException">
    /// The file could not be decoded, or the backend is unavailable.
    /// </exception>
    RgbaImage Decode(string path, ImageFormat format);

    /// <summary>
    /// Encodes RGBA into a file. The caller has already flattened transparency
    /// if the target format cannot carry it.
    /// </summary>
    /// <exception cref="CodecException">
    /// The image could not be encoded, or the backend is unavailable.
    /// </exception>
    void Encode(RgbaImage image, ImageFormat format, string path, EncodeOptions options);

    /// <summary>
    /// Reports whether the backend can run right now. Never throws, so that the
    /// support window can be drawn even when everything is broken.
    /// </summary>
    BackendAvailability CheckAvailability();
}
