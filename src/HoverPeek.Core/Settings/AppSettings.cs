namespace HoverPeek.Core.Settings;

public sealed class AppSettings
{
    // 懸停行為
    public int HoverDelayMs { get; set; } = 500;
    public int JitterTolerancePx { get; set; } = 8;
    public int AutoCloseDelayMs { get; set; } = 600;

    // 預覽視窗
    public double WindowWidth { get; set; } = 800;
    public double WindowHeight { get; set; } = 600;
    public bool CenterWindow { get; set; } = true;  // true=螢幕中央, false=跟隨滑鼠
    public int FadeInDurationMs { get; set; } = 150;
    public int FadeOutDurationMs { get; set; } = 100;

    // 圖片預覽
    public int ImageMaxDimension { get; set; } = 800;
    public bool EnableGifAnimation { get; set; } = true;

    // 文字預覽
    public long TextMaxFileSizeMB { get; set; } = 1;
    public int TextMaxLines { get; set; } = 1000;
    public int TextFontSize { get; set; } = 11;
    public string TextFontFamily { get; set; } = "Consolas";
    public bool EnableMarkdownRendering { get; set; } = true;
    public bool EnableSyntaxHighlighting { get; set; } = true;

    // 影片預覽
    public bool VideoAutoPlay { get; set; } = true;
    public bool VideoMuted { get; set; } = true;

    // 壓縮檔預覽
    public bool ArchiveAutoExpand { get; set; } = true;

    // 啟動設定
    public bool StartWithWindows { get; set; } = false;

    // 預設值
    public static AppSettings Default => new();
}
