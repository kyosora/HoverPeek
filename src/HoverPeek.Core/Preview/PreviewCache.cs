namespace HoverPeek.Core.Preview;

/// <summary>
/// 最近用過的預覽結果，同時限制筆數與總位元組數（LRU）。
/// Key 含檔案大小與修改時間，檔案被改過就自然失效；
/// 也含解碼參數（像素上限、GIF 動畫開關…），換螢幕或改設定不會拿到錯尺寸的舊圖。
/// </summary>
public sealed class PreviewCache
{
    public readonly record struct Key(
        string Path, long Length, DateTime LastWriteUtc,
        int MaxImagePixels, bool AnimateGif, long TextMaxBytes, int TextMaxLines, bool RenderMarkdown);

    private readonly int _maxEntries;
    private readonly long _maxBytes;
    private readonly object _lock = new();
    private readonly LinkedList<(Key Key, PreviewResult Value, long Cost)> _order = new();
    private readonly Dictionary<Key, LinkedListNode<(Key Key, PreviewResult Value, long Cost)>> _map = new();
    private long _totalBytes;

    public PreviewCache(int maxEntries = 24, long maxBytes = 160L * 1024 * 1024)
    {
        if (maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries), $"maxEntries 必須為正數，收到 {maxEntries}");
        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), $"maxBytes 必須為正數，收到 {maxBytes}");
        _maxEntries = maxEntries;
        _maxBytes = maxBytes;
    }

    public int Count
    {
        get { lock (_lock) return _map.Count; }
    }

    public long TotalBytes
    {
        get { lock (_lock) return _totalBytes; }
    }

    /// <summary>依檔案目前狀態建 key；檔案已不存在時回傳 null（不該快取）。</summary>
    public static Key? KeyFor(PreviewRequest request)
    {
        FileSystemInfo info;
        try
        {
            info = Directory.Exists(request.Path) ? new DirectoryInfo(request.Path) : new FileInfo(request.Path);
            if (!info.Exists)
                return null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }

        var length = info is FileInfo f ? f.Length : 0;
        return new Key(
            request.Path.ToUpperInvariant(), length, info.LastWriteTimeUtc,
            request.MaxImagePixels, request.AnimateGif, request.TextMaxBytes, request.TextMaxLines, request.RenderMarkdown);
    }

    public bool TryGet(Key key, out PreviewResult result)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                result = node.Value.Value;
                return true;
            }
        }

        result = null!;
        return false;
    }

    public void Set(Key key, PreviewResult value)
    {
        // 錯誤與占位圖不快取：檔案可能等一下就能讀，占位圖很快會被清晰版取代
        if (value.Kind is PreviewKind.Error || value.IsPlaceholder)
            return;

        var cost = EstimateCost(value);
        if (cost > _maxBytes)
            return; // 單筆就超過總上限，放進來只會把其他全擠掉

        lock (_lock)
        {
            if (_map.TryGetValue(key, out var existing))
                RemoveNode(existing);

            var node = _order.AddFirst((key, value, cost));
            _map[key] = node;
            _totalBytes += cost;

            while (_map.Count > _maxEntries || _totalBytes > _maxBytes)
                RemoveNode(_order.Last!);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _order.Clear();
            _map.Clear();
            _totalBytes = 0;
        }
    }

    private void RemoveNode(LinkedListNode<(Key Key, PreviewResult Value, long Cost)> node)
    {
        _order.Remove(node);
        _map.Remove(node.Value.Key);
        _totalBytes -= node.Value.Cost;
    }

    internal static long EstimateCost(PreviewResult r) =>
        1024 // 物件本身與小欄位
        + (r.Image?.ByteCount ?? 0)
        + (r.AnimatedGifData?.LongLength ?? 0)
        + 2L * ((r.TextContent?.Length ?? 0) + (r.MarkdownHtml?.Length ?? 0))
        + 256L * (r.Entries?.Count ?? 0);
}
