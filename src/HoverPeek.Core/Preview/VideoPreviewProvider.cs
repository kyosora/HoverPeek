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

    public Task<PreviewResult> GeneratePreviewAsync(PreviewRequest request, CancellationToken ct = default)
    {
        var filePath = request.Path;
        var info = new FileInfo(filePath);
        if (!info.Exists)
            throw new FileNotFoundException(Localization.Strings.Format("FileNotFound", filePath), filePath);

        var result = new PreviewResult
        {
            Kind = PreviewKind.Video,
            VideoFilePath = filePath,
            SourcePath = filePath,
            FileSize = info.Length,
        };

        return Task.FromResult(result);
    }
}
