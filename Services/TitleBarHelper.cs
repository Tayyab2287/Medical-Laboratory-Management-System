using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LabReportApp.Services
{
    /// <summary>
    /// The colored area with the app's own content (banner, cards, etc.) is drawn by WPF and
    /// themes normally. The very top strip with the window title/minimize/maximize/close buttons
    /// is drawn by Windows itself, not WPF - this class asks Windows (via the DWM API) to draw
    /// that strip in dark or light mode to match the app's theme. Supported on Windows 10
    /// (build 1809+) and Windows 11; on older Windows versions this silently does nothing and
    /// the title bar just stays the default color.
    /// </summary>
    public static class TitleBarHelper
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_NEW = 20; // Windows 10 20H1+ / Windows 11
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19; // Windows 10 1809-1909

        public static void ApplyTitleBarTheme(Window window, bool isDark)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                int useDark = isDark ? 1 : 0;

                int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_NEW, ref useDark, sizeof(int));
                if (result != 0)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
            }
            catch
            {
                // Older/unsupported Windows versions - just leave the title bar as-is.
            }
        }
    }
}