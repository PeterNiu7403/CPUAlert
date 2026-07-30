using System.Runtime.InteropServices;

namespace WinMoe.Ui;

/// <summary>
/// Per-monitor DPI helpers for mixed-DPI environments.
/// </summary>
public static class DisplayDpi
{
    private const uint DefaultDpi = 96;
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtdDefaultToNearest = 2;

    public static uint GetDpiForWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return DefaultDpi;
        }

        try
        {
            var dpi = GetDpiForWindowNative(windowHandle);
            return dpi == 0 ? DefaultDpi : dpi;
        }
        catch
        {
            return DefaultDpi;
        }
    }

    public static uint GetDpiForPoint(int x, int y, uint fallbackDpi = DefaultDpi)
    {
        try
        {
            var monitor = MonitorFromPoint(new POINT { X = x, Y = y }, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return fallbackDpi == 0 ? DefaultDpi : fallbackDpi;
            }

            var hr = GetDpiForMonitor(monitor, MdtdDefaultToNearest, out var dpiX, out _);
            if (hr != 0 || dpiX == 0)
            {
                return fallbackDpi == 0 ? DefaultDpi : fallbackDpi;
            }

            return dpiX;
        }
        catch
        {
            return fallbackDpi == 0 ? DefaultDpi : fallbackDpi;
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint GetDpiForWindowNative(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
