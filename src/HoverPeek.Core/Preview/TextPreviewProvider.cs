using HoverPeek.Core.Localization;
using HoverPeek.Core.Text;
using Markdig;

namespace HoverPeek.Core.Preview;

public sealed class TextPreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // 純文字
        ".txt", ".md", ".markdown", ".log", ".rst", ".nfo",

        // 程式碼
        ".cs", ".py", ".js", ".mjs", ".cjs", ".ts", ".jsx", ".tsx",
        ".java", ".go", ".rs", ".c", ".cpp", ".h", ".hpp",
        ".rb", ".php", ".swift", ".kt", ".scala", ".lua", ".sql", ".gd",

        // 設定檔
        ".json", ".jsonc", ".xml", ".yaml", ".yml", ".toml",
        ".config", ".ini", ".env", ".properties", ".editorconfig", ".gitignore",

        // Web
        ".html", ".htm", ".css", ".scss", ".sass", ".less", ".vue", ".svelte",

        // 表格
        ".csv", ".tsv",

        // Shell
        ".sh", ".bash", ".bat", ".ps1", ".psm1", ".cmd",

        // 專案檔
        ".csproj", ".sln", ".slnx", ".props", ".targets", ".xaml",
    };

    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown",
    };

    /// <summary>
    /// 每行最多顯示的字元數。壓縮過的 JSON／JS 常整份只有一行（實例：1MB 一行），
    /// AvalonEdit 與 TextBox 排版這種行會在 UI 執行緒卡上數十秒，讓整個程式被判定沒有回應。
    /// </summary>
    internal const int MaxLineChars = 2000;

    // Markdig 管線建一次就好，執行緒安全
    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public bool CanHandle(string filePath) => SupportedExtensions.Contains(Path.GetExtension(filePath));

    public Task<PreviewResult> GeneratePreviewAsync(PreviewRequest request, CancellationToken ct = default) =>
        Task.Run(() => Read(request, ct), ct);

    private static PreviewResult Read(PreviewRequest request, CancellationToken ct)
    {
        var path = request.Path;
        var fileSize = new FileInfo(path).Length;
        var byteLimit = (int)Math.Min(fileSize, Math.Max(1024, request.TextMaxBytes));
        var truncatedBySize = fileSize > byteLimit;

        var buffer = new byte[byteLimit];
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            stream.ReadExactly(buffer);

        ct.ThrowIfCancellationRequested();

        if (LooksBinary(buffer))
            return PreviewResult.Unsupported(path);

        var decoded = TextEncodingDetector.Decode(buffer, truncated: truncatedBySize);
        var (content, truncatedByLines) = LimitLines(decoded.Text, request.TextMaxLines);

        var extension = Path.GetExtension(path);
        var isMarkdown = request.RenderMarkdown && MarkdownExtensions.Contains(extension);

        // Markdown 交給 WebView2 排版，長行沒有問題，而且截斷會破壞語法；其他一律限制行長
        var truncatedByLineLength = false;
        if (!isMarkdown)
            (content, truncatedByLineLength) = LimitLineLength(content, MaxLineChars);

        var truncated = truncatedBySize || truncatedByLines || truncatedByLineLength;
        string? html = null;
        if (isMarkdown)
        {
            try
            {
                html = Markdown.ToHtml(content, MarkdownPipeline);
            }
            catch (Exception)
            {
                // Markdown 解析失敗時退回純文字顯示，內容仍然可讀
                isMarkdown = false;
            }
        }

        if (truncated)
            content += "\n\n" + DescribeTruncation(
                truncatedByLines, truncatedByLineLength, truncatedBySize,
                request.TextMaxLines, CountLinesApprox(decoded.Text), byteLimit, fileSize);

        return new PreviewResult
        {
            Kind = PreviewKind.Text,
            TextContent = content,
            TextEncoding = decoded.EncodingName,
            FileExtension = extension,
            IsMarkdown = isMarkdown,
            MarkdownHtml = html,
            IsTruncated = truncated,
            SourcePath = path,
            FileSize = fileSize,
        };
    }

    /// <summary>只取前 maxLines 行，不先 Split 整份字串。</summary>
    internal static (string Text, bool Truncated) LimitLines(string text, int maxLines)
    {
        if (maxLines <= 0)
            return (text, false);

        var index = -1;
        for (var line = 0; line < maxLines; line++)
        {
            index = text.IndexOf('\n', index + 1);
            if (index < 0)
                return (text, false);
        }

        return index + 1 >= text.Length ? (text, false) : (text[..index], true);
    }

    /// <summary>超過 maxChars 的行截斷並加上「 …」；沒有長行時原字串原樣回傳，不配置新字串。</summary>
    internal static (string Text, bool Truncated) LimitLineLength(string text, int maxChars)
    {
        if (maxChars <= 0 || text.Length <= maxChars)
            return (text, false);

        var lines = text.Split('\n');
        var cut = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > maxChars)
            {
                lines[i] = lines[i][..maxChars] + " …";
                cut = true;
            }
        }

        return cut ? (string.Join('\n', lines), true) : (text, false);
    }

    private static string DescribeTruncation(
        bool byLines, bool byLineLength, bool bySize, int maxLines, int totalLines, long loadedBytes, long fileSize)
    {
        var notes = new List<string>(3);
        if (byLines)
            notes.Add(Strings.Format("TruncatedMessage", maxLines, totalLines));
        if (byLineLength)
            notes.Add(Strings.Format("TruncatedLongLines", MaxLineChars));
        if (bySize)
            notes.Add(Strings.Format("TruncatedBySize", FormatSize(loadedBytes), FormatSize(fileSize)));
        return string.Join("\n", notes);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B",
    };

    private static int CountLinesApprox(string text) => text.AsSpan().Count('\n') + 1;

    /// <summary>
    /// 前 8KB 出現 NUL 就當成二進位檔（例如被誤命名成 .log 的 dump）。
    /// UTF-16 有 BOM 時例外，它本來就含大量 0x00。
    /// </summary>
    internal static bool LooksBinary(ReadOnlySpan<byte> bytes)
    {
        if (bytes is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..])
            return false;
        return bytes[..Math.Min(bytes.Length, 8192)].Contains((byte)0);
    }
}
