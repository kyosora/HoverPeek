using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using HoverPeek.Core.Interop;
using HoverPeek.Core.Preview;
using HoverPeek.Core.Settings;
using HoverPeek.UI.Themes;

namespace HoverPeek.UI;

/// <summary>預覽視窗依附的螢幕位置：游標座標（實體像素）與該螢幕的縮放比例。</summary>
public readonly record struct PreviewAnchor(int CursorX, int CursorY, double DpiScale);

/// <summary>預覽視窗需要向外要資料時的介面（資料夾雙擊、壓縮檔內圖片…），由 App 實作。</summary>
public interface IPreviewHost
{
    Task<PreviewResult> PreviewPathAsync(string path, double dpiScale);
    Task<PreviewResult> PreviewArchiveEntryAsync(string archivePath, string entryPath, double dpiScale);
}

/// <summary>
/// 懸停預覽視窗。不搶焦點（WS_EX_NOACTIVATE），位置與大小一律用實體像素透過 SetWindowPos 設定，
/// 才能在多螢幕、不同縮放比例下放在正確位置（WPF 的 Left/Top 是 DIP，跨螢幕時換算會錯）。
/// </summary>
public sealed partial class PreviewWindow : Window
{
    private const double MinContentWidth = 320;
    private const double MinContentHeight = 120;
    private const double ImagePadding = 16; // ImageView 的 Margin 兩側合計

    private readonly IPreviewHost _host;
    private AppSettings _settings = AppSettings.Default;
    private PreviewAnchor _anchor;
    private PreviewResult? _current;
    private bool _isLoading;
    private string? _lastRenderedPath;
    private IntPtr _hwnd;

    public PreviewWindow(IPreviewHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        ThemeManager.Attach(this);
        InitializeComponent();
        ThemeManager.ThemeChanged += OnThemeChanged;

        // 先建好 HWND，第一次預覽時不必等建立視窗
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>最後一次開始繪製的路徑。可在任何執行緒讀取（UI 卡住時看門狗用來記錄元兇）。</summary>
    public string? LastRenderedPath => Volatile.Read(ref _lastRenderedPath);

    /// <summary>目前顯示中的檔案路徑；資料夾內點進去的檔案也算。</summary>
    public string? ShownPath => IsVisible ? _current?.SourcePath : null;

    /// <summary>
    /// 是否已在顯示這個檔案的完整預覽（不是載入中、也不是占位縮圖）。
    /// 用來判斷懸停時要不要重畫：載入中的畫面必須能被同一檔案的下一次懸停取代。
    /// </summary>
    public bool IsShowingComplete(string path) =>
        IsVisible && !_isLoading && _current is { IsPlaceholder: false } c
        && string.Equals(c.SourcePath, path, StringComparison.OrdinalIgnoreCase);

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings?.Clone() ?? throw new ArgumentNullException(nameof(settings));
        ApplyTextSettings();
    }

    /// <summary>游標是否在預覽視窗範圍內（用實體座標判斷，不依賴 WPF 的 MouseEnter）。</summary>
    public bool ContainsCursor()
    {
        if (!IsVisible)
            return false;
        var rect = DisplayInfo.GetWindowRect(_hwnd);
        var (x, y) = DisplayInfo.GetCursorPosition();
        return rect?.Contains(x, y) == true;
    }

    public void ShowLoading(string path, PreviewAnchor anchor)
    {
        _anchor = anchor;
        _isLoading = true;
        Volatile.Write(ref _lastRenderedPath, path);
        _current = new PreviewResult { Kind = PreviewKind.Unsupported, SourcePath = path };
        ResetContent();
        SetHeader(path, Glyphs.Loading, string.Empty);
        ShowMessage(Glyphs.Loading, Core.Localization.Strings.PreviewLoading, null, null);
        PlaceAndShow(DefaultContentSize());
    }

    /// <summary>
    /// 顯示預覽。若目前正顯示同一檔案的占位縮圖，這次是清晰版，就只換圖不重排版面。
    /// </summary>
    public void ShowResult(PreviewResult result, PreviewAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(result);

        var isUpgrade = IsVisible
                        && _current is { IsPlaceholder: true }
                        && result.Kind == PreviewKind.Image
                        && string.Equals(_current.SourcePath, result.SourcePath, StringComparison.OrdinalIgnoreCase);

        _anchor = anchor;
        _isLoading = false;
        _current = result;

        if (!isUpgrade)
            ResetContent();

        var contentSize = RenderContent(result);
        PlaceAndShow(contentSize, fade: !isUpgrade);
    }

    public void HidePreview()
    {
        if (!IsVisible)
            return;

        StopVideo();
        Hide();
        ResetContent();
        _current = null;
        _isLoading = false;
    }

    private Size DefaultContentSize() =>
        new(_settings.WindowWidth, Math.Max(MinContentHeight, _settings.WindowHeight - HeaderHeight()));

    private double HeaderHeight()
    {
        if (HeaderBar.ActualHeight > 0)
            return HeaderBar.ActualHeight;
        HeaderBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return HeaderBar.DesiredSize.Height;
    }

    /// <summary>依內容大小（DIP）算出視窗大小，換成實體像素放到游標旁或螢幕中央。</summary>
    private void PlaceAndShow(Size contentSize, bool fade = true)
    {
        var header = HeaderHeight();
        var widthDip = Math.Clamp(contentSize.Width, MinContentWidth, _settings.WindowWidth);
        var heightDip = Math.Clamp(contentSize.Height + header, MinContentHeight + header, _settings.WindowHeight);

        var scale = _anchor.DpiScale > 0 ? _anchor.DpiScale : 1.0;
        var work = DisplayInfo.GetWorkAreaAt(_anchor.CursorX, _anchor.CursorY);
        var rect = WindowPlacement.Compute(
            _anchor.CursorX, _anchor.CursorY,
            (int)Math.Ceiling(widthDip * scale), (int)Math.Ceiling(heightDip * scale),
            work, _settings.CenterWindow, scale);

        var wasVisible = IsVisible;
        if (!wasVisible && fade && _settings.FadeInDurationMs > 0)
            RootGrid.Opacity = 0;

        // 先只移動：跨到不同 DPI 的螢幕時 WPF 會依 WM_DPICHANGED 調整大小，第二次再設定真正的尺寸
        SetWindowPos(_hwnd, HWND_TOPMOST, rect.Left, rect.Top, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        SetWindowPos(_hwnd, HWND_TOPMOST, rect.Left, rect.Top, rect.Width, rect.Height, SWP_NOACTIVATE);

        if (!wasVisible)
        {
            Show();
            if (fade && _settings.FadeInDurationMs > 0)
                FadeInContent();
        }
    }

    private void FadeInContent()
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(_settings.FadeInDurationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        RootGrid.BeginAnimation(OpacityProperty, animation);
    }

    private void OnThemeChanged()
    {
        if (_current is { IsMarkdown: true, MarkdownHtml: not null } md && MarkdownView.Visibility == Visibility.Visible)
            _ = ShowMarkdownAsync(md.MarkdownHtml);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        DisposeVideo();
        base.OnClosed(e);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
