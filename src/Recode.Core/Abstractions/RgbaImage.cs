namespace Recode.Core.Abstractions;

/// <summary>
/// A decoded image in straight, non premultiplied RGBA, eight bits per channel.
/// </summary>
/// <remarks>
/// This is the only pixel format that crosses a backend boundary. Every codec
/// decodes into it and encodes out of it, which is what lets the conversion
/// logic stay ignorant of which library is doing the work.
///
/// Deliberately a flat byte array rather than something streamed. Recode holds
/// one whole image in memory at a time, which is fine for photographs and is
/// noted as a limitation for very large scans.
/// </remarks>
public sealed class RgbaImage
{
    public RgbaImage(int width, int height, byte[] pixels)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        ArgumentNullException.ThrowIfNull(pixels);

        var expected = (long)width * height * 4;
        if (pixels.LongLength < expected)
        {
            throw new ArgumentException(
                $"Pixel buffer holds {pixels.LongLength} bytes but {width}x{height} RGBA needs {expected}.",
                nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Row major RGBA, four bytes per pixel, no row padding.</summary>
    public byte[] Pixels { get; }

    public int Stride => Width * 4;

    public static RgbaImage Allocate(int width, int height) =>
        new(width, height, new byte[checked((long)width * height * 4)]);

    /// <summary>
    /// True when at least one pixel is not fully opaque. Walks the buffer, so
    /// call it once and keep the answer.
    /// </summary>
    public bool HasTransparency()
    {
        for (var i = 3; i < Pixels.Length; i += 4)
        {
            if (Pixels[i] != 255)
            {
                return true;
            }
        }
        return false;
    }
}
