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
        var truncated = truncatedBySize || truncatedByLines;

        var extension = Path.GetExtension(path);
        var isMarkdown = request.RenderMarkdown && MarkdownExtensions.Contains(extension);
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
            content += "\n\n" + Strings.Format("TruncatedMessage", request.TextMaxLines, CountLinesApprox(decoded.Text));

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
