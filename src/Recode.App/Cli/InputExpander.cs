namespace Recode.App.Cli;

/// <summary>
/// Expands wildcards in the file arguments.
/// </summary>
/// <remarks>
/// Neither cmd.exe nor PowerShell expands wildcards before handing arguments to
/// a program, so "recode --to jpg *.heic" would otherwise arrive as the literal
/// string. Every other command line tool on Windows deals with this itself, and
/// so does this one.
///
/// Arguments without a wildcard are passed through untouched, including ones
/// that do not exist, so that a missing file is reported by name rather than
/// silently vanishing from the list.
/// </remarks>
internal static class InputExpander
{
    public static IReadOnlyList<string> Expand(IReadOnlyList<string> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var expanded = new List<string>(inputs.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            if (input.IndexOfAny(new[] { '*', '?' }) < 0)
            {
                if (seen.Add(input))
                {
                    expanded.Add(input);
                }
                continue;
            }

            foreach (var match in Match(input))
            {
                if (seen.Add(match))
                {
                    expanded.Add(match);
                }
            }
        }

        return expanded;
    }

    private static IEnumerable<string> Match(string pattern)
    {
        string directory;
        string filePattern;

        try
        {
            directory = Path.GetDirectoryName(pattern) is { Length: > 0 } d ? d : ".";
            filePattern = Path.GetFileName(pattern);
        }
        catch (ArgumentException)
        {
            // Not a usable path. Hand it back unchanged so the conversion step
            // reports it as missing, with the text the user typed.
            return new[] { pattern };
        }

        if (string.IsNullOrEmpty(filePattern) || !Directory.Exists(directory))
        {
            return new[] { pattern };
        }

        try
        {
            var matches = Directory.GetFiles(directory, filePattern);
            Array.Sort(matches, StringComparer.OrdinalIgnoreCase);

            // A pattern that matches nothing is reported as such, rather than
            // quietly reducing the batch to fewer files than the user expected.
            return matches.Length > 0 ? matches : new[] { pattern };
        }
        catch (Exception)
        {
            return new[] { pattern };
        }
    }
}
