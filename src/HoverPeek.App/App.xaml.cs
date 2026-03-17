using System.Windows;
using HoverPeek.Core.FileResolver;
using HoverPeek.Core.Localization;
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
    private bool _previewLocked = false;
    private WinForms.NotifyIcon? _notifyIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 載入設定並初始化語言
        _settingsService = new SettingsService();
        var settings = _settingsService.Current;
        LocaleManager.SetLanguage(settings.Language);

        // 建立系統托盤圖示
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = Strings.TrayTooltip
        };

        var contextMenu = new WinForms.ContextMenuStrip();

        var settingsItem = new WinForms.ToolStripMenuItem(Strings.TraySettings);
        settingsItem.Click += (s, args) => OpenSettingsWindow();
        contextMenu.Items.Add(settingsItem);

        contextMenu.Items.Add(new WinForms.ToolStripSeparator());

        var exitItem = new WinForms.ToolStripMenuItem(Strings.TrayExitApp);
        exitItem.Click += (s, args) =>
        {
            _notifyIcon.Visible = false;
            Shutdown();
        };
        contextMenu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.DoubleClick += (s, args) =>
        {
            MessageBox.Show(
                Strings.TrayRunningMessage,
                Strings.TrayRunningTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };

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
        _previewWindow.UpdateSettings(
            settings.WindowWidth, settings.WindowHeight,
            settings.CenterWindow,
            settings.FadeInDurationMs, settings.FadeOutDurationMs);

        _previewWindow.Show();
        _previewWindow.Hide();

        // 訂閱設定變更事件
        _settingsService.SettingsChanged += OnSettingsChanged;

        try
        {
            _mouseHook.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Strings.Format("MouseHookFailed", ex.Message),
                Strings.StartupFailedTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _notifyIcon.Visible = false;
            Shutdown();
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        Dispatcher.Invoke(() =>
        {
            _hoverDetector?.UpdateSettings(settings.HoverDelayMs, settings.JitterTolerancePx);
            _imageProvider?.UpdateSettings(settings.ImageMaxDimension);
            _svgProvider?.UpdateSettings(settings.ImageMaxDimension);
            _textProvider?.UpdateSettings(
                settings.TextMaxFileSizeMB * 1024 * 1024,
                settings.TextMaxLines);
            _previewWindow?.UpdateSettings(
                settings.WindowWidth, settings.WindowHeight,
                settings.CenterWindow,
                settings.FadeInDurationMs, settings.FadeOutDurationMs);
        });
    }

    private async void OnHoverStarted(int x, int y)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
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
                _previewLocked = true;
            }
            catch (Exception ex)
            {
                var errorMsg = Strings.Format("PreviewErrorMessage", filePath);

                var currentEx = ex;
                var depth = 0;
                while (currentEx != null && depth < 5)
                {
                    errorMsg += $"\n{Strings.Format("ErrorLabel", depth + 1)} {currentEx.GetType().Name}\n{currentEx.Message}\n";
                    currentEx = currentEx.InnerException;
                    depth++;
                }

                errorMsg += Strings.Format("FullStackTrace", ex);

                MessageBox.Show(errorMsg, Strings.PreviewErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                _previewLocked = false;
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
        if (_settingsService != null)
            _settingsService.SettingsChanged -= OnSettingsChanged;

        _notifyIcon?.Dispose();
        _hoverDetector?.Dispose();
        _mouseHook?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var errorMessage = Strings.Format("UnhandledExceptionMessage", e.Exception.GetType().Name, e.Exception.Message, e.Exception.StackTrace);
        MessageBox.Show(errorMessage, Strings.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var errorMessage = Strings.Format("FatalErrorMessage", exception?.GetType().Name, exception?.Message, exception?.StackTrace);
        MessageBox.Show(errorMessage, Strings.FatalErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var errorMessage = Strings.Format("TaskExceptionMessage", e.Exception.GetType().Name, e.Exception.Message, e.Exception.StackTrace);
        MessageBox.Show(errorMessage, Strings.TaskErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        e.SetObserved();
    }
}
