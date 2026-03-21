using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using HoverPeek.Core.Localization;
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
    private PreviewKind _currentListKind;
    private IReadOnlyList<ArchiveEntry>? _currentEntries;
    private readonly System.Action<string, string>? _onImageInArchiveHover;
    private readonly System.Action<string, System.Action<PreviewResult>>? _onFolderNavigate;
    private readonly System.Action<string, System.Action<PreviewResult>>? _onFilePreviewRequested;
    private bool _isMouseInside;

    private readonly Stack<string> _folderHistory = new();
    private string? _currentFolderPath;
    private bool _isPreviewingFileInFolder;

    private LibVLC? _libVLC;
    private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;

    private double _windowWidth = 800;
    private double _windowHeight = 600;
    private bool _centerWindow = true;

    public PreviewWindow(
        System.Action<string, string>? onImageInArchiveHover = null,
        System.Action<string, System.Action<PreviewResult>>? onFolderNavigate = null,
        System.Action<string, System.Action<PreviewResult>>? onFilePreviewRequested = null)
    {
        InitializeComponent();
        _onImageInArchiveHover = onImageInArchiveHover;
        _onFolderNavigate = onFolderNavigate;
        _onFilePreviewRequested = onFilePreviewRequested;

        MouseEnter += (s, e) => _isMouseInside = true;
        MouseLeave += (s, e) => _isMouseInside = false;
    }

    public bool IsMouseInside => _isMouseInside;

    public event System.Action? PreviewMouseLeft;

    public void UpdateSettings(double width, double height, bool centerWindow, int fadeInMs, int fadeOutMs)
    {
        _windowWidth = width;
        _windowHeight = height;
        _centerWindow = centerWindow;

        var fadeIn = (Storyboard)Resources["FadeIn"];
        var fadeOut = (Storyboard)Resources["FadeOut"];
        ((DoubleAnimation)fadeIn.Children[0]).Duration = TimeSpan.FromMilliseconds(fadeInMs);
        ((DoubleAnimation)fadeOut.Children[0]).Duration = TimeSpan.FromMilliseconds(fadeOutMs);
    }

    public bool IsMouseNearWindow(int mouseX, int mouseY)
    {
        if (!IsVisible || Opacity < 0.1)
            return false;

        const int buffer = 150;

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
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        HideAllPanels();
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

        switch (result.Kind)
        {
            case PreviewKind.Image:
                ShowImagePreview(result);
                break;
            case PreviewKind.Archive:
                ShowArchivePreview(result, filePath);
                break;
            case PreviewKind.Folder:
                ShowFolderPreview(result, filePath);
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

        if (result.ImageFormat == "Gif")
        {
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImagePreview, bitmap);
        }
        else
        {
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImagePreview, null);
            ImagePreview.Source = bitmap;
        }

        ImagePreview.Visibility = Visibility.Visible;
    }

    private void ShowArchivePreview(PreviewResult result, string archivePath)
    {
        _currentArchivePath = archivePath;
        _currentListKind = PreviewKind.Archive;
        _currentEntries = result.Entries;

        ArchiveTitle.Text = $"\U0001f4e6 {System.IO.Path.GetFileName(archivePath)} ({Strings.Format("ArchiveItemCount", result.Entries?.Count ?? 0)})";
        ArchivePanel.Visibility = Visibility.Visible;
        ExpandButton.Visibility = Visibility.Visible;

        var contentGrid = (Grid)ArchivePanel.FindName("ContentGrid");
        if (contentGrid != null)
        {
            var displayItems = _currentEntries?
                .Select(entry => new ArchiveDisplayItem(entry))
                .ToList();

            FileListView.ItemsSource = displayItems;
            contentGrid.Visibility = Visibility.Visible;
            ExpandButton.Content = Strings.CollapseFileList;
        }

        InnerImagePreview.Visibility = Visibility.Collapsed;
    }

    private void ShowFolderPreview(PreviewResult result, string folderPath)
    {
        _currentArchivePath = folderPath;
        _currentListKind = PreviewKind.Folder;
        _currentEntries = result.Entries;
        _currentFolderPath = folderPath;
        _folderHistory.Clear();
        BackButton.Visibility = Visibility.Collapsed;

        ArchiveTitle.Text = $"\U0001f4c1 {System.IO.Path.GetFileName(folderPath)} ({Strings.Format("FolderItemCount", result.Entries?.Count ?? 0)})";
        ArchivePanel.Visibility = Visibility.Visible;
        ExpandButton.Visibility = Visibility.Visible;

        var contentGrid = (Grid)ArchivePanel.FindName("ContentGrid");
        if (contentGrid != null)
        {
            var displayItems = _currentEntries?
                .Select(entry => new ArchiveDisplayItem(entry))
                .ToList();

            FileListView.ItemsSource = displayItems;
            contentGrid.Visibility = Visibility.Visible;
            ExpandButton.Content = Strings.CollapseFileList;
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
        var encoding = result.TextEncoding ?? Strings.EncodingUnknown;

        if (result.IsMarkdown && !string.IsNullOrEmpty(result.MarkdownHtml))
        {
            TextPreviewHeader.Text = $"\U0001f4c4 {fileName} ({encoding})";
            TextPreviewContent.Text = result.TextContent;
            TextPreviewScroll.Visibility = Visibility.Visible;
            _ = InitializeAndShowMarkdown(result.MarkdownHtml);
            return;
        }

        var syntaxHighlighting = GetSyntaxHighlighting(extension);
        if (syntaxHighlighting != null)
        {
            CodePreviewHeader.Text = $"\U0001f4c4 {fileName} ({encoding})";
            CodeEditor.Text = result.TextContent;
            CodeEditor.SyntaxHighlighting = syntaxHighlighting;
            CodeEditorPanel.Visibility = Visibility.Visible;
            return;
        }

        TextPreviewHeader.Text = $"\U0001f4c4 {fileName} ({encoding})";
        TextPreviewContent.Text = result.TextContent;
        TextPreviewScroll.Visibility = Visibility.Visible;
    }

    private async Task InitializeAndShowMarkdown(string markdownHtml)
    {
        try
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
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
                    TextPreviewScroll.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
                    MarkdownWebView.Visibility = Visibility.Collapsed;
                }
            });
        }
        catch
        {
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
            ".json" => manager.GetDefinition("JavaScript"),
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
        BackButton.Visibility = Visibility.Collapsed;

        _folderHistory.Clear();
        _currentFolderPath = null;
        _isPreviewingFileInFolder = false;

        ImagePreview.Source = null;
        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImagePreview, null);

        InnerImagePreview.Source = null;
        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(InnerImagePreview, null);

        FileListView.ItemsSource = null;

        TextPreviewContent.Text = string.Empty;
        TextPreviewHeader.Text = string.Empty;
        CodeEditor.Text = string.Empty;
        CodePreviewHeader.Text = string.Empty;

        StopVideoPlayback();
    }

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        var contentGrid = (Grid)ArchivePanel.FindName("ContentGrid");
        if (contentGrid == null) return;

        if (contentGrid.Visibility == Visibility.Visible)
        {
            contentGrid.Visibility = Visibility.Collapsed;
            ExpandButton.Content = Strings.ExpandFileList;
            InnerImagePreview.Visibility = Visibility.Collapsed;
        }
        else
        {
            var displayItems = _currentEntries?
                .Select(entry => new ArchiveDisplayItem(entry))
                .ToList();

            FileListView.ItemsSource = displayItems;
            contentGrid.Visibility = Visibility.Visible;
            ExpandButton.Content = Strings.CollapseFileList;
        }
    }

    private void OnFileListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_currentListKind != PreviewKind.Folder)
            return;

        var pos = e.GetPosition(FileListView);
        var element = FileListView.InputHitTest(pos) as DependencyObject;

        while (element != null && element is not System.Windows.Controls.ListViewItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is not System.Windows.Controls.ListViewItem lvItem ||
            lvItem.Content is not ArchiveDisplayItem displayItem ||
            _currentFolderPath == null)
            return;

        if (displayItem.Entry.IsDirectory)
        {
            if (_onFolderNavigate == null) return;
            _folderHistory.Push(_currentFolderPath);
            NavigateToFolder(displayItem.Entry.FullPath);
        }
        else
        {
            if (_onFilePreviewRequested == null) return;
            _folderHistory.Push(_currentFolderPath);
            _isPreviewingFileInFolder = true;

            _onFilePreviewRequested(displayItem.Entry.FullPath, result =>
            {
                Dispatcher.Invoke(() =>
                {
                    ShowFilePreviewFromFolder(result, displayItem.Entry.FullPath);
                });
            });
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_folderHistory.Count == 0)
            return;

        if (_isPreviewingFileInFolder)
        {
            _isPreviewingFileInFolder = false;
            var parentFolder = _folderHistory.Pop();

            HideNonFolderPanels();
            StopVideoPlayback();

            _currentListKind = PreviewKind.Folder;
            ArchivePanel.Visibility = Visibility.Visible;

            NavigateToFolder(parentFolder);
            return;
        }

        if (_onFolderNavigate == null) return;
        var previousPath = _folderHistory.Pop();
        NavigateToFolder(previousPath);
    }

    private void ShowFilePreviewFromFolder(PreviewResult result, string filePath)
    {
        HideNonFolderPanels();
        ArchivePanel.Visibility = Visibility.Collapsed;

        switch (result.Kind)
        {
            case PreviewKind.Image:
                ShowImagePreview(result);
                break;
            case PreviewKind.Video:
                ShowVideoPreview(result);
                break;
            case PreviewKind.Text:
                ShowTextPreview(result, filePath);
                break;
            case PreviewKind.Archive:
                ShowArchivePreview(result, filePath);
                break;
            default:
                ShowUnsupportedPreview();
                break;
        }

        BackButton.Visibility = Visibility.Visible;
    }

    private void HideNonFolderPanels()
    {
        ImagePreview.Visibility = Visibility.Collapsed;
        ImagePreview.Source = null;
        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImagePreview, null);

        VideoPlayer.Visibility = Visibility.Collapsed;
        TextPreviewScroll.Visibility = Visibility.Collapsed;
        MarkdownWebView.Visibility = Visibility.Collapsed;
        CodeEditorPanel.Visibility = Visibility.Collapsed;
        UnsupportedText.Visibility = Visibility.Collapsed;

        InnerImagePreview.Source = null;
        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(InnerImagePreview, null);
        InnerImagePreview.Visibility = Visibility.Collapsed;

        TextPreviewContent.Text = string.Empty;
        TextPreviewHeader.Text = string.Empty;
        CodeEditor.Text = string.Empty;
        CodePreviewHeader.Text = string.Empty;
    }

    private void NavigateToFolder(string path)
    {
        _onFolderNavigate?.Invoke(path, result =>
        {
            Dispatcher.Invoke(() =>
            {
                _currentFolderPath = path;
                _currentArchivePath = path;
                _currentEntries = result.Entries;

                ArchiveTitle.Text = $"\U0001f4c1 {System.IO.Path.GetFileName(path)} ({Strings.Format("FolderItemCount", result.Entries?.Count ?? 0)})";

                var displayItems = _currentEntries?
                    .Select(entry => new ArchiveDisplayItem(entry))
                    .ToList();
                FileListView.ItemsSource = displayItems;

                BackButton.Visibility = _folderHistory.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                InnerImagePreview.Visibility = Visibility.Collapsed;
            });
        });
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
                if (_currentListKind == PreviewKind.Folder)
                {
                    _onImageInArchiveHover?.Invoke("__folder__", displayItem.Entry.FullPath);
                }
                else
                {
                    _onImageInArchiveHover?.Invoke(_currentArchivePath, displayItem.Entry.FullPath);
                }
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

        if (imageResult.ImageFormat == "Gif")
        {
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(InnerImagePreview, bitmap);
        }
        else
        {
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(InnerImagePreview, null);
            InnerImagePreview.Source = bitmap;
        }

        InnerImagePreview.Visibility = Visibility.Visible;
    }

    private void PositionWindow(int mouseX, int mouseY)
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        var fixedWidth = _windowWidth;
        var fixedHeight = _windowHeight;

        double left = (screenWidth - fixedWidth) / 2;
        double top = (screenHeight - fixedHeight) / 2;

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
        PreviewMouseLeft?.Invoke();
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

            _mediaPlayer.EncounteredError += (sender, args) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    StopVideoPlayback();
                    VideoPlayer.Visibility = Visibility.Collapsed;
                    UnsupportedText.Text = Strings.VideoCannotPlay;
                    UnsupportedText.Visibility = Visibility.Visible;
                });
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LibVLC init failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"Video playback failed: {ex.Message}");
            VideoPlayer.Visibility = Visibility.Collapsed;
            UnsupportedText.Text = Strings.VideoCannotPlay;
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
                System.Diagnostics.Debug.WriteLine($"Stop video failed: {ex.Message}");
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
    public string TypeIcon => Entry.IsDirectory ? "\U0001f4c1" : Entry.IsImage ? "\U0001f5bc\ufe0f" : "\U0001f4c4";

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
