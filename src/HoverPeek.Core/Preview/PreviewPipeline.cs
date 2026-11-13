using System.Collections.Concurrent;
using HoverPeek.Core.FileResolver;
using HoverPeek.Core.Imaging;
using HoverPeek.Core.Interop;

namespace HoverPeek.Core.Preview;

/// <summary>
/// 預覽管線：滑鼠一停穩就開始找檔案、解碼，懸停時間到時多半已經算好。
///
/// 設計重點：
/// - 同一份檔案（同一組解碼參數）同時只解一次，結果進 LRU 快取
/// - Session 取消只代表「不再等」，已開始的解碼照樣跑完並寫入快取；
///   滑鼠在同一個檔案上微幅移動時，下一個 session 直接撿現成的
/// - 解碼同時最多 2 個，滑鼠掃過一排檔案時不會把 CPU 吃滿
/// </summary>
public sealed class PreviewPipeline : IDisposable
{
    private readonly ExplorerFileResolver _resolver;
    private readonly ShellThumbnailProvider _shell;
    private readonly PreviewCache _cache;
    private readonly IReadOnlyList<IPreviewProvider> _providers;
    // Lazy 保證同一個 key 只啟動一次解碼（GetOrAdd 在競爭時可能呼叫 factory 多次）
    private readonly ConcurrentDictionary<PreviewCache.Key, Lazy<Task<PreviewResult>>> _inFlight = new();
    private readonly SemaphoreSlim _decodeSlots = new(2, 2);
    private volatile PreviewOptions _options = new();

    /// <param name="providers">依優先順序排列，第一個 CanHandle 的負責。</param>
    public PreviewPipeline(
        ExplorerFileResolver resolver,
        ShellThumbnailProvider shell,
        PreviewCache cache,
        IReadOnlyList<IPreviewProvider> providers)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    public PreviewOptions Options
    {
        get => _options;
        set
        {
            _options = value ?? throw new ArgumentNullException(nameof(value));
            _cache.Clear(); // 解碼參數變了，舊結果尺寸不對
        }
    }

    /// <summary>滑鼠停在 (x, y)：開始準備預覽，回傳的 session 可隨時取消。</summary>
    public PreviewSession Start(int x, int y)
    {
        var options = _options;
        var dpiScale = DisplayInfo.GetDpiScaleAt(x, y);
        var cts = new CancellationTokenSource();
        var ct = cts.Token;

        var pathTask = Task.Run(() => _resolver.ResolveFileAtPoint(x, y), ct);
        var fullTask = RunFullAsync(pathTask, options, dpiScale, ct);
        var quickTask = options.ShowQuickThumbnail
            ? RunQuickAsync(pathTask, fullTask, options, dpiScale, ct)
            : Task.FromResult<PreviewResult?>(null);

        return new PreviewSession(x, y, dpiScale, pathTask, quickTask, fullTask, cts);
    }

    /// <summary>給資料夾／壓縮檔清單裡「雙擊開啟」用：已知路徑，直接產生預覽。</summary>
    public Task<PreviewResult> PreviewPathAsync(string path, double dpiScale, CancellationToken ct = default)
    {
        var request = BuildRequest(path, _options, dpiScale);
        var provider = SelectProvider(path);
        return provider == null
            ? Task.FromResult(PreviewResult.Unsupported(path))
            : GetOrCreateAsync(request, provider, ct);
    }

    public PreviewRequest BuildRequest(string path, double dpiScale) => BuildRequest(path, _options, dpiScale);

    public IPreviewProvider? SelectProvider(string path) => _providers.FirstOrDefault(p => p.CanHandle(path));

    private async Task<PreviewResult?> RunFullAsync(
        Task<string?> pathTask, PreviewOptions options, double dpiScale, CancellationToken ct)
    {
        var path = await pathTask.ConfigureAwait(false);
        if (path == null)
            return null;

        var provider = SelectProvider(path);
        if (provider == null)
            return null;

        var request = BuildRequest(path, options, dpiScale);
        var result = await GetOrCreateAsync(request, provider, ct).ConfigureAwait(false);

        // 影片：附上系統縮圖當海報，VLC 起播前不會是一片黑
        if (result.Kind == PreviewKind.Video && result.Image == null)
        {
            var poster = await TryShellAsync(path, request, cacheOnly: true, ct).ConfigureAwait(false);
            if (poster != null)
                result = result with { Image = poster };
        }

        return result;
    }

    private async Task<PreviewResult?> RunQuickAsync(
        Task<string?> pathTask, Task<PreviewResult?> fullTask, PreviewOptions options, double dpiScale, CancellationToken ct)
    {
        var path = await pathTask.ConfigureAwait(false);
        if (path == null || fullTask.IsCompleted)
            return null;

        // 只對靜態圖做：GIF 要動畫、其他類型的縮圖只是圖示，沒幫助
        if (SelectProvider(path) is not ImagePreviewProvider || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            return null;

        var request = BuildRequest(path, options, dpiScale);
        if (PreviewCache.KeyFor(request) is { } key && _cache.TryGet(key, out _))
            return null; // 完整版已在快取裡，不需要占位

        var thumb = await TryShellAsync(path, request, cacheOnly: true, ct).ConfigureAwait(false);
        if (thumb == null)
            return null;

        return new PreviewResult
        {
            Kind = PreviewKind.Image,
            Image = thumb,
            ImageWidth = thumb.Width,
            ImageHeight = thumb.Height,
            IsPlaceholder = true,
            SourcePath = path,
        };
    }

    private async Task<DecodedImage?> TryShellAsync(string path, PreviewRequest request, bool cacheOnly, CancellationToken ct)
    {
        try
        {
            return await _shell.GetAsync(path, request.MaxImagePixels, request.DpiScale, cacheOnly, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null; // 縮圖只是加分項，失敗就當沒有
        }
    }

    private Task<PreviewResult> GetOrCreateAsync(PreviewRequest request, IPreviewProvider provider, CancellationToken ct)
    {
        var key = PreviewCache.KeyFor(request);
        if (key == null)
            return Task.FromResult(PreviewResult.FromException(
                request.Path, new FileNotFoundException(Localization.Strings.Format("FileNotFound", request.Path), request.Path)));

        if (_cache.TryGet(key.Value, out var cached))
            return Task.FromResult(cached);

        var shared = _inFlight.GetOrAdd(
            key.Value,
            k => new Lazy<Task<PreviewResult>>(() => GenerateAndCacheAsync(k, request, provider)));
        return shared.Value.WaitAsync(ct);
    }

    private async Task<PreviewResult> GenerateAndCacheAsync(PreviewCache.Key key, PreviewRequest request, IPreviewProvider provider)
    {
        await Task.Yield(); // 讓呼叫端先拿到 Task，不在呼叫執行緒上同步跑解碼
        await _decodeSlots.WaitAsync().ConfigureAwait(false);
        try
        {
            var result = await provider.GeneratePreviewAsync(request).ConfigureAwait(false);
            _cache.Set(key, result);
            return result;
        }
        catch (Exception ex)
        {
            return PreviewResult.FromException(request.Path, ex);
        }
        finally
        {
            _decodeSlots.Release();
            _inFlight.TryRemove(key, out _);
        }
    }

    private static PreviewRequest BuildRequest(string path, PreviewOptions options, double dpiScale) => new()
    {
        Path = path,
        MaxImagePixels = ImageMath.ToPhysicalPixels(options.MaxImageLogicalPixels, dpiScale),
        DpiScale = dpiScale,
        AnimateGif = options.AnimateGif,
        TextMaxBytes = options.TextMaxBytes,
        TextMaxLines = options.TextMaxLines,
        RenderMarkdown = options.RenderMarkdown,
    };

    public void Dispose()
    {
        _decodeSlots.Dispose();
    }
}

/// <summary>一次懸停的預覽準備工作。</summary>
public sealed class PreviewSession
{
    private readonly CancellationTokenSource _cts;

    internal PreviewSession(
        int x, int y, double dpiScale,
        Task<string?> path, Task<PreviewResult?> quick, Task<PreviewResult?> full,
        CancellationTokenSource cts)
    {
        X = x;
        Y = y;
        DpiScale = dpiScale;
        PathTask = path;
        QuickTask = quick;
        FullTask = full;
        _cts = cts;
    }

    public int X { get; }
    public int Y { get; }
    public double DpiScale { get; }

    /// <summary>游標下的檔案路徑；不是檔案總管裡的項目時為 null。</summary>
    public Task<string?> PathTask { get; }

    /// <summary>系統縮圖快取裡的低解析度占位圖；沒有時為 null。</summary>
    public Task<PreviewResult?> QuickTask { get; }

    /// <summary>完整預覽；無法預覽的路徑為 null，失敗時是 Kind == Error 的結果。</summary>
    public Task<PreviewResult?> FullTask { get; }

    public bool IsCanceled => _cts.IsCancellationRequested;

    public void Cancel()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>取回路徑；取消、失敗都當成「沒有檔案」。</summary>
    public async Task<string?> TryGetPathAsync()
    {
        try
        {
            return await PathTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
