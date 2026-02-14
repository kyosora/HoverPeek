using SharpCompress.Archives;

namespace HoverPeek.Core.Preview;

public sealed class ArchivePreviewProvider : IPreviewProvider
{
    private static readonly HashSet<string> SupportedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz",
        ".bz2", ".xz", ".lz", ".lzma"
    };

    private static readonly HashSet<string> ImageExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".ico", ".tiff", ".tif", ".svg", ".avif"
    };

    private readonly ImagePreviewProvider _imageProvider;

    public ArchivePreviewProvider(ImagePreviewProvider imageProvider)
    {
        _imageProvider = imageProvider ?? throw new ArgumentNullException(nameof(imageProvider));
    }

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public Task<PreviewResult> GeneratePreviewAsync(
        string filePath, CancellationToken ct = default)
    {
        try
        {
            using var archive = ArchiveFactory.Open(filePath);

            var entries = archive.Entries
                .Where(e => !e.IsDirectory || HasChildren(e, archive))
                .Select(e => new ArchiveEntry
                {
                    FullPath = e.Key ?? "",
                    Name = Path.GetFileName(e.Key ?? ""),
                    Size = e.Size,
                    IsDirectory = e.IsDirectory,
                    IsImage = ImageExtensions.Contains(
                        Path.GetExtension(e.Key ?? "")),
                    LastModified = e.LastModifiedTime
                })
                .OrderBy(e => e.FullPath)
                .ToList();

            var result = new PreviewResult
            {
                Kind = PreviewKind.Archive,
                Entries = entries
            };

            return Task.FromResult(result);
        }
        catch
        {
            return Task.FromResult(new PreviewResult { Kind = PreviewKind.Unsupported });
        }
    }

    /// <summary>
    /// 從壓縮包中提取特定圖片並產生預覽。
    /// 不解壓整個檔案，只讀取目標 entry 的串流。
    /// </summary>
    public PreviewResult PreviewImageInArchive(
        string archivePath, string entryPath)
    {
        try
        {
            using var archive = ArchiveFactory.Open(archivePath);
            var entry = archive.Entries
                .FirstOrDefault(e =>
                    string.Equals(e.Key, entryPath,
                        StringComparison.OrdinalIgnoreCase));

            if (entry == null || entry.IsDirectory)
                return new PreviewResult { Kind = PreviewKind.Unsupported };

            using var stream = entry.OpenEntryStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            return _imageProvider.GenerateFromBytes(ms.ToArray());
        }
        catch
        {
            return new PreviewResult { Kind = PreviewKind.Unsupported };
        }
    }

    private static bool HasChildren(IArchiveEntry dir, IArchive archive)
    {
        if (!dir.IsDirectory)
            return false;

        var prefix = dir.Key ?? "";
        return archive.Entries.Any(e =>
            !e.IsDirectory &&
            (e.Key ?? "").StartsWith(prefix,
                StringComparison.OrdinalIgnoreCase));
    }
}
