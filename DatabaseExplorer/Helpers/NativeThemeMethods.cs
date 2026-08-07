using System.Runtime.InteropServices;

namespace DatabaseExplorer.Helpers;

/// <summary>
/// WPF has no built-in concept of a "dark" native title bar — unlike WinUI/UWP, its
/// standard window chrome is always drawn by Windows in the OS's default (light) style
/// unless the app explicitly opts in via the DWM API. This class makes that call so the
/// native title bar matches the app's current light/dark theme instead of staying light
/// regardless of what the window's content looks like.
/// </summary>
internal static class NativeThemeMethods
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    /// <summary>
    /// Sets the native title bar of the given window handle to dark or light mode.
    /// Safe to call on any Windows version — failures are swallowed since title bar
    /// theming is a cosmetic enhancement, not something worth crashing over.
    /// </summary>
    public static void SetImmersiveDarkMode(IntPtr hwnd, bool enabled)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var useDark = enabled ? 1 : 0;

        try
        {
            var result = DwmSetWindowAttribute(
                hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));

            if (result != 0)
            {
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref useDark, sizeof(int));
            }
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }
    }
}
