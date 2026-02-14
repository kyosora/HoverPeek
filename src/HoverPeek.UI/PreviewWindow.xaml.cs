using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using HoverPeek.Core.Preview;
using LibVLCSharp.Shared;
using ICSharpCode.AvalonEdit.Highlighting;

namespace HoverPeek.UI;

public partial class PreviewWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private string? _currentArchivePath;
    private IReadOnlyList<ArchiveEntry>? _currentEntries;
    private readonly System.Action<string, string>? _onImageInArchiveHover;
    private bool _isMouseInside;

    private LibVLC? _libVLC;
    private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;

    public PreviewWindow(System.Action<string, string>? onImageInArchiveHover = null)
    {
        InitializeComponent();
        _onImageInArchiveHover = onImageInArchiveHover;

        MouseEnter += (s, e) => _isMouseInside = true;
        MouseLeave += (s, e) => _isMouseInside = false;
    }

    public bool IsMouseInside => _isMouseInside;

    /// <summary>
    /// 檢查指定的滑鼠座標是否在預覽視窗的「影響範圍」內
    /// 影響範圍 = 視窗本身 + 周圍 150px 的緩衝區
    /// 用於防止滑鼠移動到預覽視窗時經過其他檔案觸發新預覽
    /// </summary>
    public bool IsMouseNearWindow(int mouseX, int mouseY)
    {
        if (!IsVisible || Opacity < 0.1)
            return false;

        const int buffer = 150;  // 緩衝區大小（px）

        var windowLeft = Left;
        var windowTop = Top;
        var windowRight = Left + ActualWidth;
        var windowBottom = Top + ActualHeight;

        return mouseX >= windowLeft - buffer &&
               mouseX <= windowRight + buffer &&
               mouseY >= windowTop - buffer &&
               mouseY <= windowBottom + buffer;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var extStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    public void ShowPreview(PreviewResult result, string filePath, int mouseX, int mouseY)
    {
        // 停止所有正在執行的動畫，避免衝突
        BeginAnimation(OpacityProperty, null);

        // 立即隱藏，避免內容更新時的閃現
        Opacity = 0;

        HideAllPanels();

        // 強制完成 UI 渲染，確保舊內容已清除
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

        switch (result.Kind)
        {
            case PreviewKind.Image:
                ShowImagePreview(result);
                break;
            case PreviewKind.Archive:
                ShowArchivePreview(result, filePath);
                break;
            case PreviewKind.Video:
                ShowVideoPreview(result);
                break;
            case PreviewKind.Text:
                ShowTextPreview(result, filePath);
                break;
            case PreviewKind.Unsupported:
                ShowUnsupportedPreview();
                break;
        }

        PositionWindow(mouseX, mouseY);
        FadeIn();
    }

    private void ShowImagePreview(PreviewResult result)
    {
        if (result.ImageData == null) return;

        using var ms = new MemoryStream(result.ImageData);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();

        // GIF 動畫支援
        if (result.ImageFormat == "Gif")
        {
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImagePreview, bitmap);
        }
        else
        {
            // 清除可能的動畫綁定，使用靜態圖片
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImagePreview, null);
            ImagePreview.Source = bitmap;
        }

        ImagePreview.Visibility = Visibility.Visible;
    }

    private void ShowArchivePreview(PreviewResult result, string archivePath)
    {
        _currentArchivePath = archivePath;
        _currentEntries = result.Entries;

        ArchiveTitle.Text = $"📦 {System.IO.Path.GetFileName(archivePath)} ({result.Entries?.Count ?? 0} 個項目)";
        ArchivePanel.Visibility = Visibility.Visible;
        ExpandButton.Visibility = Visibility.Visible;

        // 自動展開檔案列表
        var contentGrid = (Grid)ArchivePanel.FindName("ContentGrid");
        if (contentGrid != null)
        {
            var displayItems = _currentEntries?
                .Select(entry => new ArchiveDisplayItem(entry))
                .ToList();

            FileListView.ItemsSource = displayItems;
            contentGrid.Visibility = Visibility.Visible;
            ExpandButton.Content = "收起檔案列表";
        }

        InnerImagePreview.Visibility = Visibility.Collapsed;
    }

    private void ShowTextPreview(PreviewResult result, string filePath)
    {
        if (string.IsNullOrEmpty(result.TextContent))
        {
            ShowUnsupportedPreview();
            return;
        }

        var fileName = System.IO.Path.GetFileName(filePath);
        var extension = result.FileExtension ?? "";
        var encoding = result.TextEncoding ?? "未知";

        // 1. 如果是 Markdown 且有 HTML 內容，嘗試使用 WebView2 顯示
        // 同時先顯示純文字作為 fallback，如果 WebView2 初始化成功會自動切換
        if (result.IsMarkdown && !string.IsNullOrEmpty(result.MarkdownHtml))
        {
            // 先顯示純文字內容（作為 fallback 和載入中的提示）
            TextPreviewHeader.Text = $"📄 {fileName} ({encoding})";
            TextPreviewContent.Text = result.TextContent;
            TextPreviewScroll.Visibility = Visibility.Visible;

            // 異步嘗試初始化 WebView2（成功後會隱藏純文字）
            _ = InitializeAndShowMarkdown(result.MarkdownHtml);
            return;
        }

        // 2. 否則如果是程式碼檔案，使用 AvalonEdit 語法高亮
        var syntaxHighlighting = GetSyntaxHighlighting(extension);
        if (syntaxHighlighting != null)
        {
            CodePreviewHeader.Text = $"📄 {fileName} ({encoding})";
            CodeEditor.Text = result.TextContent;
            CodeEditor.SyntaxHighlighting = syntaxHighlighting;
            CodeEditorPanel.Visibility = Visibility.Visible;
            return;
        }

        // 3. 否則使用純文字顯示
        TextPreviewHeader.Text = $"📄 {fileName} ({encoding})";
        TextPreviewContent.Text = result.TextContent;
        TextPreviewScroll.Visibility = Visibility.Visible;
    }

    private async Task InitializeAndShowMarkdown(string markdownHtml)
    {
        try
        {
            // 確保在 UI 執行緒上執行
            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    // 初始化 WebView2
                    await MarkdownWebView.EnsureCoreWebView2Async();

                    var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; padding: 16px; line-height: 1.6; }}
        code {{ background: #f4f4f4; padding: 2px 6px; border-radius: 3px; font-family: Consolas, Monaco, monospace; }}
        pre {{ background: #f4f4f4; padding: 12px; border-radius: 6px; overflow-x: auto; }}
        pre code {{ background: none; padding: 0; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background: #f4f4f4; }}
    </style>
</head>
<body>
{markdownHtml}
</body>
</html>";

                    MarkdownWebView.NavigateToString(htmlContent);
                    MarkdownWebView.Visibility = Visibility.Visible;

                    // 成功顯示 WebView2，隱藏純文字 fallback
                    TextPreviewScroll.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebView2 初始化失敗: {ex.Message}");
                    // 初始化失敗時隱藏 WebView（保持純文字 fallback 顯示）
                    MarkdownWebView.Visibility = Visibility.Collapsed;
                }
            });
        }
        catch
        {
            // 忽略所有錯誤，避免未觀察的例外
        }
    }

    private IHighlightingDefinition? GetSyntaxHighlighting(string extension)
    {
        var manager = HighlightingManager.Instance;

        return extension.ToLowerInvariant() switch
        {
            ".cs" => manager.GetDefinition("C#"),
            ".cpp" or ".c" or ".h" or ".hpp" => manager.GetDefinition("C++"),
            ".java" => manager.GetDefinition("Java"),
            ".js" or ".jsx" => manager.GetDefinition("JavaScript"),
            ".ts" or ".tsx" => manager.GetDefinition("TypeScript"),
            ".py" => manager.GetDefinition("Python"),
            ".rb" => manager.GetDefinition("Ruby"),
            ".php" => manager.GetDefinition("PHP"),
            ".xml" or ".xaml" or ".config" => manager.GetDefinition("XML"),
            ".html" or ".htm" => manager.GetDefinition("HTML"),
            ".css" or ".scss" or ".sass" => manager.GetDefinition("CSS"),
            ".json" => manager.GetDefinition("JavaScript"),  // JSON 用 JavaScript 語法
            ".sql" => manager.GetDefinition("SQL"),
            ".sh" or ".bash" => manager.GetDefinition("Bash"),
            ".ps1" => manager.GetDefinition("PowerShell"),
            _ => null
        };
    }

    private void ShowUnsupportedPreview()
    {
        UnsupportedText.Visibility = Visibility.Visible;
    }

    private void HideAllPanels()
    {
        ImagePreview.Visibility = Visibility.Collapsed;
        ArchivePanel.Visibility = Visibility.Collapsed;
        VideoPlayer.Visibility = Visibility.Collapsed;
        TextPreviewScroll.Visibility = Visibility.Collapsed;
        MarkdownWebView.Visibility = Visibility.Collapsed;
        CodeEditorPanel.Visibility = Visibility.Collapsed;
        UnsupportedText.Visibility = Visibility.Collapsed;

        // 清除圖片來源和 GIF 動畫綁定
        ImagePreview.Source = null;
        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImagePreview, null);

        InnerImagePreview.Source = null;
        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(InnerImagePreview, null);

        FileListView.ItemsSource = null;

        // 清除文字內容
        TextPreviewContent.Text = string.Empty;
        TextPreviewHeader.Text = string.Empty;
        CodeEditor.Text = string.Empty;
        CodePreviewHeader.Text = string.Empty;

        // 停止影片播放
        StopVideoPlayback();
    }

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        var contentGrid = (Grid)ArchivePanel.FindName("ContentGrid");
        if (contentGrid == null) return;

        if (contentGrid.Visibility == Visibility.Visible)
        {
            contentGrid.Visibility = Visibility.Collapsed;
            ExpandButton.Content = "展開檔案列表";
            InnerImagePreview.Visibility = Visibility.Collapsed;
        }
        else
        {
            var displayItems = _currentEntries?
                .Select(entry => new ArchiveDisplayItem(entry))
                .ToList();

            FileListView.ItemsSource = displayItems;
            contentGrid.Visibility = Visibility.Visible;
            ExpandButton.Content = "收起檔案列表";
        }
    }

    private void OnFileListMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (FileListView.ItemsSource == null) return;

        var pos = e.GetPosition(FileListView);
        var element = FileListView.InputHitTest(pos) as DependencyObject;

        while (element != null && element is not System.Windows.Controls.ListViewItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is System.Windows.Controls.ListViewItem item && item.Content is ArchiveDisplayItem displayItem)
        {
            if (displayItem.Entry.IsImage && !string.IsNullOrEmpty(_currentArchivePath))
            {
                _onImageInArchiveHover?.Invoke(_currentArchivePath, displayItem.Entry.FullPath);
            }
        }
    }

    public void ShowInnerImagePreview(PreviewResult imageResult)
    {
        if (imageResult.Kind != PreviewKind.Image || imageResult.ImageData == null)
        {
            InnerImagePreview.Visibility = Visibility.Collapsed;
            return;
        }

        using var ms = new MemoryStream(imageResult.ImageData);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();

        // GIF 動畫支援
        if (imageResult.ImageFormat == "Gif")
        {
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(InnerImagePreview, bitmap);
        }
        else
        {
            // 清除可能的動畫綁定，使用靜態圖片
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(InnerImagePreview, null);
            InnerImagePreview.Source = bitmap;
        }

        InnerImagePreview.Visibility = Visibility.Visible;
    }

    private void PositionWindow(int mouseX, int mouseY)
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        // 固定視窗大小，避免因圖片大小變化導致位置飄移
        const double fixedWidth = 800;
        const double fixedHeight = 600;

        // 固定在螢幕正中間
        double left = (screenWidth - fixedWidth) / 2;
        double top = (screenHeight - fixedHeight) / 2;

        // 確保不超出螢幕邊界
        if (left < 10) left = 10;
        if (top < 10) top = 10;
        if (left + fixedWidth > screenWidth - 10)
            left = screenWidth - fixedWidth - 10;
        if (top + fixedHeight > screenHeight - 10)
            top = screenHeight - fixedHeight - 10;

        Width = fixedWidth;
        Height = fixedHeight;
        Left = left;
        Top = top;
    }

    private void FadeIn()
    {
        var storyboard = (Storyboard)Resources["FadeIn"];
        storyboard.Begin(this);
        Show();
    }

    private void FadeOut()
    {
        var storyboard = (Storyboard)Resources["FadeOut"];
        storyboard.Completed += (s, e) => Hide();
        storyboard.Begin(this);
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isMouseInside = false;
    }

    private void EnsureLibVLCInitialized()
    {
        if (_libVLC != null) return;

        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVLC = new LibVLC("--no-audio", "--no-video-title-show");
            _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC)
            {
                EnableHardwareDecoding = true
            };

            // 訂閱錯誤事件
            _mediaPlayer.EncounteredError += (sender, args) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    StopVideoPlayback();
                    VideoPlayer.Visibility = Visibility.Collapsed;
                    UnsupportedText.Text = "影片無法播放";
                    UnsupportedText.Visibility = Visibility.Visible;
                });
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LibVLC 初始化失敗: {ex.Message}");
        }
    }

    private void ShowVideoPreview(PreviewResult result)
    {
        if (string.IsNullOrEmpty(result.VideoFilePath)) return;

        try
        {
            EnsureLibVLCInitialized();

            if (_mediaPlayer == null || _libVLC == null) return;

            VideoPlayer.MediaPlayer = _mediaPlayer;
            VideoPlayer.Visibility = Visibility.Visible;

            using var media = new Media(_libVLC, result.VideoFilePath, FromType.FromPath);
            media.AddOption(":no-audio");
            _mediaPlayer.Play(media);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"影片播放失敗: {ex.Message}");
            VideoPlayer.Visibility = Visibility.Collapsed;
            UnsupportedText.Text = "影片無法播放";
            UnsupportedText.Visibility = Visibility.Visible;
        }
    }

    private void StopVideoPlayback()
    {
        if (_mediaPlayer is { IsPlaying: true })
        {
            try
            {
                _mediaPlayer.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止影片播放失敗: {ex.Message}");
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopVideoPlayback();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        base.OnClosed(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

public sealed class ArchiveDisplayItem
{
    public ArchiveEntry Entry { get; }

    public ArchiveDisplayItem(ArchiveEntry entry)
    {
        Entry = entry;
    }

    public string Name => Entry.Name;
    public string SizeText => Entry.IsDirectory ? "" : FormatFileSize(Entry.Size);
    public string TypeIcon => Entry.IsDirectory ? "📁" : Entry.IsImage ? "🖼️" : "📄";

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
