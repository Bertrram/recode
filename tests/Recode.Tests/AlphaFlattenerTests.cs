using Recode.Core.Abstractions;
using Recode.Core.Conversion;
using Xunit;

namespace Recode.Tests;

/// <summary>
/// Compositing transparency onto a background, for targets with no alpha channel.
/// </summary>
public class AlphaFlattenerTests
{
    private static RgbaImage Single(byte r, byte g, byte b, byte a)
    {
        var image = RgbaImage.Allocate(1, 1);
        image.Pixels[0] = r;
        image.Pixels[1] = g;
        image.Pixels[2] = b;
        image.Pixels[3] = a;
        return image;
    }

    [Fact]
    public void A_fully_transparent_pixel_becomes_the_background()
    {
        // The case that matters. Under a transparent pixel the colour channels
        // are usually zero, so skipping this step turns transparency into black.
        var image = Single(0, 0, 0, 0);

        AlphaFlattener.FlattenOntoWhite(image);

        Assert.Equal(new byte[] { 255, 255, 255, 255 }, image.Pixels);
    }

    [Fact]
    public void An_opaque_pixel_is_untouched()
    {
        var image = Single(12, 34, 56, 255);

        AlphaFlattener.FlattenOntoWhite(image);

        Assert.Equal(new byte[] { 12, 34, 56, 255 }, image.Pixels);
    }

    [Fact]
    public void A_half_transparent_black_pixel_lands_halfway_to_white()
    {
        var image = Single(0, 0, 0, 128);

        AlphaFlattener.FlattenOntoWhite(image);

        // 255 * (255 - 128) / 255 = 127
        Assert.Equal(127, image.Pixels[0]);
        Assert.Equal(127, image.Pixels[1]);
        Assert.Equal(127, image.Pixels[2]);
        Assert.Equal(255, image.Pixels[3]);
    }

    [Fact]
    public void Everything_ends_up_opaque()
    {
        var image = RgbaImage.Allocate(4, 4);
        for (var i = 0; i < 16; i++)
        {
            image.Pixels[i * 4 + 3] = (byte)(i * 17);
        }

        AlphaFlattener.FlattenOntoWhite(image);

        Assert.False(image.HasTransparency());
    }

    [Fact]
    public void The_background_colour_is_configurable()
    {
        var image = Single(0, 0, 0, 0);

        AlphaFlattener.Flatten(image, (10, 20, 30));

        Assert.Equal(new byte[] { 10, 20, 30, 255 }, image.Pixels);
    }

    [Fact]
    public void Flattening_twice_changes_nothing_the_second_time()
    {
        // Rounding that drifts would show up here as a slow slide towards the
        // background colour.
        var image = Single(90, 140, 200, 77);

        AlphaFlattener.FlattenOntoWhite(image);
        var once = (byte[])image.Pixels.Clone();

        AlphaFlattener.FlattenOntoWhite(image);

        Assert.Equal(once, image.Pixels);
    }

    [Fact]
    public void Mid_grey_at_half_alpha_does_not_drift_darker()
    {
        // With truncation instead of rounding this comes out a level low, and
        // the error compounds across a batch of conversions.
        var image = Single(128, 128, 128, 128);

        AlphaFlattener.FlattenOntoWhite(image);

        Assert.Equal(191, image.Pixels[0]);
    }

    [Fact]
    public void Transparency_detection_finds_a_single_soft_pixel()
    {
        var image = RgbaImage.Allocate(8, 8);
        for (var i = 3; i < image.Pixels.Length; i += 4)
        {
            image.Pixels[i] = 255;
        }

        Assert.False(image.HasTransparency());

        image.Pixels[4 * 4 + 3] = 254;
        Assert.True(image.HasTransparency());
    }

    [Fact]
    public void A_null_image_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => AlphaFlattener.FlattenOntoWhite(null!));
    }
}
