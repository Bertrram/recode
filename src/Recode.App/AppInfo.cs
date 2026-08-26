using System.Diagnostics;
using System.Reflection;

namespace Recode.App;

/// <summary>
/// Constants about this build, kept in one place so the window footer, the
/// usage text and the README cannot drift apart.
/// </summary>
public static class AppInfo
{
    public const string Name = "Recode";

    public const string RepositoryUrl = "https://github.com/Bertrram/recode";

    /// <summary>Version as shown to the user, for example "0.1.0".</summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>Full path to the running executable.</summary>
    public static string ExecutablePath { get; } = ReadExecutablePath();

    private static string ReadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the source revision the SDK appends, for example
            // "0.1.0+9f2c1a". The commit hash is of no use in a window title.
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string ReadExecutablePath()
    {
        // ProcessPath is the executable itself. Assembly.Location points at the
        // managed DLL, which is not what Explorer needs in a registry command.
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        return Process.GetCurrentProcess().MainModule?.FileName ?? "recode.exe";
    }
}
