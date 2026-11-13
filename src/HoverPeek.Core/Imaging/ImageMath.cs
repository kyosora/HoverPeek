namespace HoverPeek.Core.Imaging;

public static class ImageMath
{
    /// <summary>
    /// 等比例縮到 maxDimension 以內；本來就夠小的不放大。
    /// 結果至少 1×1，避免極端長寬比（例如 20000×3）算出 0。
    /// </summary>
    public static (int Width, int Height) FitWithin(int width, int height, int maxDimension)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), $"圖片尺寸必須為正數，收到 {width}×{height}");
        if (maxDimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxDimension), $"maxDimension 必須為正數，收到 {maxDimension}");

        if (width <= maxDimension && height <= maxDimension)
            return (width, height);

        var scale = Math.Min((double)maxDimension / width, (double)maxDimension / height);
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>
    /// 把螢幕縮放比例（1.0 = 100%）套到邏輯尺寸上，得到實際要解碼的像素上限。
    /// </summary>
    public static int ToPhysicalPixels(int logicalPixels, double dpiScale)
    {
        if (dpiScale <= 0 || double.IsNaN(dpiScale))
            dpiScale = 1.0;
        return Math.Max(1, (int)Math.Round(logicalPixels * dpiScale));
    }
}
