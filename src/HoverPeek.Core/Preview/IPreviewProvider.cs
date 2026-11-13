using HoverPeek.Core.Imaging;

namespace HoverPeek.Core.Preview;

public enum PreviewKind
{
    Image,
    Archive,
    Video,
    Text,
    Folder,
    Unsupported,
    Error
}

public sealed record PreviewResult
{
    public required PreviewKind Kind { get; init; }

    /// <summary>靜態圖：已解碼的像素，UI 直接顯示。</summary>
    public DecodedImage? Image { get; init; }

    /// <summary>GIF 動畫：保留原始位元組交給 WpfAnimatedGif。</summary>
    public byte[]? AnimatedGifData { get; init; }

    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
    public string? ImageFormat { get; init; }

    /// <summary>true = 這是先頂著用的低解析度縮圖，清晰版稍後送達。</summary>
    public bool IsPlaceholder { get; init; }

    public IReadOnlyList<ArchiveEntry>? Entries { get; init; }

    /// <summary>影片：檔案路徑交給 VLC；Image 可放系統縮圖當片頭海報。</summary>
    public string? VideoFilePath { get; init; }

    public string? TextContent { get; init; }
    public string? TextEncoding { get; init; }
    public string? FileExtension { get; init; }
    public bool IsMarkdown { get; init; }
    public string? MarkdownHtml { get; init; }
    public bool IsTruncated { get; init; }

    public string? SourcePath { get; init; }
    public long FileSize { get; init; }

    /// <summary>Kind == Error 時的訊息；Detail 放例外鏈，給想追問題的人看。</summary>
    public string? ErrorMessage { get; init; }
    public string? ErrorDetail { get; init; }

    public static PreviewResult Unsupported(string? path = null) =>
        new() { Kind = PreviewKind.Unsupported, SourcePath = path };

    public static PreviewResult FromException(string path, Exception ex)
    {
        var chain = new System.Text.StringBuilder();
        for (var e = ex; e != null; e = e.InnerException)
            chain.AppendLine($"{e.GetType().Name}: {e.Message}");

        return new PreviewResult
        {
            Kind = PreviewKind.Error,
            SourcePath = path,
            ErrorMessage = ex.GetBaseException().Message,
            ErrorDetail = chain.ToString().TrimEnd(),
        };
    }
}

public sealed record ArchiveEntry
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public required long Size { get; init; }
    public required bool IsDirectory { get; init; }
    public required bool IsImage { get; init; }
    public DateTime? LastModified { get; init; }
}

/// <summary>
/// 產生預覽時需要的參數。由管線依當下螢幕 DPI 與設定組好再傳入，
/// provider 本身不持有可變設定，方便多執行緒同時呼叫。
/// </summary>
public sealed record PreviewRequest
{
    public required string Path { get; init; }

    /// <summary>圖片長邊上限（實體像素，已乘上螢幕縮放比例）。</summary>
    public int MaxImagePixels { get; init; } = 800;
    public double DpiScale { get; init; } = 1.0;

    public bool AnimateGif { get; init; } = true;
    public long TextMaxBytes { get; init; } = 1024 * 1024;
    public int TextMaxLines { get; init; } = 1000;
    public bool RenderMarkdown { get; init; } = true;
}

public interface IPreviewProvider
{
    bool CanHandle(string filePath);
    Task<PreviewResult> GeneratePreviewAsync(PreviewRequest request, CancellationToken ct = default);
}
