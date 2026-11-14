using System.IO;
using System.Windows;
using System.Windows.Threading;
using HoverPeek.Core.Diagnostics;
using HoverPeek.Core.FileResolver;
using HoverPeek.Core.Imaging;
using HoverPeek.Core.Localization;
using HoverPeek.Core.MouseHook;
using HoverPeek.Core.Preview;
using HoverPeek.Core.Settings;
using HoverPeek.UI;
using Icon = System.Drawing.Icon;
using MessageBox = System.Windows.MessageBox;

namespace HoverPeek.App;

/// <summary>
/// 協調層：滑鼠事件 → 預覽管線 → 預覽視窗。
///
/// 時序：
///   停穩 60ms   → 開新 session，背景開始找檔案、解碼
///   懸停時間到  → 已算好就直接顯示；否則先顯示系統縮圖或「載入中」，完成後替換
///   滑鼠移開    → 延遲後重新確認游標下的檔案，仍是同一個就不關（在檔案上微動不會閃掉）
/// </summary>
public partial class App : System.Windows.Application, IPreviewHost
{
    private static readonly TimeSpan FullResultGrace = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan QuickResultGrace = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan InsideWindowRecheck = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ErrorDialogCooldown = TimeSpan.FromSeconds(30);

    private SettingsService _settingsService = null!;
    private GlobalMouseHook? _mouseHook;
    private MouseHookThread? _hookThread;
    private UiWatchdog? _watchdog;
    private HoverDetector? _hoverDetector;
    private ShellThumbnailProvider? _shell;
    private ArchivePreviewProvider _archiveProvider = null!;
    private PreviewPipeline _pipeline = null!;
    private PreviewWindow _previewWindow = null!;
    private TrayIconController? _tray;
    private SettingsWindow? _settingsWindow;

    // 收合判斷用的獨立 resolver：管線的 resolver 同時在背景跑，避免互相覆蓋快取
    private readonly ExplorerFileResolver _closeResolver = new();

    private PreviewSession? _session;
    private CancellationTokenSource? _closeCts;
    private DateTime _lastErrorDialog = DateTime.MinValue;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _settingsService = new SettingsService();
        var settings = _settingsService.Current;
        LocaleManager.SetLanguage(settings.Language);

        _shell = new ShellThumbnailProvider();
        var imageProvider = new ImagePreviewProvider(_shell);
        _archiveProvider = new ArchivePreviewProvider(imageProvider);

        // 順序即優先序：第一個 CanHandle 的負責
        var providers = new IPreviewProvider[]
        {
            _archiveProvider,
            new SvgPreviewProvider(),
            imageProvider,
            new VideoPreviewProvider(),
            new TextPreviewProvider(),
            new FolderPreviewProvider(),
        };
        _pipeline = new PreviewPipeline(new ExplorerFileResolver(), _shell, new PreviewCache(), providers)
        {
            Options = PreviewOptions.From(settings),
        };

        _previewWindow = new PreviewWindow(this);
        _previewWindow.ApplySettings(settings);

        _tray = new TrayIconController(LoadAppIcon());
        _tray.SettingsRequested += OpenSettingsWindow;
        _tray.PauseToggled += OnPauseToggled;
        _tray.ExitRequested += Shutdown;

        _mouseHook = new GlobalMouseHook();
        _hoverDetector = new HoverDetector(_mouseHook, settings.HoverDelayMs, settings.JitterTolerancePx);
        _hoverDetector.Settled += (x, y) => Dispatcher.BeginInvoke(() => OnSettled(x, y));
        _hoverDetector.HoverStarted += (x, y) => Dispatcher.BeginInvoke(() => _ = OnHoverStartedAsync(x, y));
        _hoverDetector.HoverEnded += () => Dispatcher.BeginInvoke(ScheduleClose);

        _settingsService.SettingsChanged += s => Dispatcher.BeginInvoke(() => ApplySettings(s));

        try
        {
            // hook 放在專屬執行緒：UI 卡住時系統滑鼠不會跟著頓，Windows 也不會因逾時移除 hook
            _hookThread = new MouseHookThread(_mouseHook);
            _hookThread.Start();
        }
        catch (Exception ex)
        {
            AppLog.Error("滑鼠 hook 安裝失敗", ex);
            MessageBox.Show(Strings.Format("MouseHookFailed", ex.Message), Strings.StartupFailedTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        _watchdog = new UiWatchdog(Dispatcher, () => _previewWindow.LastRenderedPath, TimeSpan.FromSeconds(3));

        // 閒下來再暖機 VLC，不拖慢啟動
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => _ = PreviewWindow.WarmUpVideoAsync());
        AppLog.Info($"HoverPeek 啟動，設定檔：{_settingsService.SettingsFilePath}");
    }

    private bool IsPaused => _tray?.IsPaused == true;

    private void OnSettled(int x, int y)
    {
        if (IsPaused || _previewWindow.ContainsCursor())
            return;

        _session?.Cancel();
        _session = _pipeline.Start(x, y);
    }

    private async Task OnHoverStartedAsync(int x, int y)
    {
        if (IsPaused || _previewWindow.ContainsCursor())
            return;

        var session = _session;
        if (session == null || session.IsCanceled || session.X != x || session.Y != y)
        {
            session?.Cancel();
            session = _session = _pipeline.Start(x, y);
        }

        var path = await session.TryGetPathAsync();
        if (!ReferenceEquals(session, _session) || path == null)
            return;

        CancelScheduledClose();
        if (_previewWindow.IsShowingComplete(path))
            return;

        var anchor = new PreviewAnchor(x, y, session.DpiScale);
        var full = session.FullTask;

        // 多數情況在停穩後的等待期間已經算好
        await Task.WhenAny(full, Task.Delay(FullResultGrace));
        if (!ReferenceEquals(session, _session))
            return;

        if (!full.IsCompleted)
        {
            await Task.WhenAny(full, session.QuickTask, Task.Delay(QuickResultGrace));
            if (!ReferenceEquals(session, _session))
                return;

            if (!full.IsCompleted)
            {
                var quick = session.QuickTask is { IsCompletedSuccessfully: true, Result: { } q } ? q : null;
                if (quick != null)
                    _previewWindow.ShowResult(quick, anchor);
                else
                    _previewWindow.ShowLoading(path, anchor);
            }
        }

        PreviewResult? result;
        try
        {
            result = await full;
        }
        catch (OperationCanceledException)
        {
            return; // 新的 session 接手
        }

        if (!ReferenceEquals(session, _session))
            return;

        if (result == null)
            _previewWindow.HidePreview(); // 不是可預覽的檔案（例如滑到捷徑上）
        else
            _previewWindow.ShowResult(result, anchor);
    }

    private void ScheduleClose()
    {
        if (!_previewWindow.IsVisible)
            return;

        _closeCts?.Cancel();
        _closeCts = new CancellationTokenSource();
        _ = CloseWhenLeftAsync(_closeCts.Token);
    }

    private void CancelScheduledClose()
    {
        _closeCts?.Cancel();
        _closeCts = null;
    }

    private async Task CloseWhenLeftAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_settingsService.Current.AutoCloseDelayMs, ct);

            while (true)
            {
                // 游標在預覽視窗裡：使用者在捲動或點清單，持續等到離開
                if (_previewWindow.ContainsCursor())
                {
                    await Task.Delay(InsideWindowRecheck, ct);
                    continue;
                }

                var (x, y) = Core.Interop.DisplayInfo.GetCursorPosition();
                var under = await Task.Run(() => _closeResolver.ResolveFileAtPoint(x, y), ct);
                if (string.Equals(under, _previewWindow.ShownPath, StringComparison.OrdinalIgnoreCase))
                    return; // 還在同一個檔案上

                // 滑到另一個可預覽的檔案：先不關，等懸停直接換內容，避免「消失→出現」的閃爍
                // SelectProvider 會查 Directory.Exists，網路磁碟離線時可能卡很久，不能在 UI 執行緒做
                if (under != null && await Task.Run(() => _pipeline.SelectProvider(under), ct) != null)
                {
                    await Task.Delay(InsideWindowRecheck, ct);
                    continue;
                }

                ct.ThrowIfCancellationRequested();
                _previewWindow.HidePreview();
                return;
            }
        }
        catch (OperationCanceledException)
        {
            // 新的預覽出現或游標回到檔案上
        }
    }

    private void OnPauseToggled(bool paused)
    {
        if (!paused)
            return;
        _session?.Cancel();
        _session = null;
        CancelScheduledClose();
        _previewWindow.HidePreview();
    }

    private void ApplySettings(AppSettings settings)
    {
        _hoverDetector?.UpdateSettings(settings.HoverDelayMs, settings.JitterTolerancePx);
        _pipeline.Options = PreviewOptions.From(settings);
        _previewWindow.ApplySettings(settings);
    }

    // ---- IPreviewHost：預覽視窗內的資料夾瀏覽、壓縮檔內圖片 ----

    // 整段丟到背景：選 provider、建快取 key 都會碰檔案系統，網路磁碟可能卡住 UI
    Task<PreviewResult> IPreviewHost.PreviewPathAsync(string path, double dpiScale) =>
        Task.Run(() => _pipeline.PreviewPathAsync(path, dpiScale));

    Task<PreviewResult> IPreviewHost.PreviewArchiveEntryAsync(string archivePath, string entryPath, double dpiScale)
    {
        var template = _pipeline.BuildRequest(archivePath, dpiScale);
        return Task.Run(() => _archiveProvider.PreviewImageInArchive(archivePath, entryPath, template));
    }

    // ---- 視窗與生命週期 ----

    private void OpenSettingsWindow()
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settingsService);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/HoverPeek;component/HoverPeek.ico", UriKind.Absolute);
            if (GetResourceStream(uri) is { } info)
                return new Icon(info.Stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            AppLog.Warn("讀取內嵌圖示失敗，改用檔案", ex);
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "HoverPeek.ico");
        return File.Exists(iconPath) ? new Icon(iconPath) : System.Drawing.SystemIcons.Application;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _session?.Cancel();
        _watchdog?.Dispose();
        _hoverDetector?.Dispose();
        _hookThread?.Dispose();
        _tray?.Dispose();
        _pipeline?.Dispose();
        _shell?.Dispose();
        AppLog.Info("HoverPeek 結束");
        base.OnExit(e);
    }

    // ---- 例外處理：一律寫日誌；UI 執行緒上的未預期錯誤才提示，而且 30 秒內只提示一次 ----

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("UI 執行緒未處理的例外", e.Exception);
        e.Handled = true;

        if (DateTime.UtcNow - _lastErrorDialog < ErrorDialogCooldown)
            return;
        _lastErrorDialog = DateTime.UtcNow;
        MessageBox.Show(Strings.Format("UnexpectedErrorLogged", AppLog.LogFile), Strings.ErrorTitle,
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        AppLog.Error($"未處理的例外（程式即將結束：{e.IsTerminating}）", e.ExceptionObject as Exception);

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLog.Warn("背景工作未觀察到的例外", e.Exception);
        e.SetObserved();
    }
}
