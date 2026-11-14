using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HoverPeek.Core.Preview;
using HoverPeek.UI;
using LibVLCSharp.Shared;

// 用法：dotnet run -c Release --project tools/HoverPeek.StressRender -- <檔案...>
// 量每個檔案「背景處理」與「UI 執行緒」各花多少時間。UI 執行緒超過 500ms 就會讓人感覺卡頓，
// 超過 5 秒 Windows 會判定程式沒有回應。

internal static class Program
{
    private const double UiWarnMs = 500;

    [STAThread]
    private static int Main(string[] args)
    {
        // 手動結束：GIF 測試會開關視窗，預設的「最後一個視窗關閉就結束」會讓後面的 pack URI 失效
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var providers = new IPreviewProvider[]
        {
            new ArchivePreviewProvider(new ImagePreviewProvider()),
            new SvgPreviewProvider(),
            new ImagePreviewProvider(),
            new VideoPreviewProvider(),
            new TextPreviewProvider(),
            new FolderPreviewProvider(),
        };

        Console.WriteLine($"{"檔案",-44} {"類型",-6} {"背景",8} {"UI 執行緒",10} {"記憶體增量",10}   （GIF 的 UI 欄 = 播放 3 秒期間 UI 最長的卡頓）");
        foreach (var path in args)
        {
            var provider = providers.FirstOrDefault(p => p.CanHandle(path));
            if (provider == null)
            {
                Console.WriteLine($"{Short(path),-44} 不支援");
                continue;
            }

            var bg = Stopwatch.StartNew();
            var result = Task.Run(() => provider.GeneratePreviewAsync(new PreviewRequest { Path = path })).GetAwaiter().GetResult();
            bg.Stop();

            GC.Collect();
            var memBefore = Process.GetCurrentProcess().PrivateMemorySize64;
            var ui = result switch
            {
                { AnimatedGifData: not null } => MeasureGifPlayback(result),
                { Kind: PreviewKind.Video } => MeasureVideo(path),
                _ => MeasurePreviewWindow(result),
            };
            var memDelta = (Process.GetCurrentProcess().PrivateMemorySize64 - memBefore) / 1024.0 / 1024.0;

            var kind = result.AnimatedGifData != null ? "GIF動畫" : result.Kind.ToString();
            var flag = ui > UiWarnMs ? "  ← 卡頓" : "";
            Console.WriteLine($"{Short(path),-44} {kind,-6} {bg.ElapsedMilliseconds,6}ms {ui,8:0}ms {memDelta,8:0}MB{flag}");
        }

        return 0;
    }

    /// <summary>文字、圖片、清單：照 PreviewWindow 的流程填內容並完成一次排版與繪製（離屏）。</summary>
    private static double MeasurePreviewWindow(PreviewResult result)
    {
        var window = new PreviewWindow(new NullHost());
        var sw = Stopwatch.StartNew();
        Invoke(window, "ResetContent");
        var size = (Size)Invoke(window, "RenderContent", result)!;
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        root.UpdateLayout();
        var rtb = new RenderTargetBitmap(800, 600, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(root);
        sw.Stop();
        window.Close();
        return sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// GIF：照 PreviewWindow 的流程顯示（第一格 + GifPlayer 串流播放），讓它實際播 3 秒，
    /// 期間每 50ms 排一個小工作給 UI，回傳最長的等待時間 = 播放期間 UI 最久卡了多久。
    /// </summary>
    private static double MeasureGifPlayback(PreviewResult result)
    {
        var window = new PreviewWindow(new NullHost());
        Invoke(window, "ResetContent");
        Invoke(window, "RenderContent", result);

        var dispatcher = Dispatcher.CurrentDispatcher;
        var worst = 0.0;
        var frame = new DispatcherFrame();
        var until = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
        var lastTick = Stopwatch.StartNew();
        timer.Tick += (_, _) =>
        {
            worst = Math.Max(worst, lastTick.Elapsed.TotalMilliseconds - 50);
            lastTick.Restart();
            if (until.Elapsed > TimeSpan.FromSeconds(3))
            {
                timer.Stop();
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);

        Invoke(window, "ResetContent"); // 停止播放器
        window.Close();
        return Math.Max(0, worst);
    }

    private static LibVLC? _vlc;

    /// <summary>影片：量 PreviewWindow 在 UI 執行緒上呼叫的 Media 建立與 Play()。</summary>
    private static double MeasureVideo(string path)
    {
        LibVLCSharp.Shared.Core.Initialize();
        _vlc ??= new LibVLC("--vout=dummy", "--aout=dummy", "--quiet");
        using var player = new MediaPlayer(_vlc);
        var sw = Stopwatch.StartNew();
        using (var media = new Media(_vlc, path, FromType.FromPath))
        {
            media.AddOption(":no-audio");
            player.Play(media);
        }
        sw.Stop();
        Thread.Sleep(500);
        Task.Run(player.Stop).Wait(TimeSpan.FromSeconds(5));
        return sw.Elapsed.TotalMilliseconds;
    }

    private static object? Invoke(object target, string method, params object[] args)
    {
        var info = target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                   ?? throw new MissingMethodException(target.GetType().Name, method);
        return info.Invoke(target, args);
    }

    private static string Short(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length > 42 ? name[..41] + "…" : name;
    }

    private sealed class NullHost : IPreviewHost
    {
        public Task<PreviewResult> PreviewPathAsync(string path, double dpiScale) => Task.FromResult(PreviewResult.Unsupported(path));
        public Task<PreviewResult> PreviewArchiveEntryAsync(string a, string e, double d) => Task.FromResult(PreviewResult.Unsupported(e));
    }
}
