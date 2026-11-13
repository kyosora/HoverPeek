namespace HoverPeek.Core.Preview;

public sealed class FolderPreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> ImageExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".ico", ".tiff", ".tif", ".svg", ".avif"
    };

    private const int MaxEntries = 500;

    public bool CanHandle(string filePath) => Directory.Exists(filePath);

    public Task<PreviewResult> GeneratePreviewAsync(PreviewRequest request, CancellationToken ct = default)
    {
        var filePath = request.Path;
        return Task.Run(() =>
        {
            try
            {
                var dir = new DirectoryInfo(filePath);
                var items = dir.EnumerateFileSystemInfos()
                    .OrderBy(f => f is not DirectoryInfo)
                    .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxEntries);

                var entries = new List<ArchiveEntry>();
                foreach (var item in items)
                {
                    ct.ThrowIfCancellationRequested();

                    var isDir = item is DirectoryInfo;
                    entries.Add(new ArchiveEntry
                    {
                        FullPath = item.FullName,
                        Name = item.Name,
                        Size = isDir ? 0 : ((FileInfo)item).Length,
                        IsDirectory = isDir,
                        IsImage = !isDir && ImageExtensions.Contains(item.Extension),
                        LastModified = item.LastWriteTime
                    });
                }

                return new PreviewResult
                {
                    Kind = PreviewKind.Folder,
                    Entries = entries,
                    SourcePath = filePath
                };
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // 沒權限或磁碟離線：顯示原因，不要裝作是不支援的格式
                return PreviewResult.FromException(filePath, ex);
            }
        }, ct);
    }
}
