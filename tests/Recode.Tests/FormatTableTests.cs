using Recode.Core.Formats;
using Xunit;

namespace Recode.Tests;

/// <summary>
/// The format table is the single source of truth for the whole program, so
/// these tests run against the real formats.json rather than a fixture.
/// </summary>
public class FormatTableTests
{
    private static FormatTable Table => FormatTableLoader.LoadEmbedded();

    [Theory]
    [InlineData(".png", "png")]
    [InlineData(".PNG", "png")]
    [InlineData("png", "png")]
    [InlineData(".webp", "webp")]
    [InlineData(".HEIC", "heic")]
    [InlineData("  .avif  ", "avif")]
    public void Extension_lookup_is_case_and_dot_insensitive(string input, string expectedId)
    {
        Assert.True(Table.TryGetByExtension(input, out var format));
        Assert.Equal(expectedId, format!.Id);
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData("JPEG")]
    public void Jpg_and_jpeg_resolve_to_the_same_format(string alias)
    {
        Assert.True(Table.TryGetByExtension(alias, out var format));
        Assert.Equal("jpeg", format!.Id);
    }

    [Fact]
    public void Jpg_and_jpeg_are_separate_targets_that_keep_their_extension()
    {
        Assert.True(Table.TryResolveTarget("jpg", out var jpg));
        Assert.True(Table.TryResolveTarget("jpeg", out var jpeg));

        // Same encoder, different file name. That is the whole point of
        // modelling a target as a format plus an extension.
        Assert.Equal(jpg!.Format.Id, jpeg!.Format.Id);
        Assert.Equal(".jpg", jpg.Extension);
        Assert.Equal(".jpeg", jpeg.Extension);
        Assert.Equal("JPG", jpg.MenuLabel);
        Assert.Equal("JPEG", jpeg.MenuLabel);
    }

    [Theory]
    [InlineData(".xyz")]
    [InlineData("psd")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("   ")]
    public void Unknown_extensions_are_rejected(string extension)
    {
        Assert.False(Table.TryGetByExtension(extension, out var format));
        Assert.Null(format);
    }

    [Fact]
    public void Unknown_target_is_rejected()
    {
        Assert.False(Table.TryResolveTarget("psd", out var target));
        Assert.Null(target);
    }

    [Fact]
    public void Single_extension_formats_keep_their_display_name_in_the_menu()
    {
        Assert.True(Table.TryResolveTarget("webp", out var webp));

        // Not "WEBP". A format with one extension has nothing to disambiguate,
        // so it uses the name people recognise.
        Assert.Equal("WebP", webp!.MenuLabel);
    }

    [Fact]
    public void Every_format_names_a_backend_that_exists()
    {
        foreach (var format in Table.Formats)
        {
            var backend = Table.GetBackendFor(format);
            Assert.Equal(format.BackendId, backend.Id);
        }
    }

    [Fact]
    public void Every_extension_is_claimed_by_exactly_one_format()
    {
        var all = Table.Formats.SelectMany(f => f.Extensions).ToList();
        Assert.Equal(all.Count, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Path_lookup_uses_the_extension()
    {
        Assert.True(Table.TryGetForPath(@"C:\Photos\Holiday 2026\IMG_0042.HEIC", out var format));
        Assert.Equal("heic", format!.Id);
    }

    [Fact]
    public void Path_without_extension_is_rejected()
    {
        Assert.False(Table.TryGetForPath(@"C:\Photos\README", out _));
    }

    [Fact]
    public void Writable_targets_cover_every_writable_extension()
    {
        var targets = Table.WritableTargets.ToList();
        var expected = Table.Formats.Where(f => f.CanWrite).SelectMany(f => f.Extensions).ToList();

        Assert.Equal(expected.Count, targets.Count);
        Assert.All(targets, t => Assert.True(t.Format.CanWrite));
    }

    [Fact]
    public void Heic_reports_kvazaar_rather_than_x265()
    {
        Assert.True(Table.TryGetByExtension(".heic", out var heic));
        var backend = Table.GetBackendFor(heic!);
        var description = heic.DescribeBackend(backend);

        // x265 is GPL. If it ever appears here, the licensing story has broken.
        Assert.Contains("kvazaar", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("x265", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_extensions_are_rejected_when_building_a_table()
    {
        const string json = """
        {
          "backends": [ { "id": "wic", "displayName": "WIC" } ],
          "formats": [
            { "id": "a", "displayName": "A", "extensions": [ ".dup" ], "backend": "wic", "canRead": true, "canWrite": true },
            { "id": "b", "displayName": "B", "extensions": [ ".dup" ], "backend": "wic", "canRead": true, "canWrite": true }
          ]
        }
        """;

        var error = Assert.Throws<FormatTableException>(() => FormatTableLoader.Load(json));
        Assert.Contains(".dup", error.Message);
    }

    [Fact]
    public void Format_naming_a_missing_backend_is_rejected()
    {
        const string json = """
        {
          "backends": [ { "id": "wic", "displayName": "WIC" } ],
          "formats": [
            { "id": "a", "displayName": "A", "extensions": [ ".a" ], "backend": "nonexistent", "canRead": true, "canWrite": true }
          ]
        }
        """;

        var error = Assert.Throws<FormatTableException>(() => FormatTableLoader.Load(json));
        Assert.Contains("nonexistent", error.Message);
    }

    [Fact]
    public void Extension_without_a_dot_in_json_is_normalised()
    {
        const string json = """
        {
          "backends": [ { "id": "wic", "displayName": "WIC" } ],
          "formats": [
            { "id": "a", "displayName": "A", "extensions": [ "A" ], "backend": "wic", "canRead": true, "canWrite": true }
          ]
        }
        """;

        var table = FormatTableLoader.Load(json);
        Assert.True(table.TryGetByExtension(".a", out var format));
        Assert.Equal(".a", format!.PrimaryExtension);
    }
}
