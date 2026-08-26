using Recode.Core.Abstractions;
using Recode.Core.Conversion;
using Xunit;

namespace Recode.Tests;

/// <summary>
/// EXIF orientation geometry, tested as pure arithmetic with no decoder involved.
/// </summary>
public class ExifOrientationTests
{
    /// <summary>
    /// A 2 by 3 image where every pixel is identifiable by its red channel:
    ///   1 2
    ///   3 4
    ///   5 6
    /// Asymmetric on both axes, so a mirror cannot be mistaken for a rotation.
    /// </summary>
    private static RgbaImage Numbered()
    {
        var image = RgbaImage.Allocate(2, 3);

        for (var i = 0; i < 6; i++)
        {
            image.Pixels[i * 4] = (byte)(i + 1);
            image.Pixels[i * 4 + 3] = 255;
        }

        return image;
    }

    private static int[] Values(RgbaImage image)
    {
        var result = new int[image.Width * image.Height];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = image.Pixels[i * 4];
        }
        return result;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(-3)]
    [InlineData(255)]
    public void A_missing_or_nonsense_tag_leaves_the_image_alone(int orientation)
    {
        var source = Numbered();
        var result = ExifOrientation.Apply(source, orientation);

        // A corrupt tag is not a reason to refuse a photograph.
        Assert.Same(source, result);
    }

    [Fact]
    public void Orientation_2_mirrors_horizontally()
    {
        var result = ExifOrientation.Apply(Numbered(), 2);

        Assert.Equal(2, result.Width);
        Assert.Equal(3, result.Height);
        Assert.Equal(new[] { 2, 1, 4, 3, 6, 5 }, Values(result));
    }

    [Fact]
    public void Orientation_3_rotates_by_180_degrees()
    {
        var result = ExifOrientation.Apply(Numbered(), 3);

        Assert.Equal(new[] { 6, 5, 4, 3, 2, 1 }, Values(result));
    }

    [Fact]
    public void Orientation_4_mirrors_vertically()
    {
        var result = ExifOrientation.Apply(Numbered(), 4);

        Assert.Equal(new[] { 5, 6, 3, 4, 1, 2 }, Values(result));
    }

    [Fact]
    public void Orientation_5_transposes()
    {
        var result = ExifOrientation.Apply(Numbered(), 5);

        Assert.Equal(3, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(new[] { 1, 3, 5, 2, 4, 6 }, Values(result));
    }

    [Fact]
    public void Orientation_6_rotates_a_quarter_turn_clockwise()
    {
        // The common one. A phone held upright writes 6 and expects the viewer
        // to turn the stored image clockwise.
        var result = ExifOrientation.Apply(Numbered(), 6);

        Assert.Equal(3, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(new[] { 5, 3, 1, 6, 4, 2 }, Values(result));
    }

    [Fact]
    public void Orientation_7_is_the_other_diagonal()
    {
        var result = ExifOrientation.Apply(Numbered(), 7);

        Assert.Equal(new[] { 6, 4, 2, 5, 3, 1 }, Values(result));
    }

    [Fact]
    public void Orientation_8_rotates_a_quarter_turn_anticlockwise()
    {
        var result = ExifOrientation.Apply(Numbered(), 8);

        Assert.Equal(3, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(new[] { 2, 4, 6, 1, 3, 5 }, Values(result));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void The_four_diagonal_orientations_swap_width_and_height(int orientation)
    {
        Assert.True(ExifOrientation.SwapsAxes(orientation));

        var result = ExifOrientation.Apply(Numbered(), orientation);
        Assert.Equal(3, result.Width);
        Assert.Equal(2, result.Height);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void The_others_keep_the_shape(int orientation)
    {
        Assert.False(ExifOrientation.SwapsAxes(orientation));

        var result = ExifOrientation.Apply(Numbered(), orientation);
        Assert.Equal(2, result.Width);
        Assert.Equal(3, result.Height);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void No_pixel_is_lost_or_duplicated(int orientation)
    {
        var result = ExifOrientation.Apply(Numbered(), orientation);

        // Every transform is a permutation. Anything else means a rounding
        // error in the index arithmetic.
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, Values(result).OrderBy(v => v));
    }

    [Fact]
    public void All_four_channels_move_together()
    {
        var image = RgbaImage.Allocate(2, 1);
        image.Pixels[0] = 10; image.Pixels[1] = 20; image.Pixels[2] = 30; image.Pixels[3] = 40;
        image.Pixels[4] = 50; image.Pixels[5] = 60; image.Pixels[6] = 70; image.Pixels[7] = 80;

        var result = ExifOrientation.Apply(image, 2);

        Assert.Equal(new byte[] { 50, 60, 70, 80, 10, 20, 30, 40 }, result.Pixels);
    }
}
