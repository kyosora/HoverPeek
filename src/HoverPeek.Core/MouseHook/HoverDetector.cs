namespace HoverPeek.Core.MouseHook;

/// <summary>
/// 偵測滑鼠是否在同一區域停留超過閾值時間。
/// 允許小幅度抖動（人手不可能完全靜止）。
/// </summary>
public sealed class HoverDetector : IDisposable
{
    private readonly int _hoverThresholdMs;
    private readonly int _jitterTolerancePx;
    private readonly GlobalMouseHook _mouseHook;

    private CancellationTokenSource? _cts;
    private int _anchorX;
    private int _anchorY;
    private bool _hoverFired;
    private bool _disposed;

    public event Action<int, int>? HoverStarted;
    public event Action? HoverEnded;

    public HoverDetector(
        GlobalMouseHook mouseHook,
        int hoverThresholdMs = 800,
        int jitterTolerancePx = 8)
    {
        _mouseHook = mouseHook ?? throw new ArgumentNullException(nameof(mouseHook));
        _hoverThresholdMs = hoverThresholdMs;
        _jitterTolerancePx = jitterTolerancePx;

        _mouseHook.MouseMoved += OnMouseMoved;
    }

    private void OnMouseMoved(int x, int y)
    {
        var dx = Math.Abs(x - _anchorX);
        var dy = Math.Abs(y - _anchorY);

        if (dx > _jitterTolerancePx || dy > _jitterTolerancePx)
        {
            _anchorX = x;
            _anchorY = y;

            var wasHovering = _hoverFired;
            _hoverFired = false;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var token = _cts.Token;
            _ = FireAfterDelay(x, y, token);

            if (wasHovering)
            {
                HoverEnded?.Invoke();
            }
        }
    }

    private async Task FireAfterDelay(int x, int y, CancellationToken ct)
    {
        try
        {
            await Task.Delay(_hoverThresholdMs, ct);
            if (!ct.IsCancellationRequested)
            {
                _hoverFired = true;
                HoverStarted?.Invoke(x, y);
            }
        }
        catch (TaskCanceledException)
        {
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
