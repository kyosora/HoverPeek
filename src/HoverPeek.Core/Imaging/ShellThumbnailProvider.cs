using System.Runtime.InteropServices;
using HoverPeek.Core.Threading;

namespace HoverPeek.Core.Imaging;

/// <summary>
/// 透過 IShellItemImageFactory 取得 Windows 的縮圖（與檔案總管共用 thumbcache）。
/// 用途有二：
/// 1. 快速首圖：只查快取（cacheOnly），已看過的資料夾 7–15ms 就有畫面
/// 2. 後備解碼：Skia 不支援的格式（AVIF、HEIC、RAW…）交給系統安裝的 WIC codec
/// </summary>
public sealed class ShellThumbnailProvider : IDisposable
{
    private readonly StaWorker _worker = new("HoverPeek.ShellThumbnail");

    public Task<DecodedImage?> GetAsync(
        string path, int maxPhysicalPixels, double dpiScale, bool cacheOnly, CancellationToken ct = default)
    {
        return _worker.InvokeAsync(() => Get(path, maxPhysicalPixels, dpiScale, cacheOnly), ct);
    }

    private static DecodedImage? Get(string path, int maxPhysicalPixels, double dpiScale, bool cacheOnly)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        if (hr < 0 || factory == null)
            return null;

        try
        {
            var flags = SIIGBF.ResizeToFit | SIIGBF.ThumbnailOnly;
            if (cacheOnly)
                flags |= SIIGBF.InCacheOnly;

            hr = factory.GetImage(new SIZE { cx = maxPhysicalPixels, cy = maxPhysicalPixels }, flags, out var hbitmap);
            if (hr < 0 || hbitmap == IntPtr.Zero)
                return null;

            try
            {
                return ReadBitmap(hbitmap, dpiScale);
            }
            finally
            {
                DeleteObject(hbitmap);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>
    /// 用 GetDIBits 讀成 top-down 32bpp BGRA。
    /// CreateBitmapSourceFromHBitmap 會把 alpha 丟掉（透明 PNG 變黑底），所以自己讀。
    /// </summary>
    private static DecodedImage? ReadBitmap(IntPtr hbitmap, double dpiScale)
    {
        if (GetObject(hbitmap, Marshal.SizeOf<BITMAP>(), out var bm) == 0 || bm.bmWidth <= 0 || bm.bmHeight <= 0)
            return null;

        var width = bm.bmWidth;
        var height = Math.Abs(bm.bmHeight);
        var stride = width * 4;
        var pixels = new byte[stride * height];

        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // 負值 = top-down，跟 WPF 的列順序一致
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0, // BI_RGB
        };

        var hdc = GetDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(hdc, hbitmap, 0, (uint)height, pixels, ref header, 0) == 0)
                return null;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }

        NormalizeAlpha(pixels);

        var dpi = 96.0 * (dpiScale > 0 ? dpiScale : 1.0);
        return new DecodedImage
        {
            Width = width,
            Height = height,
            Stride = stride,
            Pixels = pixels,
            DpiX = dpi,
            DpiY = dpi,
        };
    }

    /// <summary>
    /// 不透明的縮圖（大多數 JPEG）alpha 全是 0，直接當 Pbgra32 會整張透明。
    /// 全 0 時補成 255；有任何非 0 alpha 就視為已預乘的透明圖，保持原樣。
    /// </summary>
    internal static void NormalizeAlpha(byte[] bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] != 0)
                return;
        }

        for (var i = 3; i < bgra.Length; i += 4)
            bgra[i] = 255;
    }

    public void Dispose() => _worker.Dispose();

    [Flags]
    private enum SIIGBF
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        MemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid, out IShellItemImageFactory? ppv);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        [Out] byte[] lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
}
