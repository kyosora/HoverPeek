using HoverPeek.Core.Settings;

namespace HoverPeek.Core.Preview;

/// <summary>從 AppSettings 抽出的預覽參數快照（不可變，可安全跨執行緒傳遞）。</summary>
public sealed record PreviewOptions
{
    /// <summary>圖片長邊上限，邏輯像素（DIP）；實際解碼時乘上螢幕縮放比例。</summary>
    public int MaxImageLogicalPixels { get; init; } = 800;
    public bool AnimateGif { get; init; } = true;
    public long TextMaxBytes { get; init; } = 1024 * 1024;
    public int TextMaxLines { get; init; } = 1000;
    public bool RenderMarkdown { get; init; } = true;
    public bool ShowQuickThumbnail { get; init; } = true;

    public static PreviewOptions From(AppSettings s) => new()
    {
        MaxImageLogicalPixels = s.ImageMaxDimension,
        AnimateGif = s.EnableGifAnimation,
        TextMaxBytes = s.TextMaxFileSizeMB * 1024 * 1024,
        TextMaxLines = s.TextMaxLines,
        RenderMarkdown = s.EnableMarkdownRendering,
        ShowQuickThumbnail = s.ShowQuickThumbnail,
    };
}
