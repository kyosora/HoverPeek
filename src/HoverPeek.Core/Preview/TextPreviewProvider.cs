using System.Text;
using Markdig;

namespace HoverPeek.Core.Preview;

public sealed class TextPreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // 純文字
        ".txt", ".md", ".log", ".rst",

        // 程式碼檔案
        ".cs", ".py", ".js", ".ts", ".jsx", ".tsx",
        ".java", ".go", ".rs", ".c", ".cpp", ".h", ".hpp",
        ".rb", ".php", ".swift", ".kt", ".scala",

        // 設定檔
        ".json", ".xml", ".yaml", ".yml", ".toml",
        ".config", ".ini", ".env", ".properties",

        // Web 相關
        ".html", ".htm", ".css", ".scss", ".sass", ".less",
        ".svg", ".vue",

        // 文件
        ".csv", ".tsv",

        // Shell/Script
        ".sh", ".bash", ".bat", ".ps1", ".cmd"
    };

    private readonly long _maxFileSizeBytes;
    private readonly int _maxPreviewLines;

    public TextPreviewProvider(long maxFileSizeBytes = 1024 * 1024, int maxPreviewLines = 1000)
    {
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxPreviewLines = maxPreviewLines;
    }

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public async Task<PreviewResult> GeneratePreviewAsync(
        string filePath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"檔案不存在: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);

            // 檢查檔案大小
            if (fileInfo.Length > _maxFileSizeBytes)
            {
                return new PreviewResult
                {
                    Kind = PreviewKind.Text,
                    TextContent = $"檔案過大無法預覽\n\n檔案大小: {FormatFileSize(fileInfo.Length)}\n最大支援: {FormatFileSize(_maxFileSizeBytes)}",
                    TextEncoding = "N/A",
                    FileExtension = Path.GetExtension(filePath)
                };
            }

            // 嘗試讀取文字內容
            string content;
            string encodingName;

            try
            {
                // 先嘗試 UTF-8
                content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
                encodingName = "UTF-8";
            }
            catch
            {
                try
                {
                    // UTF-8 失敗，嘗試系統預設編碼
                    content = await File.ReadAllTextAsync(filePath, Encoding.Default, ct);
                    encodingName = Encoding.Default.EncodingName;
                }
                catch
                {
                    // 嘗試 Big5（繁體中文常用）
                    var big5 = Encoding.GetEncoding("big5");
                    content = await File.ReadAllTextAsync(filePath, big5, ct);
                    encodingName = "Big5";
                }
            }

            // 限制行數（避免顯示超長內容）
            var lines = content.Split('\n');
            if (lines.Length > _maxPreviewLines)
            {
                var truncatedLines = lines.Take(_maxPreviewLines).ToArray();
                content = string.Join('\n', truncatedLines);
                content += $"\n\n... (已截斷，僅顯示前 {_maxPreviewLines} 行，共 {lines.Length} 行)";
            }

            // 檢查是否為 Markdown 檔案
            var extension = Path.GetExtension(filePath);
            var isMarkdown = extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
                             extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase);

            string? markdownHtml = null;
            if (isMarkdown)
            {
                try
                {
                    // 使用 Markdig 轉換 Markdown 為 HTML
                    var pipeline = new MarkdownPipelineBuilder()
                        .UseAdvancedExtensions()  // GitHub Flavored Markdown 擴展
                        .Build();
                    markdownHtml = Markdown.ToHtml(content, pipeline);
                }
                catch
                {
                    // Markdown 轉換失敗，fallback 到純文字顯示
                    isMarkdown = false;
                }
            }

            return new PreviewResult
            {
                Kind = PreviewKind.Text,
                TextContent = content,
                TextEncoding = encodingName,
                FileExtension = extension,
                IsMarkdown = isMarkdown,
                MarkdownHtml = markdownHtml
            };
        }
        catch (Exception ex)
        {
            throw new Exception($"讀取文字檔案失敗: {filePath}", ex);
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
