using System.Collections.Concurrent;
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
        // 壓縮檔內的圖只能走 Skia（系統縮圖需要實體檔案路徑），所以不列 SVG／AVIF／HEIC
        ".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".webp",
        ".ico", ".tiff", ".tif"
    };

    private const int MaxEntries = 2000;

    private readonly ImagePreviewProvider _imageProvider;

    // 輕量快取：只保留最後一個壓縮包的圖片解壓結果，避免 mouse move 時重複開檔
    private readonly object _cacheLock = new();
    private string? _cachedArchivePath;
    private readonly ConcurrentDictionary<string, PreviewResult> _imageCache = new();
    private const int MaxCachedImages = 8;

    public ArchivePreviewProvider(ImagePreviewProvider imageProvider)
    {
        _imageProvider = imageProvider ?? throw new ArgumentNullException(nameof(imageProvider));
    }

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public Task<PreviewResult> GeneratePreviewAsync(PreviewRequest request, CancellationToken ct = default)
    {
        var filePath = request.Path;
        return Task.Run(() =>
        {
            try
            {
                using var archive = ArchiveFactory.Open(filePath);

                // 先收集所有非目錄 entry 的路徑前綴，用來快速判斷目錄是否有子項
                var filePrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in archive.Entries)
                {
                    if (!e.IsDirectory && e.Key != null)
                    {
                        // 把每一層父路徑都加進去
                        var parent = Path.GetDirectoryName(e.Key)?.Replace('\\', '/');
                        while (!string.IsNullOrEmpty(parent))
                        {
                            if (!filePrefixes.Add(parent + "/"))
                                break; // 已存在，更上層也已經加過了
                            parent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                        }
                    }
                }

                var count = 0;
                var entries = new List<ArchiveEntry>();

                foreach (var e in archive.Entries.OrderBy(e => e.Key))
                {
                    ct.ThrowIfCancellationRequested();

                    if (count >= MaxEntries)
                        break;

                    // 跳過沒有子項的空目錄
                    if (e.IsDirectory)
                    {
                        var dirKey = (e.Key ?? "").Replace('\\', '/');
                        if (!dirKey.EndsWith("/"))
                            dirKey += "/";
                        if (!filePrefixes.Contains(dirKey))
                            continue;
                    }

                    entries.Add(new ArchiveEntry
                    {
                        FullPath = e.Key ?? "",
                        Name = Path.GetFileName(e.Key ?? ""),
                        Size = e.Size,
                        IsDirectory = e.IsDirectory,
                        IsImage = ImageExtensions.Contains(
                            Path.GetExtension(e.Key ?? "")),
                        LastModified = e.LastModifiedTime
                    });

                    count++;
                }

                // 切換壓縮包時清掉圖片快取
                InvalidateCacheIfNeeded(filePath);

                return new PreviewResult
                {
                    Kind = PreviewKind.Archive,
                    Entries = entries,
                    SourcePath = filePath,
                    FileSize = new FileInfo(filePath).Length,
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 加密、損毀或格式不支援：把原因顯示出來
                return PreviewResult.FromException(filePath, ex);
            }
        }, ct);
    }

    /// <summary>
    /// 從壓縮包中提取特定圖片並產生預覽。
    /// 帶有輕量快取，避免滑鼠移動時重複解壓。
    /// </summary>
    public PreviewResult PreviewImageInArchive(
        string archivePath, string entryPath, PreviewRequest template)
    {
        InvalidateCacheIfNeeded(archivePath);

        if (_imageCache.TryGetValue(entryPath, out var cached))
            return cached;

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

            var result = _imageProvider.GenerateFromBytes(ms.ToArray(), template with { Path = entryPath });

            // 快取滿了就全清，保持記憶體用量小
            if (_imageCache.Count >= MaxCachedImages)
                _imageCache.Clear();

            _imageCache.TryAdd(entryPath, result);
            return result;
        }
        catch (Exception ex)
        {
            return PreviewResult.FromException(entryPath, ex);
        }
    }

    private void InvalidateCacheIfNeeded(string archivePath)
    {
        lock (_cacheLock)
        {
            if (!string.Equals(_cachedArchivePath, archivePath, StringComparison.OrdinalIgnoreCase))
            {
                _cachedArchivePath = archivePath;
                _imageCache.Clear();
            }
        }
    }
}
