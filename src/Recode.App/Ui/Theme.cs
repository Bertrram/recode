using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Recode.App.Ui;

/// <summary>
/// Follows the system light and dark setting.
/// </summary>
/// <remarks>
/// Two parts to looking native on Windows 11: the window contents, handled by
/// the resource dictionary below, and the title bar, which the desktop window
/// manager draws and which stays light unless it is told otherwise. A dark
/// window with a light title bar is the usual giveaway that an application is
/// not really following the theme.
///
/// The palette is written out rather than taken from SystemColors, which still
/// reports the Windows 7 era greys and would look wrong next to Explorer.
/// </remarks>
public static class Theme
{
    /// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE.</summary>
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static bool IsDark { get; } = DetectDarkMode();

    /// <summary>
    /// Segoe UI Variable is the Windows 11 interface font. Segoe UI is listed
    /// after it so the window still looks right on Windows 10.
    /// </summary>
    public static FontFamily InterfaceFont { get; } =
        new("Segoe UI Variable Text, Segoe UI Variable, Segoe UI");

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Resources.MergedDictionaries.Add(CreateResources());
        window.FontFamily = InterfaceFont;
        window.Background = Brush("RecodeWindowBackground");

        // The handle does not exist until the window is being shown, so the
        // title bar is recoloured on the way up.
        window.SourceInitialized += (_, _) => ApplyTitleBar(window);
    }

    private static ResourceDictionary CreateResources()
    {
        var resources = new ResourceDictionary();

        if (IsDark)
        {
            Add(resources, "RecodeWindowBackground", "#202020");
            Add(resources, "RecodeCardBackground", "#2B2B2B");
            Add(resources, "RecodeBorder", "#383838");
            Add(resources, "RecodeText", "#FFFFFF");
            Add(resources, "RecodeSecondaryText", "#C5C5C5");
            Add(resources, "RecodeOk", "#6CCB5F");
            Add(resources, "RecodeProblem", "#FF99A4");
            Add(resources, "RecodeLink", "#69B7FF");
        }
        else
        {
            Add(resources, "RecodeWindowBackground", "#F3F3F3");
            Add(resources, "RecodeCardBackground", "#FFFFFF");
            Add(resources, "RecodeBorder", "#E5E5E5");
            Add(resources, "RecodeText", "#1A1A1A");
            Add(resources, "RecodeSecondaryText", "#5D5D5D");
            Add(resources, "RecodeOk", "#0F7B0F");
            Add(resources, "RecodeProblem", "#C42B1C");
            Add(resources, "RecodeLink", "#0F6CBD");
        }

        return resources;
    }

    private static void Add(ResourceDictionary resources, string key, string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        resources[key] = brush;
    }

    private static Brush Brush(string key)
    {
        var resources = CreateResources();
        return (Brush)resources[key];
    }

    private static void ApplyTitleBar(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var useDark = IsDark ? 1 : 0;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        }
        catch (Exception)
        {
            // Older builds of Windows do not know the attribute. A light title
            // bar is not worth failing to open the window over.
        }
    }

    private static bool DetectDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            // AppsUseLightTheme is 0 for dark and 1 for light. Absent means the
            // default, which is light.
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
