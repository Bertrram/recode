using Recode.Core.Conversion;
using Recode.Core.Formats;
using Xunit;

namespace Recode.Tests;

/// <summary>
/// Which backend handles which end of a conversion.
/// </summary>
public class BackendSelectorTests
{
    private static readonly FormatTable Table = FormatTableLoader.LoadEmbedded();
    private static readonly BackendSelector Selector = new(Table);

    private static ImageFormat Format(string extension)
    {
        Assert.True(Table.TryGetByExtension(extension, out var format));
        return format!;
    }

    [Theory]
    [InlineData(".jpg", ".png", "wic", "wic")]
    [InlineData(".png", ".jpg", "wic", "wic")]
    [InlineData(".heic", ".jpg", "libheif", "wic")]
    [InlineData(".jpg", ".heic", "wic", "libheif")]
    [InlineData(".png", ".avif", "wic", "libheif")]
    [InlineData(".tiff", ".webp", "wic", "libwebp")]
    [InlineData(".webp", ".png", "libwebp", "wic")]
    [InlineData(".heic", ".avif", "libheif", "libheif")]
    [InlineData(".webp", ".heic", "libwebp", "libheif")]
    [InlineData(".avif", ".webp", "libheif", "libwebp")]
    public void The_source_picks_the_decoder_and_the_target_picks_the_encoder(
        string from, string to, string expectedDecoder, string expectedEncoder)
    {
        var pair = Selector.Select(Format(from), Format(to));

        Assert.Equal(expectedDecoder, pair.DecoderBackendId);
        Assert.Equal(expectedEncoder, pair.EncoderBackendId);
    }

    [Fact]
    public void A_conversion_inside_one_backend_is_reported_as_such()
    {
        Assert.True(Selector.Select(Format(".jpg"), Format(".png")).IsSingleBackend);
        Assert.True(Selector.Select(Format(".heic"), Format(".avif")).IsSingleBackend);
        Assert.False(Selector.Select(Format(".heic"), Format(".png")).IsSingleBackend);
    }

    [Fact]
    public void Avif_and_heic_share_libheif()
    {
        // AVIF is routed through libheif with libaom rather than through a
        // second library. One interop surface instead of two.
        Assert.Equal("libheif", Format(".avif").BackendId);
        Assert.Equal("libheif", Format(".heic").BackendId);
    }

    [Fact]
    public void Every_pair_in_the_table_can_be_selected()
    {
        var readable = Table.Formats.Where(f => f.CanRead).ToList();
        var writable = Table.Formats.Where(f => f.CanWrite).ToList();

        foreach (var source in readable)
        {
            foreach (var target in writable)
            {
                var pair = Selector.Select(source, target);
                Assert.False(string.IsNullOrEmpty(pair.DecoderBackendId));
                Assert.False(string.IsNullOrEmpty(pair.EncoderBackendId));
            }
        }
    }

    [Fact]
    public void A_source_that_cannot_be_read_is_refused()
    {
        var unreadable = Format(".png") with { CanRead = false };

        var error = Assert.Throws<ConversionNotSupportedException>(
            () => Selector.Select(unreadable, Format(".jpg")));

        Assert.Contains("source", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_target_that_cannot_be_written_is_refused()
    {
        var unwritable = Format(".png") with { CanWrite = false };

        var error = Assert.Throws<ConversionNotSupportedException>(
            () => Selector.Select(Format(".jpg"), unwritable));

        Assert.Contains("target", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_format_naming_an_unknown_backend_is_refused()
    {
        var broken = Format(".png") with { BackendId = "nonexistent" };

        Assert.Throws<FormatTableException>(() => Selector.Select(broken, Format(".jpg")));
    }
}
