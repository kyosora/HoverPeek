using System.Windows.Threading;

namespace HoverPeek.Core.MouseHook;

/// <summary>
/// 在專屬執行緒上安裝低階滑鼠 hook 並跑訊息迴圈。
///
/// 為什麼不放在 UI 執行緒：WH_MOUSE_LL 的回呼在「安裝 hook 的執行緒」上執行，
/// Windows 每次滑鼠移動都要等它返回。UI 執行緒一卡（例如排版一個超長的文字行），
/// 整個系統的滑鼠就跟著頓；逾時太多次 Windows 還會直接移除 hook，之後預覽就再也不會出現。
/// </summary>
public sealed class MouseHookThread : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly GlobalMouseHook _hook;
    private Dispatcher? _dispatcher;
    private bool _disposed;

    public MouseHookThread(GlobalMouseHook hook)
    {
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
    }

    /// <summary>啟動執行緒並等 hook 安裝完成；安裝失敗會把原因丟回呼叫端。</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_dispatcher != null)
            throw new InvalidOperationException("滑鼠 hook 執行緒已經啟動過了");

        var ready = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                _hook.Install();
                _dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                ready.SetResult(ex);
                return;
            }

            ready.SetResult(null);
            Dispatcher.Run(); // 訊息迴圈：LL hook 的回呼靠它派送
        })
        {
            IsBackground = true,
            Name = "HoverPeek.MouseHook",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!ready.Task.Wait(StartTimeout))
            throw new TimeoutException($"滑鼠 hook 執行緒 {StartTimeout.TotalSeconds:0} 秒內沒有完成安裝");
        if (ready.Task.Result is { } error)
            throw new InvalidOperationException($"滑鼠 hook 安裝失敗：{error.Message}", error);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        var dispatcher = _dispatcher;
        if (dispatcher == null)
            return;

        // 解除 hook 要在安裝它的執行緒上做，再結束訊息迴圈
        dispatcher.Invoke(_hook.Dispose);
        dispatcher.InvokeShutdown();
    }
}
