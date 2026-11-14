using System.Windows.Threading;
using HoverPeek.App;
using HoverPeek.Core.Diagnostics;

namespace HoverPeek.Core.Tests;

public sealed class UiWatchdogTests
{
    [Fact]
    public void Constructor_RejectsThresholdShorterThanTwoPolls()
    {
        using var ui = new DispatcherThread();
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiWatchdog(ui.Dispatcher, () => null, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void FrozenUiThread_IsLoggedWithCurrentWork_ThenRecoveryIsLogged()
    {
        // 2026-11-14：UI 卡死時日誌一片空白，查不到是哪個檔案。這裡模擬 UI 卡 5 秒。
        var marker = $"watchdog-test-{Guid.NewGuid():N}";
        using var ui = new DispatcherThread();
        using var watchdog = new UiWatchdog(ui.Dispatcher, () => marker, TimeSpan.FromSeconds(2));

        Thread.Sleep(2500);
        Assert.DoesNotContain(marker, ReadLog()); // 正常期間不該誤報

        using var release = new ManualResetEventSlim();
        ui.Dispatcher.BeginInvoke(() => release.Wait(TimeSpan.FromSeconds(5)));
        Thread.Sleep(6500);

        var log = ReadLog();
        var stallAt = log.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(stallAt >= 0, "UI 卡住期間沒有寫出含目前預覽路徑的警告");
        Assert.Contains("恢復", log[stallAt..]);
    }

    private static string ReadLog() =>
        File.Exists(AppLog.LogFile)
            ? File.ReadAllText(AppLog.LogFile)
            : string.Empty;

    /// <summary>一條有訊息迴圈的 STA 執行緒，扮演 UI 執行緒。</summary>
    private sealed class DispatcherThread : IDisposable
    {
        private readonly Thread _thread;

        public DispatcherThread()
        {
            using var ready = new ManualResetEventSlim();
            Dispatcher? dispatcher = null;
            _thread = new Thread(() =>
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            })
            { IsBackground = true };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            ready.Wait();
            Dispatcher = dispatcher!;
        }

        public Dispatcher Dispatcher { get; }

        public void Dispose() => Dispatcher.InvokeShutdown();
    }
}
