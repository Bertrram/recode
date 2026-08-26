using Recode.Core.Formats;
using Recode.Core.Registry;
using Xunit;

namespace Recode.Tests;

/// <summary>
/// The registry layout is generated from the format table, not written by hand.
/// These tests hold that promise in place.
/// </summary>
public class ContextMenuGeneratorTests
{
    private const string Exe = @"C:\Tools\Recode\recode.exe";

    private static readonly FormatTable Table = FormatTableLoader.LoadEmbedded();

    private static ContextMenuPlan Generate() =>
        new ContextMenuGenerator(Table, new ContextMenuOptions { ExecutablePath = Exe }).Generate();

    [Fact]
    public void Every_readable_extension_gets_a_verb()
    {
        var plan = Generate();
        var readable = Table.Formats.Where(f => f.CanRead).SelectMany(f => f.Extensions).ToList();

        Assert.Equal(readable.Count, plan.RootKeys.Count);

        foreach (var extension in readable)
        {
            Assert.Contains(plan.RootKeys, k =>
                k.Contains($@"SystemFileAssociations\{extension}\shell\Recode.ConvertTo",
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Everything_is_written_under_HKCU()
    {
        var plan = Generate();

        // HKCU is what keeps the installer free of administrator rights.
        Assert.All(plan.Keys, k =>
            Assert.StartsWith(@"HKCU\Software\Classes", k.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void The_top_level_verb_declares_a_submenu()
    {
        var plan = Generate();
        var verb = plan.Keys.First(k => k.Path.EndsWith(@".png\shell\Recode.ConvertTo", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Convert to", Value(verb, "MUIVerb"));

        // An empty SubCommands is what makes Explorer read the nested shell key.
        Assert.Equal(string.Empty, Value(verb, "SubCommands"));
        Assert.Equal("Player", Value(verb, "MultiSelectModel"));
        Assert.Equal($"{Exe},0", Value(verb, "Icon"));
    }

    [Fact]
    public void The_source_format_is_absent_from_its_own_submenu()
    {
        var plan = Generate();
        var labels = SubmenuLabels(plan, ".png");

        Assert.DoesNotContain("PNG", labels);
        Assert.Contains("JPG", labels);
        Assert.Contains("WebP", labels);
    }

    [Fact]
    public void Both_jpeg_extensions_are_offered_from_a_non_jpeg_source()
    {
        var plan = Generate();
        var labels = SubmenuLabels(plan, ".png");

        // Same encoder, two entries, because the extension the user picks is
        // the extension they get.
        Assert.Contains("JPG", labels);
        Assert.Contains("JPEG", labels);
    }

    [Fact]
    public void Neither_jpeg_extension_is_offered_from_a_jpeg_source()
    {
        var plan = Generate();

        // Excluding by format rather than by extension. .jpg and .jpeg are the
        // same format, so neither belongs in the other's menu.
        var fromJpg = SubmenuLabels(plan, ".jpg");
        Assert.DoesNotContain("JPG", fromJpg);
        Assert.DoesNotContain("JPEG", fromJpg);

        var fromJpeg = SubmenuLabels(plan, ".jpeg");
        Assert.DoesNotContain("JPG", fromJpeg);
        Assert.DoesNotContain("JPEG", fromJpeg);
    }

    [Fact]
    public void Commands_quote_the_executable_and_the_file()
    {
        var plan = Generate();
        var command = plan.Keys
            .First(k => k.Path.EndsWith(@".png\shell\Recode.ConvertTo\shell\01-jpg\command", StringComparison.OrdinalIgnoreCase));

        var value = command.Values.Single(v => v.Name == string.Empty).Value;

        Assert.Equal($"\"{Exe}\" --to jpg -- \"%1\"", value);
    }

    [Fact]
    public void The_separator_before_the_operands_is_present()
    {
        var plan = Generate();

        // Without "--", a file named "--force.jpg" arriving from Explorer would
        // be read as a flag.
        foreach (var command in plan.Keys.Where(k => k.Path.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase)))
        {
            var value = command.Values.Single(v => v.Name == string.Empty).Value;

            if (value.Contains("--to", StringComparison.Ordinal))
            {
                Assert.Contains(" -- \"%1\"", value, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void Submenu_entries_are_ordered_by_key_name()
    {
        var plan = Generate();
        var prefix = @"HKCU\Software\Classes\SystemFileAssociations\.png\shell\Recode.ConvertTo\shell\";

        var names = plan.Keys
            .Where(k => k.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k.Path[prefix.Length..])
            .Where(n => !n.Contains('\\'))
            .ToList();

        // Explorer sorts submenu entries by key name, so the numeric prefix is
        // what actually controls the order the user sees.
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
    }

    [Fact]
    public void A_support_entry_sits_at_the_end_behind_a_separator()
    {
        var plan = Generate();
        var support = plan.Keys
            .First(k => k.Path.EndsWith(@".png\shell\Recode.ConvertTo\shell\99-support", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Format support", Value(support, "MUIVerb"));
        Assert.Equal("Single", Value(support, "MultiSelectModel"));

        // 0x20 is ECF_SEPARATORBEFORE.
        var flags = support.Values.Single(v => v.Name == "CommandFlags");
        Assert.Equal(RegistryValueKind.Dword, flags.Kind);
        Assert.Equal("32", flags.Value);

        var command = plan.Keys
            .First(k => k.Path.EndsWith(@"\99-support\command", StringComparison.OrdinalIgnoreCase));
        Assert.Equal($"\"{Exe}\" --about", command.Values.Single(v => v.Name == string.Empty).Value);
    }

    [Fact]
    public void Every_submenu_entry_has_exactly_one_command()
    {
        var plan = Generate();
        var entries = plan.Keys.Count(k =>
            k.Path.Contains(@"\Recode.ConvertTo\shell\", StringComparison.OrdinalIgnoreCase) &&
            !k.Path.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase));

        var commands = plan.Keys.Count(k =>
            k.Path.Contains(@"\Recode.ConvertTo\shell\", StringComparison.OrdinalIgnoreCase) &&
            k.Path.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(entries, commands);
    }

    [Fact]
    public void Every_key_carries_the_application_icon()
    {
        var plan = Generate();

        foreach (var key in plan.Keys.Where(k => !k.Path.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.Equal($"{Exe},0", Value(key, "Icon"));
        }
    }

    [Fact]
    public void Adding_a_format_to_the_table_adds_it_to_the_menu()
    {
        // The promise this whole design rests on: the menu comes from the
        // table, so a new format needs no change to the generator.
        const string json = """
        {
          "backends": [ { "id": "wic", "displayName": "WIC" } ],
          "formats": [
            { "id": "png", "displayName": "PNG", "extensions": [ ".png" ], "backend": "wic", "canRead": true, "canWrite": true },
            { "id": "jxl", "displayName": "JPEG XL", "extensions": [ ".jxl" ], "backend": "wic", "canRead": true, "canWrite": true }
          ]
        }
        """;

        var table = FormatTableLoader.Load(json);
        var plan = new ContextMenuGenerator(table, new ContextMenuOptions { ExecutablePath = Exe }).Generate();

        Assert.Contains(plan.RootKeys, k => k.Contains(@"\.jxl\", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("JPEG XL", SubmenuLabels(plan, ".png"));
        Assert.Contains("PNG", SubmenuLabels(plan, ".jxl"));
    }

    [Fact]
    public void An_empty_executable_path_is_refused()
    {
        Assert.Throws<ArgumentException>(() =>
            new ContextMenuGenerator(Table, new ContextMenuOptions { ExecutablePath = "  " }));
    }

    private static string Value(RegistryKeySpec key, string name) =>
        key.Values.Single(v => v.Name == name).Value;

    private static List<string> SubmenuLabels(ContextMenuPlan plan, string extension)
    {
        var prefix = $@"SystemFileAssociations\{extension}\shell\Recode.ConvertTo\shell\";

        return plan.Keys
            .Where(k => k.Path.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            .Where(k => !k.Path.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase))
            .SelectMany(k => k.Values.Where(v => v.Name == "MUIVerb"))
            .Select(v => v.Value)
            .ToList();
    }
}
