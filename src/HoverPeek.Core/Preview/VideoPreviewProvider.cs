namespace HoverPeek.Core.Preview;

public sealed class VideoPreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mkv", ".mov", ".webm",
        ".wmv", ".flv", ".mpeg", ".mpg", ".ts",
        ".m4v", ".3gp", ".ogv", ".vob"
    };

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public Task<PreviewResult> GeneratePreviewAsync(
        string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"檔案不存在: {filePath}");
        }

        var result = new PreviewResult
        {
            Kind = PreviewKind.Video,
            VideoFilePath = filePath
        };

        return Task.FromResult(result);
    }
}
