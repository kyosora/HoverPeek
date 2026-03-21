using HoverPeek.Core.Localization;
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

    private long _maxFileSizeBytes;
    private int _maxPreviewLines;

    public TextPreviewProvider(long maxFileSizeBytes = 1024 * 1024, int maxPreviewLines = 1000)
    {
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxPreviewLines = maxPreviewLines;
    }

    public void UpdateSettings(long maxFileSize, int maxLines)
    {
        _maxFileSizeBytes = maxFileSize;
        _maxPreviewLines = maxLines;
    }

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public Task<PreviewResult> GeneratePreviewAsync(
        string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(Strings.Format("FileNotFound", filePath));
                }

                var fileInfo = new FileInfo(filePath);

                // 檢查檔案大小
                if (fileInfo.Length > _maxFileSizeBytes)
                {
                    return new PreviewResult
                    {
                        Kind = PreviewKind.Text,
                        TextContent = $"{Strings.FileTooLargeTitle}\n\n{Strings.Format("FileSizeLabel", FormatFileSize(fileInfo.Length))}\n{Strings.Format("MaxSizeLabel", FormatFileSize(_maxFileSizeBytes))}",
                        TextEncoding = "N/A",
                        FileExtension = Path.GetExtension(filePath)
                    };
                }

                // 嘗試讀取文字內容
                string content;
                string encodingName;

                try
                {
                    content = File.ReadAllText(filePath, Encoding.UTF8);
                    encodingName = "UTF-8";
                }
                catch
                {
                    try
                    {
                        content = File.ReadAllText(filePath, Encoding.Default);
                        encodingName = Encoding.Default.EncodingName;
                    }
                    catch
                    {
                        var big5 = Encoding.GetEncoding("big5");
                        content = File.ReadAllText(filePath, big5);
                        encodingName = "Big5";
                    }
                }

                ct.ThrowIfCancellationRequested();

                // 限制行數
                var lines = content.Split('\n');
                if (lines.Length > _maxPreviewLines)
                {
                    var truncatedLines = lines.Take(_maxPreviewLines).ToArray();
                    content = string.Join('\n', truncatedLines);
                    content += $"\n\n{Strings.Format("TruncatedMessage", _maxPreviewLines, lines.Length)}";
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
                        var pipeline = new MarkdownPipelineBuilder()
                            .UseAdvancedExtensions()
                            .Build();
                        markdownHtml = Markdown.ToHtml(content, pipeline);
                    }
                    catch
                    {
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new Exception(Strings.Format("ReadTextFailed", filePath), ex);
            }
        }, ct);
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
