using System.Globalization;

namespace Recode.App.Cli;

/// <summary>
/// Parses the command line.
/// </summary>
/// <remarks>
/// Hand written rather than pulled from a package, because the surface is six
/// flags and a list of files, and because Explorer is one of the callers. A
/// selection made in Explorer can contain a file named "--force.jpg", so
/// everything after "--" is treated as an operand no matter what it looks like.
/// </remarks>
public static class CommandLineParser
{
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        // No arguments at all means the executable was double clicked or picked
        // from the Start menu. Showing what it supports is more use than a
        // usage message nobody can see.
        if (args.Count == 0)
        {
            return new CommandLineOptions { Mode = CommandMode.About };
        }

        var inputs = new List<string>();
        string? target = null;
        string? outputDirectory = null;
        int? quality = null;
        var force = false;
        var operandsOnly = false;
        CommandMode? explicitMode = null;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (operandsOnly)
            {
                inputs.Add(arg);
                continue;
            }

            switch (arg)
            {
                case "--":
                    operandsOnly = true;
                    continue;

                case "--to":
                    if (!TryTakeValue(args, ref i, "--to", out var targetValue, out var targetError))
                    {
                        return CommandLineOptions.Invalid(targetError);
                    }
                    target = targetValue;
                    continue;

                case "--quality":
                    if (!TryTakeValue(args, ref i, "--quality", out var qualityValue, out var qualityError))
                    {
                        return CommandLineOptions.Invalid(qualityError);
                    }
                    if (!int.TryParse(qualityValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return CommandLineOptions.Invalid($"--quality expects a number, not '{qualityValue}'.");
                    }
                    // Kept as typed. Clamping belongs to the conversion layer,
                    // which knows whether the target format uses quality at all.
                    quality = parsed;
                    continue;

                case "--outdir":
                    if (!TryTakeValue(args, ref i, "--outdir", out var outValue, out var outError))
                    {
                        return CommandLineOptions.Invalid(outError);
                    }
                    outputDirectory = outValue;
                    continue;

                case "--force":
                    force = true;
                    continue;

                case "--list":
                    explicitMode = CommandMode.List;
                    continue;

                case "--about":
                    explicitMode = CommandMode.About;
                    continue;

                case "--emit-registry":
                    explicitMode = CommandMode.EmitRegistry;
                    continue;

                case "--emit-extensions":
                    explicitMode = CommandMode.EmitExtensions;
                    continue;

                case "--help":
                case "-h":
                case "-?":
                case "/?":
                    return new CommandLineOptions { Mode = CommandMode.Help };

                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        return CommandLineOptions.Invalid($"Unknown option '{arg}'.");
                    }
                    inputs.Add(arg);
                    continue;
            }
        }

        if (explicitMode is not null)
        {
            return new CommandLineOptions
            {
                Mode = explicitMode.Value,
                Force = force,
                Quality = quality,
                OutputDirectory = outputDirectory,
                Inputs = inputs
            };
        }

        if (target is null)
        {
            return inputs.Count > 0
                ? CommandLineOptions.Invalid("No target format given. Use --to <format>, for example --to png.")
                : CommandLineOptions.Invalid("Nothing to do. Use --to <format> with one or more files, or --list.");
        }

        if (inputs.Count == 0)
        {
            return CommandLineOptions.Invalid($"--to {target} was given but no files were listed.");
        }

        return new CommandLineOptions
        {
            Mode = CommandMode.Convert,
            TargetToken = target,
            Quality = quality,
            Force = force,
            OutputDirectory = outputDirectory,
            Inputs = inputs
        };
    }

    private static bool TryTakeValue(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        out string value,
        out string error)
    {
        if (index + 1 >= args.Count)
        {
            value = string.Empty;
            error = $"{option} expects a value.";
            return false;
        }

        index++;
        value = args[index];
        error = string.Empty;
        return true;
    }
}
