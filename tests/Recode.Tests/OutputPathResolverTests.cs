using Recode.Core.Conversion;
using Recode.Tests.Fakes;
using Xunit;

namespace Recode.Tests;

/// <summary>
/// Output naming has more edge cases than anything else in the project, and
/// getting it wrong means overwriting somebody's photograph.
/// </summary>
public class OutputPathResolverTests
{
    private const string Folder = @"C:\Photos";

    private static OutputPathResolver ResolverWith(params string[] existing) =>
        new(new FakeFileSystem(existing));

    [Fact]
    public void Writes_beside_the_input_when_nothing_is_in_the_way()
    {
        var resolver = ResolverWith(@"C:\Photos\holiday.heic");

        var result = resolver.Resolve(@"C:\Photos\holiday.heic", ".png", null, force: false);

        Assert.Equal(@"C:\Photos\holiday.png", result.Path);
        Assert.False(result.OverwritesExisting);
        Assert.False(result.SameAsInput);
    }

    [Fact]
    public void Adds_1_when_the_name_is_taken()
    {
        var resolver = ResolverWith(
            @"C:\Photos\holiday.heic",
            @"C:\Photos\holiday.png");

        var result = resolver.Resolve(@"C:\Photos\holiday.heic", ".png", null, force: false);

        Assert.Equal(@"C:\Photos\holiday (1).png", result.Path);
    }

    [Fact]
    public void Counts_up_past_existing_numbered_copies()
    {
        var resolver = ResolverWith(
            @"C:\Photos\holiday.heic",
            @"C:\Photos\holiday.png",
            @"C:\Photos\holiday (1).png",
            @"C:\Photos\holiday (2).png");

        var result = resolver.Resolve(@"C:\Photos\holiday.heic", ".png", null, force: false);

        Assert.Equal(@"C:\Photos\holiday (3).png", result.Path);
    }

    [Fact]
    public void Fills_a_gap_in_the_numbering()
    {
        // (1) is missing, so it is reused rather than skipped. Explorer behaves
        // the same way.
        var resolver = ResolverWith(
            @"C:\Photos\holiday.heic",
            @"C:\Photos\holiday.png",
            @"C:\Photos\holiday (2).png");

        var result = resolver.Resolve(@"C:\Photos\holiday.heic", ".png", null, force: false);

        Assert.Equal(@"C:\Photos\holiday (1).png", result.Path);
    }

    [Fact]
    public void Force_overwrites_instead_of_numbering()
    {
        var resolver = ResolverWith(
            @"C:\Photos\holiday.heic",
            @"C:\Photos\holiday.png");

        var result = resolver.Resolve(@"C:\Photos\holiday.heic", ".png", null, force: true);

        Assert.Equal(@"C:\Photos\holiday.png", result.Path);
        Assert.True(result.OverwritesExisting);
    }

    [Fact]
    public void Same_format_without_force_produces_a_numbered_copy()
    {
        // The file in the way is the input itself. Recompressing must not
        // silently replace the original.
        var resolver = ResolverWith(@"C:\Photos\holiday.jpg");

        var result = resolver.Resolve(@"C:\Photos\holiday.jpg", ".jpg", null, force: false);

        Assert.Equal(@"C:\Photos\holiday (1).jpg", result.Path);
        Assert.False(result.SameAsInput);
    }

    [Fact]
    public void Same_format_with_force_recompresses_in_place()
    {
        var resolver = ResolverWith(@"C:\Photos\holiday.jpg");

        var result = resolver.Resolve(@"C:\Photos\holiday.jpg", ".jpg", null, force: true);

        Assert.Equal(@"C:\Photos\holiday.jpg", result.Path);
        Assert.True(result.SameAsInput);
        Assert.True(result.OverwritesExisting);
    }

    [Fact]
    public void Output_directory_is_used_when_given()
    {
        var resolver = ResolverWith(@"C:\Photos\holiday.heic");

        var result = resolver.Resolve(@"C:\Photos\holiday.heic", ".png", @"D:\Converted", force: false);

        Assert.Equal(@"D:\Converted\holiday.png", result.Path);
    }

    [Fact]
    public void Output_directory_removes_the_same_format_collision()
    {
        // holiday.jpg exists in the source folder but not in the target folder,
        // so no numbering is needed.
        var resolver = ResolverWith(@"C:\Photos\holiday.jpg");

        var result = resolver.Resolve(@"C:\Photos\holiday.jpg", ".jpg", @"D:\Converted", force: false);

        Assert.Equal(@"D:\Converted\holiday.jpg", result.Path);
        Assert.False(result.SameAsInput);
    }

    [Fact]
    public void Extension_without_a_dot_is_accepted()
    {
        var resolver = ResolverWith(@"C:\Photos\holiday.heic");

        var result = resolver.Resolve(@"C:\Photos\holiday.heic", "png", null, force: false);

        Assert.Equal(@"C:\Photos\holiday.png", result.Path);
    }

    [Fact]
    public void Dots_in_the_file_name_are_preserved()
    {
        var resolver = ResolverWith(@"C:\Photos\holiday.2026.06.12.heic");

        var result = resolver.Resolve(@"C:\Photos\holiday.2026.06.12.heic", ".png", null, force: false);

        // Only the last extension is replaced. Everything before it is part of
        // the name, whatever it looks like.
        Assert.Equal(@"C:\Photos\holiday.2026.06.12.png", result.Path);
    }

    [Fact]
    public void Existing_file_is_matched_case_insensitively()
    {
        // Windows would not let both exist, so neither may the resolver.
        var resolver = ResolverWith(
            @"C:\Photos\holiday.heic",
            @"C:\Photos\HOLIDAY.PNG");

        var result = resolver.Resolve(@"C:\Photos\holiday.heic", ".png", null, force: false);

        Assert.Equal(@"C:\Photos\holiday (1).png", result.Path);
    }

    [Fact]
    public void Names_with_spaces_and_brackets_survive()
    {
        var resolver = ResolverWith(
            @"C:\Photos\Skitur (Norge) 2026.heic",
            @"C:\Photos\Skitur (Norge) 2026.png");

        var result = resolver.Resolve(@"C:\Photos\Skitur (Norge) 2026.heic", ".png", null, force: false);

        Assert.Equal(@"C:\Photos\Skitur (Norge) 2026 (1).png", result.Path);
    }

    [Fact]
    public void Empty_input_path_is_rejected()
    {
        var resolver = ResolverWith();
        Assert.Throws<ArgumentException>(() => resolver.Resolve("", ".png", null, force: false));
    }
}
