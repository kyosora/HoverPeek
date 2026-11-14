using SkiaSharp;

namespace HoverPeek.Core.Imaging;

/// <summary>
/// 逐格解碼 GIF 動畫，一次只在記憶體裡放一格。
///
/// 取代 WpfAnimatedGif：它會在 UI 執行緒上一次解碼並合成所有影格，
/// 記憶體 = 影格數 × 寬 × 高 × 4。實測 1920×1080、300 格的 GIF（檔案僅 2.3MB）
/// 卡住 UI 19.9 秒、吃掉 7.6GB（2026-11-14）。這裡改成背景逐格解碼、縮到顯示尺寸，
/// 記憶體與影格數無關。
///
/// 不是執行緒安全的：同一個實例只能由一條執行緒依序呼叫 DecodeNext。
/// </summary>
public sealed class GifFrameStreamer : IDisposable
{
    /// <summary>瀏覽器慣例：0 或極短的影格延遲當成 100ms，否則有些 GIF 會快得像閃爍。</summary>
    private const int MinFrameDurationMs = 20;
    private const int DefaultFrameDurationMs = 100;

    private readonly SKData _data;
    private readonly SKCodec _codec;
    private readonly SKCodecFrameInfo[] _frames;
    private readonly SKBitmap _canvas;
    private readonly SKBitmap _scaled;
    private readonly HashSet<int> _framesToKeep;
    private readonly Dictionary<int, SKBitmap> _keptFrames = new();
    private int _next;

    private GifFrameStreamer(SKData data, SKCodec codec, SKCodecFrameInfo[] frames, int width, int height)
    {
        _data = data;
        _codec = codec;
        _frames = frames;

        var full = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _canvas = new SKBitmap(full);
        _scaled = new SKBitmap(full.WithSize(width, height));

        // 大多數影格只依賴前一格（畫布上就是它）；依賴更早影格的（disposal = restore previous），
        // 那一格解完要另外存一份。通常只有零到幾格。
        _framesToKeep = frames
            .Select((f, i) => (f.RequiredFrame, i))
            .Where(x => x.RequiredFrame >= 0 && x.RequiredFrame != x.i - 1)
            .Select(x => x.RequiredFrame)
            .ToHashSet();
    }

    public int FrameCount => _frames.Length;
    public int Width => _scaled.Width;
    public int Height => _scaled.Height;
    public int Stride => _scaled.RowBytes;

    /// <summary>不是 GIF、或只有一格（不需要動畫）時回傳 null。</summary>
    public static GifFrameStreamer? TryCreate(byte[] gifBytes, int maxPhysicalPixels)
    {
        ArgumentNullException.ThrowIfNull(gifBytes);
        if (!SkiaImageDecoder.IsGif(gifBytes))
            return null;

        var data = SKData.CreateCopy(gifBytes);
        var codec = SKCodec.Create(data);
        var frames = codec?.FrameInfo ?? Array.Empty<SKCodecFrameInfo>();
        if (codec == null || frames.Length < 2)
        {
            codec?.Dispose();
            data.Dispose();
            return null;
        }

        var (w, h) = ImageMath.FitWithin(codec.Info.Width, codec.Info.Height, maxPhysicalPixels);
        return new GifFrameStreamer(data, codec, frames, w, h);
    }

    /// <summary>
    /// 解下一格（播完自動從頭開始），縮放後的 BGRA 像素寫進 destination，回傳這一格該停留的毫秒數。
    /// destination 長度至少要 Stride × Height。
    /// </summary>
    public int DecodeNext(byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Length < Stride * Height)
            throw new ArgumentException($"緩衝區太小：需要 {Stride * Height} bytes，收到 {destination.Length}", nameof(destination));

        var index = _next;
        _next = (_next + 1) % _frames.Length;
        var frame = _frames[index];

        PrepareCanvas(index, frame.RequiredFrame);
        var result = _codec.GetPixels(_canvas.Info, _canvas.GetPixels(), new SKCodecOptions(index, frame.RequiredFrame));
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new InvalidOperationException($"GIF 第 {index} 格解碼失敗：{result}");

        if (_framesToKeep.Contains(index))
            _keptFrames[index] = _canvas.Copy();

        if (!_canvas.ScalePixels(_scaled, SKFilterQuality.Medium))
            throw new InvalidOperationException($"GIF 第 {index} 格縮放失敗");
        _scaled.GetPixelSpan().CopyTo(destination);

        return frame.Duration < MinFrameDurationMs ? DefaultFrameDurationMs : frame.Duration;
    }

    /// <summary>讓畫布上是這一格所依賴的影格（或清空）。</summary>
    private void PrepareCanvas(int index, int requiredFrame)
    {
        if (requiredFrame < 0)
        {
            _canvas.Erase(SKColors.Transparent);
        }
        else if (requiredFrame != index - 1)
        {
            if (!_keptFrames.TryGetValue(requiredFrame, out var kept))
                throw new InvalidOperationException($"GIF 第 {index} 格需要第 {requiredFrame} 格，但它沒有被保留");
            kept.CopyTo(_canvas);
        }
        // requiredFrame == index - 1：畫布上就是上一格，直接疊上去
    }

    public void Dispose()
    {
        foreach (var kept in _keptFrames.Values)
            kept.Dispose();
        _scaled.Dispose();
        _canvas.Dispose();
        _codec.Dispose();
        _data.Dispose();
    }
}
