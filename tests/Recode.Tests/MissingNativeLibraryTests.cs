using Recode.Core.Abstractions;
using Recode.Core.Codecs;
using Recode.Core.Conversion;
using Recode.Core.Diagnostics;
using Recode.Core.Formats;
using Recode.Tests.Fakes;
using Xunit;

namespace Recode.Tests;

/// <summary>
/// What happens when a bundled library is missing or broken.
/// </summary>
/// <remarks>
/// This is the scenario the support window exists for. A copy that went wrong
/// must produce a sentence naming the file and the folder, never a crash and
/// never an exception from inside the interop layer.
/// </remarks>
public class MissingNativeLibraryTests
{
    private static readonly FormatTable Table = FormatTableLoader.LoadEmbedded();

    private static ImageFormat Format(string extension)
    {
        Assert.True(Table.TryGetByExtension(extension, out var format));
        return format!;
    }

    [Fact]
    public void A_missing_library_is_reported_rather_than_thrown()
    {
        var codec = new HeifCodec(new MissingNativeLibraryLoader());

        var availability = codec.CheckAvailability();

        Assert.False(availability.Available);
        Assert.Equal("heif.dll", availability.MissingLibrary);
        Assert.Contains(@"C:\Program Files\Recode", availability.ExpectedLocation);
    }

    [Fact]
    public void Checking_availability_never_throws()
    {
        // The support window calls this while drawing itself. If it threw, the
        // window explaining the problem would be the thing that failed.
        var heif = new HeifCodec(new MissingNativeLibraryLoader());
        var webp = new WebpCodec(new MissingNativeLibraryLoader());

        Assert.False(heif.CheckAvailability().Available);
        Assert.False(webp.CheckAvailability().Available);
    }

    [Fact]
    public void Decoding_without_the_library_names_the_file_and_the_folder()
    {
        var codec = new HeifCodec(new MissingNativeLibraryLoader());

        var error = Assert.Throws<BackendUnavailableException>(
            () => codec.Decode(@"C:\Photos\holiday.heic", Format(".heic")));

        Assert.Contains("heif.dll", error.Message);
        Assert.Contains(@"C:\Program Files\Recode", error.Message);
    }

    [Fact]
    public void Encoding_without_the_library_fails_the_same_way()
    {
        var codec = new WebpCodec(new MissingNativeLibraryLoader());
        var image = RgbaImage.Allocate(4, 4);

        var error = Assert.Throws<BackendUnavailableException>(
            () => codec.Encode(image, Format(".webp"), @"C:\out.webp", EncodeOptions.Default));

        Assert.Contains("libwebp.dll", error.Message);
    }

    [Fact]
    public void A_library_that_loads_but_exports_nothing_is_reported_too()
    {
        // The other failure mode: the file is there but is a different build.
        var codec = new HeifCodec(new EmptyNativeLibraryLoader());

        var availability = codec.CheckAvailability();

        Assert.False(availability.Available);
        Assert.Contains("heif_context_alloc", availability.Detail ?? string.Empty);
    }

    [Fact]
    public void A_batch_survives_a_missing_backend_and_reports_per_file()
    {
        var registry = new CodecRegistry(new IImageCodec[]
        {
            new WicCodec(),
            new HeifCodec(new MissingNativeLibraryLoader()),
            new WebpCodec(new MissingNativeLibraryLoader())
        });

        var service = new ConversionService(Table, registry, new FakeFileSystem(@"C:\Photos\a.heic"));

        // A HEIC source, so the missing library is hit while decoding. The
        // codec checks that it has a usable library before it opens anything,
        // which is what lets this stay a unit test with no file on disk.
        Assert.True(Table.TryResolveTarget("png", out var target));
        var result = service.ConvertOne(@"C:\Photos\a.heic", target!, ConversionOptions.Default);

        // A failure, not an exception. The batch would carry on to the next file.
        Assert.Equal(ConversionStatus.Failed, result.Status);
        Assert.Contains("heif.dll", result.Message);
    }

    [Fact]
    public void The_support_report_still_renders_with_everything_broken()
    {
        var registry = new CodecRegistry(new IImageCodec[]
        {
            new WicCodec(),
            new HeifCodec(new MissingNativeLibraryLoader()),
            new WebpCodec(new MissingNativeLibraryLoader())
        });

        var report = new SupportProbe(Table, registry).Run();

        Assert.False(report.Healthy);
        Assert.Equal(Table.Formats.Sum(f => f.Extensions.Count), report.Rows.Count);

        // WIC is part of Windows, so those formats keep working no matter what
        // happened to the bundled files. That is the point of the split.
        var png = report.Rows.Single(r => r.Extension == ".png");
        Assert.True(png.Available);
        Assert.True(png.CanRead);
        Assert.True(png.CanWrite);

        var heic = report.Rows.Single(r => r.Extension == ".heic");
        Assert.False(heic.Available);
        Assert.Contains("heif.dll", heic.Problem ?? string.Empty);

        Assert.Equal(2, report.BrokenBackends.Count());
    }

    [Fact]
    public void Every_format_still_gets_a_row_when_backends_are_missing()
    {
        var registry = new CodecRegistry(new IImageCodec[]
        {
            new WicCodec(),
            new HeifCodec(new MissingNativeLibraryLoader()),
            new WebpCodec(new MissingNativeLibraryLoader())
        });

        var report = new SupportProbe(Table, registry).Run();

        foreach (var extension in Table.Formats.SelectMany(f => f.Extensions))
        {
            Assert.Contains(report.Rows, r => r.Extension == extension);
        }
    }
}
