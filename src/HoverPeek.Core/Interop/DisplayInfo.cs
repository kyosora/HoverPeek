using System.Runtime.InteropServices;

namespace HoverPeek.Core.Interop;

/// <summary>
/// 查詢某個螢幕座標所在的顯示器資訊。座標一律是實體像素
/// （程式以 PerMonitorV2 執行，滑鼠 hook、SetWindowPos 都用實體像素）。
/// </summary>
public static class DisplayInfo
{
    public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
    }

    /// <summary>該點所在螢幕的縮放比例，100% = 1.0。查不到時回傳 1.0。</summary>
    public static double GetDpiScaleAt(int x, int y)
    {
        var monitor = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return 1.0;

        try
        {
            return GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0
                ? dpiX / 96.0
                : 1.0;
        }
        catch (EntryPointNotFoundException)
        {
            return 1.0; // Windows 8.0 以前沒有 shcore，HoverPeek 不支援但也不該崩潰
        }
    }

    /// <summary>該點所在螢幕扣掉工作列後的可用範圍（實體像素）。</summary>
    public static PixelRect GetWorkAreaAt(int x, int y)
    {
        var monitor = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            throw new InvalidOperationException($"取不到座標 ({x},{y}) 所在螢幕的資訊，Win32 錯誤 {Marshal.GetLastWin32Error()}");

        var r = info.rcWork;
        return new PixelRect(r.Left, r.Top, r.Right, r.Bottom);
    }

    public static (int X, int Y) GetCursorPosition() =>
        GetCursorPos(out var p) ? (p.X, p.Y) : (0, 0);

    public static PixelRect? GetWindowRect(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !GetWindowRectNative(hwnd, out var r))
            return null;
        return new PixelRect(r.Left, r.Top, r.Right, r.Bottom);
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    private static extern bool GetWindowRectNative(IntPtr hWnd, out RECT lpRect);
}
