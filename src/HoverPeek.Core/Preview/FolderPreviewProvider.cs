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

    public Task<PreviewResult> GeneratePreviewAsync(
        string filePath, CancellationToken ct = default)
    {
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
            catch (OperationCanceledException)
            {
                return new PreviewResult { Kind = PreviewKind.Unsupported };
            }
            catch
            {
                return new PreviewResult { Kind = PreviewKind.Unsupported };
            }
        }, ct);
    }
}
