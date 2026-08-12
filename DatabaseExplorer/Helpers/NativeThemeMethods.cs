using System.Runtime.InteropServices;

namespace DatabaseExplorer.Helpers;

internal static class NativeThemeMethods
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

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
