using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Recode.App.Cli;
using Recode.App.Ui;
using Recode.Core.Conversion;
using Recode.Core.Diagnostics;
using Recode.Core.Formats;
using Recode.Core.Registry;

namespace Recode.App;

/// <summary>
/// Entry point. Decides between converting, listing, showing the window and
/// printing the registry layout, then gets out of the way.
/// </summary>
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            return Run(CommandLineParser.Parse(args));
        }
        catch (Exception ex)
        {
            // Last resort. Anything reaching here is a defect, but the user
            // should still get a sentence rather than a stack trace dialog.
            return ReportFatal(ex);
        }
    }

    private static int Run(CommandLineOptions options)
    {
        var table = FormatTableLoader.LoadEmbedded();

        return options.Mode switch
        {
            CommandMode.Help => ShowHelp(options),
            CommandMode.List => ShowList(table),
            CommandMode.About => ShowAbout(table),
            CommandMode.EmitRegistry => EmitRegistry(table),
            CommandMode.EmitExtensions => EmitExtensions(table),
            CommandMode.Convert => Convert(table, options),
            _ => ShowHelp(options)
        };
    }

    // -----------------------------------------------------------------------
    // Converting
    // -----------------------------------------------------------------------

    private static int Convert(FormatTable table, CommandLineOptions options)
    {
        if (!table.TryResolveTarget(options.TargetToken!, out var target))
        {
            return ReportUsageError(
                $"'{options.TargetToken}' is not a format Recode can write. Run 'recode --list' to see the options.");
        }

        var inputs = InputExpander.Expand(options.Inputs);
        if (inputs.Count == 0)
        {
            return ReportUsageError("No files matched.");
        }

        var registry = CodecRegistry.CreateDefault();
        var service = new ConversionService(table, registry);

        var conversionOptions = new ConversionOptions(
            options.Quality,
            options.Force,
            options.OutputDirectory);

        var hasConsole = ConsoleAttach.TryAttach();

        var summary = service.Convert(
            inputs,
            target,
            conversionOptions,
            hasConsole ? new Progress<ConversionResult>(PrintResult) : null);

        if (hasConsole)
        {
            PrintSummary(summary);
        }
        else if (summary.FailedCount > 0 || summary.SkippedCount > 0)
        {
            // Started from the context menu, so there is nowhere to print.
            // Silence on success, a window on failure.
            ShowFailures(summary);
        }

        return summary.AllSucceeded ? ExitSuccess : ExitFailure;
    }

    private static void PrintResult(ConversionResult result)
    {
        var name = Path.GetFileName(result.InputPath);

        switch (result.Status)
        {
            case ConversionStatus.Converted:
                Console.WriteLine($"{name} -> {Path.GetFileName(result.OutputPath)}");
                break;

            case ConversionStatus.Skipped:
                Console.Error.WriteLine($"{name}: skipped. {result.Message}");
                break;

            default:
                Console.Error.WriteLine($"{name}: {result.Message}");
                break;
        }
    }

    private static void PrintSummary(ConversionSummary summary)
    {
        if (summary.Results.Count <= 1 && summary.AllSucceeded)
        {
            return;
        }

        var parts = new List<string> { $"{summary.ConvertedCount} converted" };

        if (summary.FailedCount > 0)
        {
            parts.Add($"{summary.FailedCount} failed");
        }
        if (summary.SkippedCount > 0)
        {
            parts.Add($"{summary.SkippedCount} skipped");
        }

        Console.WriteLine(string.Join(", ", parts) + ".");
    }

    private static void ShowFailures(ConversionSummary summary)
    {
        var details = summary.Results
            .Where(r => !r.Succeeded)
            .Select(r => $"{Path.GetFileName(r.InputPath)}: {r.Message}")
            .ToList();

        var heading = summary.ConvertedCount > 0
            ? $"{summary.ConvertedCount} of {summary.Results.Count} files were converted."
            : summary.Results.Count == 1
                ? "The file could not be converted."
                : "None of the files could be converted.";

        ShowWindow(new MessageWindow(heading, details));
    }

    // -----------------------------------------------------------------------
    // Listing and the support window
    // -----------------------------------------------------------------------

    private static int ShowList(FormatTable table)
    {
        ConsoleAttach.TryAttach();

        var report = new SupportProbe(table, CodecRegistry.CreateDefault()).Run();

        Console.WriteLine($"{"Format",-8} {"Read",-5} {"Write",-6} Backend");

        foreach (var row in report.Rows)
        {
            var read = row.CanRead && row.Available ? "yes" : "no";
            var write = row.CanWrite && row.Available ? "yes" : "no";
            Console.WriteLine($"{row.DisplayName,-8} {read,-5} {write,-6} {row.Backend}");
        }

        if (report.Healthy)
        {
            return ExitSuccess;
        }

        Console.WriteLine();
        foreach (var backend in report.BrokenBackends)
        {
            Console.Error.WriteLine(
                backend.MissingLibrary is not null && backend.ExpectedLocation is not null
                    ? $"{backend.MissingLibrary} is missing from {backend.ExpectedLocation}"
                    : $"{backend.DisplayName}: {backend.Problem}");
        }

        return ExitFailure;
    }

    private static int ShowAbout(FormatTable table)
    {
        var report = new SupportProbe(table, CodecRegistry.CreateDefault()).Run();
        ShowWindow(new AboutWindow(report));
        return ExitSuccess;
    }

    // -----------------------------------------------------------------------
    // Registry layout, consumed by tools/install-context-menu.ps1
    // -----------------------------------------------------------------------

    private static int EmitRegistry(FormatTable table)
    {
        ConsoleAttach.TryAttach();

        var generator = new ContextMenuGenerator(
            table,
            new ContextMenuOptions { ExecutablePath = AppInfo.ExecutablePath });

        var plan = generator.Generate();

        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });

        Console.WriteLine(json);
        return ExitSuccess;
    }

    /// <summary>
    /// Every extension the context menu should attach to, one per line.
    /// </summary>
    private static int EmitExtensions(FormatTable table)
    {
        ConsoleAttach.TryAttach();

        foreach (var extension in table.ReadableExtensions)
        {
            Console.WriteLine(extension);
        }

        return ExitSuccess;
    }

    // -----------------------------------------------------------------------
    // Help and failures
    // -----------------------------------------------------------------------

    private static int ShowHelp(CommandLineOptions options)
    {
        var hasConsole = ConsoleAttach.TryAttach();

        if (!hasConsole)
        {
            // Launched from Explorer with something malformed. The support
            // window is more use here than a usage message with no console.
            return ShowAbout(FormatTableLoader.LoadEmbedded());
        }

        if (options.HasError)
        {
            Console.Error.WriteLine(options.Error);
            Console.Error.WriteLine();
        }

        Console.WriteLine(UsageText);
        return options.HasError ? ExitFailure : ExitSuccess;
    }

    private static int ReportUsageError(string message)
    {
        if (ConsoleAttach.TryAttach())
        {
            Console.Error.WriteLine(message);
        }
        else
        {
            ShowWindow(new MessageWindow("Recode could not run that command.", new[] { message }));
        }

        return ExitFailure;
    }

    private static int ReportFatal(Exception ex)
    {
        if (ConsoleAttach.TryAttach())
        {
            Console.Error.WriteLine(ex.Message);
        }
        else
        {
            try
            {
                ShowWindow(new MessageWindow("Recode stopped unexpectedly.", new[] { ex.Message }));
            }
            catch (Exception)
            {
                // If even the window will not open there is nothing left to try.
            }
        }

        return ExitFailure;
    }

    /// <summary>
    /// Shows a window and waits for it to close.
    /// </summary>
    /// <remarks>
    /// A WPF Application is created here rather than through App.xaml, because
    /// most runs of this program never show a window at all and should not pay
    /// for starting one.
    /// </remarks>
    private static void ShowWindow(Window window)
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
        application.Run(window);
    }

    private static string UsageText =>
        $"""
        {AppInfo.Name} {AppInfo.Version}
        Convert images between formats.

        Usage:
          recode --to <format> [options] <file> [<file> ...]
          recode --list
          recode --about

        Options:
          --to <format>       Target format, for example png, jpg, webp, avif
          --quality <1-100>   Quality for jpg, webp, heic and avif. Default 85
          --force             Replace existing files instead of adding (1), (2)
          --outdir <path>     Write results here instead of beside each input
          --list              Show every format and the backend that handles it
          --about             Open the support window
          --help              Show this text

        Examples:
          recode --to png photo.heic
          recode --to jpg --quality 92 *.heic
          recode --to webp --outdir converted image1.png image2.png

        Originals are never deleted. If the target file already exists, a
        numbered copy is written instead, unless --force is given.
        """;
}
