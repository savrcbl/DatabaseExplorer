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
    // DWMWA_USE_IMMERSIVE_DARK_MODE. Windows 11 (and Windows 10 20H1+) use value 20;
    // early Windows 10 Insider builds used 19. We try 20 first and fall back to 19.
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
                // Older Windows 10 builds expect the pre-release attribute value instead.
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref useDark, sizeof(int));
            }
        }
        catch (EntryPointNotFoundException)
        {
            // dwmapi.dll on this OS version doesn't support the attribute at all
            // (very old Windows 10). Nothing to do — the title bar just stays light.
        }
        catch (DllNotFoundException)
        {
            // Not running on Windows (e.g. design-time tooling) — nothing to do.
        }
    }
}
