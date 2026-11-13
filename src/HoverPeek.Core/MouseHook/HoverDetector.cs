namespace HoverPeek.Core.MouseHook;

/// <summary>
/// 偵測滑鼠停留。允許小幅抖動（人手不可能完全靜止）。
///
/// 事件順序（同一個停留點）：
///   Settled（停穩 SettleMs 後）→ HoverStarted（停滿 hoverThreshold 後）
/// 移動超過容忍範圍時，若 HoverStarted 已觸發則送 HoverEnded，並重新計時。
///
/// Settled 用來提早開始準備預覽，等 HoverStarted 時多半已經算好。
/// 事件在執行緒池上觸發，訂閱端要自己切回 UI 執行緒。
/// </summary>
public sealed class HoverDetector : IDisposable
{
    public const int SettleMs = 60;

    private volatile int _hoverThresholdMs;
    private volatile int _jitterTolerancePx;
    private readonly GlobalMouseHook _mouseHook;

    private CancellationTokenSource? _cts;
    private int _anchorX = int.MinValue;
    private int _anchorY = int.MinValue;
    private volatile bool _hoverFired;
    private bool _disposed;

    public event Action<int, int>? Settled;
    public event Action<int, int>? HoverStarted;
    public event Action? HoverEnded;

    public HoverDetector(GlobalMouseHook mouseHook, int hoverThresholdMs = 400, int jitterTolerancePx = 8)
    {
        _mouseHook = mouseHook ?? throw new ArgumentNullException(nameof(mouseHook));
        UpdateSettings(hoverThresholdMs, jitterTolerancePx);
        _mouseHook.MouseMoved += OnMouseMoved;
    }

    public void UpdateSettings(int hoverThresholdMs, int jitterTolerancePx)
    {
        _hoverThresholdMs = Math.Max(SettleMs, hoverThresholdMs);
        _jitterTolerancePx = Math.Max(0, jitterTolerancePx);
    }

    // 由 hook 回呼呼叫（安裝 hook 的執行緒，也就是 UI 執行緒）；必須很快返回，否則整個系統的滑鼠會卡
    private void OnMouseMoved(int x, int y)
    {
        var tolerance = _jitterTolerancePx;
        if (Math.Abs(x - _anchorX) <= tolerance && Math.Abs(y - _anchorY) <= tolerance)
            return;

        _anchorX = x;
        _anchorY = y;

        var wasHovering = _hoverFired;
        _hoverFired = false;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = FireAfterDelayAsync(x, y, _cts.Token);

        if (wasHovering)
            HoverEnded?.Invoke();
    }

    private async Task FireAfterDelayAsync(int x, int y, CancellationToken ct)
    {
        try
        {
            await Task.Delay(SettleMs, ct).ConfigureAwait(false);
            Settled?.Invoke(x, y);

            await Task.Delay(_hoverThresholdMs - SettleMs, ct).ConfigureAwait(false);
            _hoverFired = true;
            HoverStarted?.Invoke(x, y);
        }
        catch (TaskCanceledException)
        {
            // 滑鼠又動了，正常流程
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _mouseHook.MouseMoved -= OnMouseMoved;
        _cts?.Cancel();
        _cts?.Dispose();
        _disposed = true;
    }
}
