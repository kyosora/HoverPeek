using System.Windows;
using HoverPeek.Core.FileResolver;
using HoverPeek.Core.MouseHook;
using HoverPeek.Core.Preview;
using HoverPeek.Core.Settings;
using HoverPeek.UI;
using System.Drawing;
using WinForms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace HoverPeek.App;

public partial class App : System.Windows.Application
{
    private GlobalMouseHook? _mouseHook;
    private HoverDetector? _hoverDetector;
    private ExplorerFileResolver? _fileResolver;
    private PreviewWindow? _previewWindow;
    private ImagePreviewProvider? _imageProvider;
    private SvgPreviewProvider? _svgProvider;
    private ArchivePreviewProvider? _archiveProvider;
    private VideoPreviewProvider? _videoProvider;
    private TextPreviewProvider? _textProvider;
    private SettingsService? _settingsService;
    private bool _previewLocked = false;  // 預覽鎖定標記
    private WinForms.NotifyIcon? _notifyIcon;  // 系統托盤圖示

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 建立系統托盤圖示
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = "HoverPeek - 檔案預覽工具"
        };

        // 建立右鍵選單
        var contextMenu = new WinForms.ContextMenuStrip();

        var settingsItem = new WinForms.ToolStripMenuItem("設定");
        settingsItem.Click += (s, args) => OpenSettingsWindow();
        contextMenu.Items.Add(settingsItem);

        contextMenu.Items.Add(new WinForms.ToolStripSeparator());

        var exitItem = new WinForms.ToolStripMenuItem("退出 HoverPeek");
        exitItem.Click += (s, args) =>
        {
            _notifyIcon.Visible = false;
            Shutdown();
        };
        contextMenu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = contextMenu;

        // 雙擊托盤圖示可以開啟設定視窗（未來可擴充）
        _notifyIcon.DoubleClick += (s, args) =>
        {
            MessageBox.Show(
                "HoverPeek 正在背景運作中\n\n懸停在檔案總管的圖片、壓縮檔、影片上即可預覽",
                "HoverPeek",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };

        // 載入設定
        _settingsService = new SettingsService();
        var settings = _settingsService.Current;

        // 使用設定初始化 Providers
        _imageProvider = new ImagePreviewProvider(settings.ImageMaxDimension);
        _svgProvider = new SvgPreviewProvider(settings.ImageMaxDimension);
        _archiveProvider = new ArchivePreviewProvider(_imageProvider);
        _videoProvider = new VideoPreviewProvider();
        _textProvider = new TextPreviewProvider(
            settings.TextMaxFileSizeMB * 1024 * 1024,
            settings.TextMaxLines);
        _fileResolver = new ExplorerFileResolver();

        _mouseHook = new GlobalMouseHook();
        _hoverDetector = new HoverDetector(_mouseHook,
            hoverThresholdMs: settings.HoverDelayMs,
            jitterTolerancePx: settings.JitterTolerancePx);

        _hoverDetector.HoverStarted += OnHoverStarted;
        _hoverDetector.HoverEnded += OnHoverEnded;

        _previewWindow = new PreviewWindow(OnImageInArchiveHover);

        _previewWindow.Show();
        _previewWindow.Hide();

        try
        {
            _mouseHook.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"無法安裝滑鼠 Hook：{ex.Message}\n\n請確認應用程式有足夠的權限。",
                "HoverPeek 啟動失敗",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _notifyIcon.Visible = false;
            Shutdown();
        }
    }

    private async void OnHoverStarted(int x, int y)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            // 如果預覽已鎖定，忽略所有新的懸停事件
            // 這樣使用者就能安全地在檔案列表間移動滑鼠，不會觸發新預覽
            if (_previewLocked)
            {
                return;
            }

            _previewWindow?.Hide();

            var filePath = _fileResolver?.ResolveFileAtPoint(x, y);
            if (filePath == null)
            {
                return;
            }

            IPreviewProvider? provider = _archiveProvider?.CanHandle(filePath) == true
                ? _archiveProvider
                : _svgProvider?.CanHandle(filePath) == true
                    ? _svgProvider
                    : _imageProvider?.CanHandle(filePath) == true
                        ? _imageProvider
                        : _videoProvider?.CanHandle(filePath) == true
                            ? _videoProvider
                            : _textProvider?.CanHandle(filePath) == true
                                ? _textProvider
                                : null;

            if (provider == null)
            {
                return;
            }

            try
            {
                var preview = await provider.GeneratePreviewAsync(filePath);
                _previewWindow?.ShowPreview(preview, filePath, x, y);
                _previewLocked = true;  // 顯示預覽後立即鎖定
            }
            catch (Exception ex)
            {
                var errorMsg = $"預覽圖片時發生錯誤：\n\n檔案：{filePath}\n\n";

                var currentEx = ex;
                var depth = 0;
                while (currentEx != null && depth < 5)
                {
                    errorMsg += $"\n[錯誤 {depth + 1}] {currentEx.GetType().Name}\n{currentEx.Message}\n";
                    currentEx = currentEx.InnerException;
                    depth++;
                }

                errorMsg += $"\n完整堆疊：\n{ex}";

                MessageBox.Show(errorMsg, "HoverPeek 預覽錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                _previewWindow?.Hide();
            }
        });
    }

    private void OnHoverEnded()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            var delay = _settingsService?.Current.AutoCloseDelayMs ?? 600;
            await Task.Delay(delay);
            if (_previewWindow != null && !_previewWindow.IsMouseInside)
            {
                _previewWindow.Hide();
                _previewLocked = false;  // 關閉視窗後解除鎖定
            }
        });
    }

    private void OnImageInArchiveHover(string archivePath, string entryPath)
    {
        if (_archiveProvider == null || _previewWindow == null)
            return;

        try
        {
            var imagePreview = _archiveProvider.PreviewImageInArchive(archivePath, entryPath);
            _previewWindow.ShowInnerImagePreview(imagePreview);
        }
        catch
        {
        }
    }

    private void OpenSettingsWindow()
    {
        if (_settingsService == null)
            return;

        Dispatcher.Invoke(() =>
        {
            var settingsWindow = new SettingsWindow(_settingsService)
            {
                Owner = null,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            settingsWindow.ShowDialog();
        });
    }

    private static Icon LoadAppIcon()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "HoverPeek.ico");
        if (System.IO.File.Exists(iconPath))
            return new Icon(iconPath);

        return SystemIcons.Application;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        _hoverDetector?.Dispose();
        _mouseHook?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var errorMessage = $"發生未處理的例外：\n\n{e.Exception.GetType().Name}\n{e.Exception.Message}\n\n堆疊追蹤：\n{e.Exception.StackTrace}";
        MessageBox.Show(errorMessage, "HoverPeek 錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var errorMessage = $"發生致命錯誤：\n\n{exception?.GetType().Name}\n{exception?.Message}\n\n堆疊追蹤：\n{exception?.StackTrace}";
        MessageBox.Show(errorMessage, "HoverPeek 致命錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var errorMessage = $"Task 未觀察例外：\n\n{e.Exception.GetType().Name}\n{e.Exception.Message}\n\n堆疊追蹤：\n{e.Exception.StackTrace}";
        MessageBox.Show(errorMessage, "HoverPeek Task 錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        e.SetObserved();
    }
}

