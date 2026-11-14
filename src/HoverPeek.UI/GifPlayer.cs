using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HoverPeek.Core.Diagnostics;
using HoverPeek.Core.Imaging;

namespace HoverPeek.UI;

/// <summary>
/// 串流播放 GIF：背景執行緒逐格解碼（GifFrameStreamer），UI 執行緒每格只做一次 WritePixels。
/// UI 上的工作量固定是「一張顯示尺寸的圖」，與影格數、原始解析度無關。
/// </summary>
internal sealed class GifPlayer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private GifPlayer()
    {
    }

    /// <summary>
    /// 開始在 target 上播放。target 應先顯示第一格的靜態圖，播放器準備好後才換成動畫，
    /// 所以這段期間畫面不會空白。不是動畫（單格）時什麼都不做。
    /// </summary>
    public static GifPlayer Start(byte[] gifBytes, int maxPhysicalPixels, double dpiScale, Image target)
    {
        var player = new GifPlayer();
        _ = player.RunAsync(gifBytes, maxPhysicalPixels, dpiScale, target, target.Dispatcher);
        return player;
    }

    private async Task RunAsync(byte[] gifBytes, int maxPhysicalPixels, double dpiScale, Image target, Dispatcher ui)
    {
        var ct = _cts.Token;
        GifFrameStreamer? streamer = null;
        try
        {
            // 建立 codec 會掃過整個檔案取得影格資訊，放背景
            streamer = await Task.Run(() => GifFrameStreamer.TryCreate(gifBytes, maxPhysicalPixels), ct);
            if (streamer == null)
                return;

            var buffer = new byte[streamer.Stride * streamer.Height];
            var dpi = 96.0 * (dpiScale > 0 ? dpiScale : 1.0);
            WriteableBitmap? bitmap = null;
            var rect = new Int32Rect(0, 0, streamer.Width, streamer.Height);
            var clock = Stopwatch.StartNew();

            while (!ct.IsCancellationRequested)
            {
                clock.Restart();
                var s = streamer;
                var durationMs = await Task.Run(() => s.DecodeNext(buffer), ct);

                await ui.InvokeAsync(() =>
                {
                    if (ct.IsCancellationRequested)
                        return;
                    if (bitmap == null)
                    {
                        bitmap = new WriteableBitmap(rect.Width, rect.Height, dpi, dpi, PixelFormats.Pbgra32, null);
                        target.Source = bitmap;
                    }
                    bitmap.WritePixels(rect, buffer, s.Stride, 0);
                }, DispatcherPriority.Render, ct);

                var wait = durationMs - (int)clock.ElapsedMilliseconds;
                if (wait > 0)
                    await Task.Delay(wait, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 換了預覽或視窗關閉，正常結束
        }
        catch (Exception ex)
        {
            // 動畫壞掉就停在第一格的靜態圖，不影響其他預覽
            AppLog.Warn("GIF 動畫播放失敗，停在第一格", ex);
        }
        finally
        {
            streamer?.Dispose();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
    }
}
