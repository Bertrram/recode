using System.Runtime.InteropServices;
using System.Text;

namespace Recode.App.Cli;

/// <summary>
/// Attaches to the console of whichever process started this one.
/// </summary>
/// <remarks>
/// Recode is built as a Windows application rather than a console application,
/// because a console application launched from a context menu flashes a black
/// window on screen for every conversion. The cost of that choice is that there
/// is no console to write to when the program genuinely is run from a terminal,
/// which is what this recovers.
///
/// One visible consequence remains: the shell does not wait for a Windows
/// application, so its prompt reappears before the output does. That is noted
/// in the README rather than worked around, because the workarounds are worse
/// than the problem.
/// </remarks>
internal static class ConsoleAttach
{
    private const int AttachParentProcess = -1;

    private static bool _attempted;
    private static bool _attached;

    /// <summary>
    /// True when there is somewhere to print. Safe to call more than once.
    /// </summary>
    public static bool TryAttach()
    {
        if (_attempted)
        {
            return _attached;
        }

        _attempted = true;

        try
        {
            // A console may already exist if the process was started in an
            // unusual way. Attaching again would fail and lose the one we have.
            if (GetConsoleWindow() == IntPtr.Zero && !AttachConsole(AttachParentProcess))
            {
                return false;
            }

            var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(output);

            var error = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
            Console.SetError(error);

            TrySetUtf8();

            _attached = true;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TrySetUtf8()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (Exception)
        {
            // Some hosts refuse the change. Output falls back to the console's
            // own code page, which is fine for the ASCII this program prints.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
}
