using System.Collections.Concurrent;

namespace HoverPeek.Core.Threading;

/// <summary>
/// 一條常駐的 STA 背景執行緒，依序執行排入的工作。
/// Shell 縮圖處理器（IShellItemImageFactory 背後的 thumbnail handler）有些只在 STA 正常運作，
/// 放在執行緒池（MTA）上偶爾會拿不到縮圖或卡住；集中在這條執行緒上也避免每次重新初始化 COM。
/// </summary>
public sealed class StaWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private bool _disposed;

    public StaWorker(string name)
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = name,
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (ct.IsCancellationRequested)
        {
            tcs.SetCanceled(ct);
            return tcs.Task;
        }

        _queue.Add(() =>
        {
            // 排隊期間已經取消的就不做，避免滑鼠掃過一排檔案時堆積工作
            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                return;
            }

            try
            {
                tcs.TrySetResult(work());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
            work();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _queue.CompleteAdding();
    }
}
