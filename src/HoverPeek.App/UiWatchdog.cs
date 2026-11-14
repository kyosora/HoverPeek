using System.Diagnostics;
using System.Windows.Threading;
using HoverPeek.Core.Diagnostics;

namespace HoverPeek.App;

/// <summary>
/// 監看 UI 執行緒有沒有卡住。UI 一卡，程式自己什麼都寫不出來，
/// 所以由背景執行緒每秒排一個小工作給 UI，太久沒被執行就記錄「卡了多久、當時在預覽什麼」，
/// 恢復時再記一筆實際卡住的時間。2026-11-14 曾因一行 1MB 的 JSON 卡死且日誌一片空白，才加了這個。
/// </summary>
internal sealed class UiWatchdog : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly Dispatcher _ui;
    private readonly Func<string?> _describeCurrentWork;
    private readonly TimeSpan _threshold;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _cts = new();
    // 兩條執行緒共用，一律用 Interlocked／Volatile 讀寫（Stopwatch 本身不是執行緒安全的）
    private long _lastPongTimestamp = Stopwatch.GetTimestamp();
    private int _pingPending;
    private int _reported;

    /// <param name="describeCurrentWork">回傳目前在做什麼（例如預覽中的路徑），必須能在背景執行緒安全呼叫。</param>
    public UiWatchdog(Dispatcher ui, Func<string?> describeCurrentWork, TimeSpan threshold)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _describeCurrentWork = describeCurrentWork ?? throw new ArgumentNullException(nameof(describeCurrentWork));
        // 每秒才戳一次，正常狀況下距上次回應也常略超過 1 秒；門檻太短會誤報
        if (threshold < PollInterval * 2)
            throw new ArgumentOutOfRangeException(nameof(threshold), $"門檻至少要 {PollInterval * 2}，收到 {threshold}");
        _threshold = threshold;
        _thread = new Thread(Run) { IsBackground = true, Name = "HoverPeek.UiWatchdog" };
        _thread.Start();
    }

    private void Run()
    {
        while (!_cts.Token.WaitHandle.WaitOne(PollInterval))
        {
            // 上一個 ping 還沒被執行就不再排新的，避免卡住期間在 UI 佇列裡堆積
            if (Interlocked.Exchange(ref _pingPending, 1) == 0)
                _ui.BeginInvoke(DispatcherPriority.Normal, OnPong);

            var stalled = Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastPongTimestamp));
            if (stalled > _threshold && Interlocked.CompareExchange(ref _reported, 1, 0) == 0)
            {
                AppLog.Warn($"UI 執行緒已 {stalled.TotalSeconds:0.0} 秒沒有回應，目前預覽：{_describeCurrentWork() ?? "（無）"}");
            }
        }
    }

    private void OnPong()
    {
        var now = Stopwatch.GetTimestamp();
        var stalled = Stopwatch.GetElapsedTime(Interlocked.Exchange(ref _lastPongTimestamp, now), now);
        if (Interlocked.Exchange(ref _reported, 0) == 1)
            AppLog.Warn($"UI 執行緒恢復回應，這次卡住約 {stalled.TotalSeconds:0.0} 秒");

        Volatile.Write(ref _pingPending, 0);
    }

    public void Dispose()
    {
        // 只取消、不 Dispose：看門狗執行緒可能還在等這個 WaitHandle
        _cts.Cancel();
    }
}
