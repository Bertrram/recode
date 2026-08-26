using System.Windows.Media;
using System.Windows.Media.Imaging;
using Recode.Core.Conversion;
using Recode.Core.Formats;
using Xunit;

namespace Recode.Tests;

/// <summary>
/// Real files through the real codecs.
/// </summary>
/// <remarks>
/// These need the bundled libraries. Run tools/build-natives.ps1 before
/// dotnet test, or the HEIC and AVIF cases will fail with a message saying so.
/// The unit tests above need nothing bundled.
///
/// The test images are 64 by 48 and a few hundred bytes each, small enough to
/// live in the repository without apology.
/// </remarks>
public sealed class EndToEndConversionTests : IDisposable
{
    private static readonly FormatTable Table = FormatTableLoader.LoadEmbedded();

    private readonly string _workingDirectory;
    private readonly ConversionService _service;

    public EndToEndConversionTests()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), "recode-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDirectory);

        _service = new ConversionService(Table, CodecRegistry.CreateDefault());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test run over.
        }
    }

    private string CopyIn(string name)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestImages", name);
        Assert.True(File.Exists(source), $"Test image {name} is missing from the build output.");

        var destination = Path.Combine(_workingDirectory, name);
        File.Copy(source, destination, overwrite: true);
        return destination;
    }

    private static TargetFormat Target(string token)
    {
        Assert.True(Table.TryResolveTarget(token, out var target));
        return target!;
    }

    private ConversionResult Convert(string image, string to, ConversionOptions? options = null) =>
        _service.ConvertOne(CopyIn(image), Target(to), options ?? ConversionOptions.Default);

    private static void AssertSucceeded(ConversionResult result)
    {
        Assert.True(
            result.Succeeded,
            $"{Path.GetFileName(result.InputPath)} did not convert: {result.Message}");

        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath), $"{result.OutputPath} was not written.");
        Assert.True(new FileInfo(result.OutputPath!).Length > 0, "The output file is empty.");
    }

    /// <summary>Decodes a written file back, to prove it is a real image and not just bytes.</summary>
    private static (int Width, int Height, byte[] Pixels) ReadBack(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];

        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        return (converted.PixelWidth, converted.PixelHeight, pixels);
    }

    private static (byte B, byte G, byte R, byte A) PixelAt(
        (int Width, int Height, byte[] Pixels) image, int x, int y)
    {
        var index = (y * image.Width + x) * 4;
        return (image.Pixels[index], image.Pixels[index + 1], image.Pixels[index + 2], image.Pixels[index + 3]);
    }

    // -----------------------------------------------------------------------
    // The three the specification calls for
    // -----------------------------------------------------------------------

    [Fact]
    public void Jpg_to_png()
    {
        var result = Convert("sample.jpg", "png");
        AssertSucceeded(result);

        Assert.Equal(".png", Path.GetExtension(result.OutputPath));

        var image = ReadBack(result.OutputPath!);
        Assert.Equal(64, image.Width);
        Assert.Equal(48, image.Height);
    }

    [Fact]
    public void Heic_to_jpg()
    {
        var result = Convert("sample.heic", "jpg");
        AssertSucceeded(result);

        var image = ReadBack(result.OutputPath!);
        Assert.Equal(64, image.Width);
        Assert.Equal(48, image.Height);

        // The top left quadrant is red in the source. HEVC is lossy, so this
        // checks the hue survived rather than an exact value.
        var (b, g, r, _) = PixelAt(image, 8, 6);
        Assert.True(r > 150, $"Expected a red pixel, got R={r} G={g} B={b}.");
        Assert.True(r > g && r > b, $"Red is not dominant: R={r} G={g} B={b}.");
    }

    [Fact]
    public void Png_to_avif()
    {
        var result = Convert("sample-rgba.png", "avif");
        AssertSucceeded(result);

        Assert.Equal(".avif", Path.GetExtension(result.OutputPath));

        // WIC cannot read AVIF on a stock Windows install, so the file is read
        // back through the same libheif backend that wrote it.
        var registry = CodecRegistry.CreateDefault();
        Assert.True(Table.TryGetByExtension(".avif", out var avif));

        var decoded = registry.GetDecoder(avif!).Decode(result.OutputPath!, avif!);
        Assert.Equal(64, decoded.Width);
        Assert.Equal(48, decoded.Height);
    }

    // -----------------------------------------------------------------------
    // Behaviour the specification describes
    // -----------------------------------------------------------------------

    [Fact]
    public void The_original_is_never_deleted()
    {
        var input = CopyIn("sample.jpg");
        var result = _service.ConvertOne(input, Target("png"), ConversionOptions.Default);

        AssertSucceeded(result);
        Assert.True(File.Exists(input), "The source file was removed.");
    }

    [Fact]
    public void A_second_conversion_writes_a_numbered_copy()
    {
        var input = CopyIn("sample.jpg");

        var first = _service.ConvertOne(input, Target("png"), ConversionOptions.Default);
        var second = _service.ConvertOne(input, Target("png"), ConversionOptions.Default);

        AssertSucceeded(first);
        AssertSucceeded(second);

        Assert.Equal("sample.png", Path.GetFileName(first.OutputPath));
        Assert.Equal("sample (1).png", Path.GetFileName(second.OutputPath));
        Assert.True(File.Exists(first.OutputPath));
    }

    [Fact]
    public void Force_replaces_instead_of_numbering()
    {
        var input = CopyIn("sample.jpg");

        var first = _service.ConvertOne(input, Target("png"), ConversionOptions.Default);
        var second = _service.ConvertOne(input, Target("png"), new ConversionOptions(Force: true));

        AssertSucceeded(second);
        Assert.Equal(first.OutputPath, second.OutputPath);
        Assert.Single(Directory.GetFiles(_workingDirectory, "*.png"));
    }

    [Fact]
    public void Recompressing_to_the_same_format_keeps_the_original()
    {
        var input = CopyIn("sample.jpg");
        var originalLength = new FileInfo(input).Length;

        var result = _service.ConvertOne(input, Target("jpg"), new ConversionOptions(Quality: 20));

        AssertSucceeded(result);
        Assert.Equal("sample (1).jpg", Path.GetFileName(result.OutputPath));
        Assert.Equal(originalLength, new FileInfo(input).Length);
    }

    [Fact]
    public void Transparency_is_flattened_onto_white_for_jpeg()
    {
        // The bottom right quadrant of the test image is fully transparent.
        var result = Convert("sample-rgba.png", "jpg");
        AssertSucceeded(result);

        var image = ReadBack(result.OutputPath!);
        var (b, g, r, a) = PixelAt(image, 48, 36);

        Assert.Equal(255, a);
        Assert.True(r > 230 && g > 230 && b > 230, $"Expected white, got R={r} G={g} B={b}. Black here means the flattening step was skipped.");
    }

    [Fact]
    public void Transparency_survives_a_format_that_can_carry_it()
    {
        var result = Convert("sample-rgba.png", "webp");
        AssertSucceeded(result);

        var registry = CodecRegistry.CreateDefault();
        Assert.True(Table.TryGetByExtension(".webp", out var webp));

        var decoded = registry.GetDecoder(webp!).Decode(result.OutputPath!, webp!);
        Assert.True(decoded.HasTransparency(), "The alpha channel was lost.");
    }

    [Fact]
    public void Quality_changes_the_file_size()
    {
        var low = Convert("sample-rgba.png", "jpg", new ConversionOptions(Quality: 5));
        var high = Convert("sample-rgba.png", "jpg", new ConversionOptions(Quality: 100));

        AssertSucceeded(low);
        AssertSucceeded(high);

        Assert.True(
            new FileInfo(high.OutputPath!).Length > new FileInfo(low.OutputPath!).Length,
            "Quality 100 did not produce a larger file than quality 5.");
    }

    [Fact]
    public void An_undecodable_file_fails_without_taking_the_batch_down()
    {
        var broken = Path.Combine(_workingDirectory, "broken.png");
        File.WriteAllText(broken, "this is not a png");

        var good = CopyIn("sample.jpg");

        var summary = _service.Convert(
            new[] { broken, good },
            Target("webp"),
            ConversionOptions.Default);

        Assert.False(summary.AllSucceeded);
        Assert.Equal(1, summary.ConvertedCount);
        Assert.Equal(1, summary.FailedCount);

        // The good file still converted. That is the whole point.
        Assert.True(File.Exists(Path.Combine(_workingDirectory, "sample.webp")));
    }

    [Fact]
    public void A_file_that_does_not_exist_is_reported_by_name()
    {
        var result = _service.ConvertOne(
            Path.Combine(_workingDirectory, "nothing.png"),
            Target("jpg"),
            ConversionOptions.Default);

        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Contains("does not exist", result.Message);
    }

    [Fact]
    public void An_unsupported_extension_is_skipped_rather_than_failed()
    {
        var document = Path.Combine(_workingDirectory, "notes.txt");
        File.WriteAllText(document, "hello");

        var result = _service.ConvertOne(document, Target("png"), ConversionOptions.Default);

        Assert.Equal(ConversionStatus.Skipped, result.Status);
        Assert.Contains(".txt", result.Message);
    }

    [Fact]
    public void Output_directory_is_created_when_missing()
    {
        var target = Path.Combine(_workingDirectory, "converted", "deeper");
        var input = CopyIn("sample.jpg");

        var result = _service.ConvertOne(input, Target("png"), new ConversionOptions(OutputDirectory: target));

        AssertSucceeded(result);
        Assert.Equal(target, Path.GetDirectoryName(result.OutputPath));
    }

    [Fact]
    public void No_temporary_files_are_left_behind()
    {
        var result = Convert("sample.jpg", "webp");
        AssertSucceeded(result);

        Assert.Empty(Directory.GetFiles(_workingDirectory, "*.tmp"));
    }

    [Fact]
    public void A_failed_encode_leaves_no_temporary_file()
    {
        var broken = Path.Combine(_workingDirectory, "broken.heic");
        File.WriteAllText(broken, "not a heic file");

        var result = _service.ConvertOne(broken, Target("png"), ConversionOptions.Default);

        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Empty(Directory.GetFiles(_workingDirectory, "*.tmp"));
    }

    // -----------------------------------------------------------------------
    // The full matrix
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("jpg")]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("bmp")]
    [InlineData("tif")]
    [InlineData("tiff")]
    [InlineData("gif")]
    [InlineData("heic")]
    [InlineData("heif")]
    [InlineData("avif")]
    [InlineData("webp")]
    public void Every_target_format_can_be_written(string token)
    {
        var result = Convert("sample-rgba.png", token);
        AssertSucceeded(result);
        Assert.Equal("." + token, Path.GetExtension(result.OutputPath));
    }

    [Theory]
    [InlineData("sample.jpg")]
    [InlineData("sample.heic")]
    [InlineData("sample-rgba.png")]
    public void Every_bundled_source_format_can_be_read(string name)
    {
        var result = Convert(name, "bmp");
        AssertSucceeded(result);

        var image = ReadBack(result.OutputPath!);
        Assert.Equal(64, image.Width);
        Assert.Equal(48, image.Height);
    }
}
