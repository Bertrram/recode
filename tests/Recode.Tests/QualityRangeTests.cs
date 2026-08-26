using Recode.Core.Conversion;
using Recode.Core.Formats;
using Xunit;

namespace Recode.Tests;

public class QualityRangeTests
{
    private static FormatTable Table => FormatTableLoader.LoadEmbedded();

    private static ImageFormat Format(string extension)
    {
        Assert.True(Table.TryGetByExtension(extension, out var format));
        return format!;
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(int.MinValue, 1)]
    [InlineData(101, 100)]
    [InlineData(500, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Values_outside_the_range_are_pulled_to_the_nearest_end(int input, int expected)
    {
        Assert.Equal(expected, QualityRange.Clamp(input));
    }

    [Fact]
    public void A_requested_quality_is_used_for_formats_that_have_one()
    {
        Assert.Equal(72, QualityRange.Resolve(72, Format(".jpg")));
        Assert.Equal(72, QualityRange.Resolve(72, Format(".webp")));
        Assert.Equal(72, QualityRange.Resolve(72, Format(".heic")));
        Assert.Equal(72, QualityRange.Resolve(72, Format(".avif")));
    }

    [Fact]
    public void An_out_of_range_request_is_clamped_rather_than_refused()
    {
        // Someone typing --quality 150 wants the best the format offers.
        // Refusing the whole batch over it would help nobody.
        Assert.Equal(100, QualityRange.Resolve(150, Format(".jpg")));
        Assert.Equal(1, QualityRange.Resolve(0, Format(".jpg")));
    }

    [Fact]
    public void Formats_without_quality_ignore_the_request()
    {
        var png = Format(".png");
        Assert.False(png.SupportsQuality);

        // PNG is lossless. Asking for quality 20 must not produce a worse PNG.
        Assert.Equal(QualityRange.Resolve(null, png), QualityRange.Resolve(20, png));
    }

    [Fact]
    public void No_request_falls_back_to_the_format_default()
    {
        var jpeg = Format(".jpg");
        Assert.Equal(jpeg.DefaultQuality, QualityRange.Resolve(null, jpeg));
    }

    [Fact]
    public void Every_default_in_the_table_is_inside_the_range()
    {
        foreach (var format in Table.Formats)
        {
            var resolved = QualityRange.Resolve(null, format);
            Assert.InRange(resolved, QualityRange.Min, QualityRange.Max);
        }
    }

    [Fact]
    public void A_null_format_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => QualityRange.Resolve(50, null!));
    }
}
