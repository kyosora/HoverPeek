namespace HoverPeek.Core.Imaging;

/// <summary>
/// 已解碼、可直接交給 UI 的點陣圖。
/// 像素格式固定為 32bpp BGRA，alpha 已預乘（對應 WPF 的 Pbgra32），
/// UI 端用 BitmapSource.Create 就能顯示，不需要再編碼或解碼一次。
/// </summary>
public sealed record DecodedImage
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Stride { get; init; }
    public required byte[] Pixels { get; init; }

    /// <summary>原始檔案的像素尺寸（縮圖前），顯示在標題列的資訊用。</summary>
    public int SourceWidth { get; init; }
    public int SourceHeight { get; init; }

    /// <summary>編碼格式（JPEG、PNG…），系統縮圖來源時為 null。</summary>
    public string? Format { get; init; }

    /// <summary>
    /// 建議的顯示 DPI。解碼時已依螢幕縮放比例放大像素，
    /// UI 用這個 DPI 建 BitmapSource，就能在高 DPI 螢幕上 1:1 清晰顯示。
    /// </summary>
    public double DpiX { get; init; } = 96;
    public double DpiY { get; init; } = 96;

    public long ByteCount => Pixels.LongLength;
}
