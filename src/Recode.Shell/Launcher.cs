using System.Diagnostics;
using System.Text;

namespace Recode.Shell;

/// <summary>
/// Starts recode.exe.
/// </summary>
/// <remarks>
/// The extension does no conversion of its own. It builds a command line and
/// hands over, which keeps the code running inside Explorer's surrogate down to
/// menu drawing and process creation. Anything that decodes an image, and so
/// anything that could be fed a malformed file, stays in a separate process
/// that can fail without taking the shell with it.
/// </remarks>
internal static class Launcher
{
    internal const string ExecutableName = "recode.exe";

    /// <summary>
    /// Windows caps a command line at roughly 32767 characters. Long photo
    /// paths reach that sooner than expected, so a very large selection is sent
    /// in batches rather than truncated.
    /// </summary>
    private const int CommandLineBudget = 30000;

    internal static string ExecutablePath => Path.Combine(ModuleLocation.Directory, ExecutableName);

    internal static bool IsAvailable => File.Exists(ExecutablePath);

    /// <summary>Converts files to the given target extension, for example "png".</summary>
    internal static void Convert(string targetToken, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || !IsAvailable)
        {
            return;
        }

        foreach (var batch in Batch(paths))
        {
            var arguments = new StringBuilder();
            arguments.Append("--to ").Append(targetToken).Append(" --");

            foreach (var path in batch)
            {
                arguments.Append(" \"").Append(path).Append('"');
            }

            Start(arguments.ToString());
        }
    }

    internal static void ShowSupportWindow() => Start("--about");

    private static void Start(string arguments)
    {
        if (!IsAvailable)
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,

                // Without this the child inherits the surrogate's working
                // directory, which is somewhere in System32.
                WorkingDirectory = ModuleLocation.Directory
            };

            using var process = Process.Start(startInfo);
        }
        catch (Exception)
        {
            // Nothing may propagate into Explorer from here. recode.exe reports
            // its own failures in its own window.
        }
    }

    /// <summary>
    /// Splits a selection into groups that fit on a command line.
    /// </summary>
    private static IEnumerable<List<string>> Batch(IReadOnlyList<string> paths)
    {
        var batch = new List<string>();
        var length = 0;

        foreach (var path in paths)
        {
            // Each path costs its own length plus two quotes and a space.
            var cost = path.Length + 3;

            if (batch.Count > 0 && length + cost > CommandLineBudget)
            {
                yield return batch;
                batch = new List<string>();
                length = 0;
            }

            batch.Add(path);
            length += cost;
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }
}
