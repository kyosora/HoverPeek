using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HoverPeek.Core.Imaging;
using HoverPeek.Core.Localization;
using HoverPeek.Core.Preview;
using ICSharpCode.AvalonEdit.Highlighting;

namespace HoverPeek.UI;

/// <summary>Segoe Fluent Icons 的字碼。</summary>
internal static class Glyphs
{
    public const string Image = "";
    public const string Video = "";
    public const string Document = "";
    public const string Code = "";
    public const string Archive = "";
    public const string Folder = "";
    public const string Error = "";
    public const string Unknown = "";
    public const string Loading = "";
}

internal static class Formatting
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string FileSize(long bytes)
    {
        if (bytes < 0)
            return string.Empty;
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < Units.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return order == 0 ? $"{bytes} B" : $"{len:0.#} {Units[order]}";
    }

    public static string Join(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}

public sealed partial class PreviewWindow
{
    private static readonly Size MessageSize = new(440, 220);

    private void ResetContent()
    {
        ImageHost.Visibility = Visibility.Collapsed;
        ListHost.Visibility = Visibility.Collapsed;
        VideoPlayer.Visibility = Visibility.Collapsed;
        PlainTextView.Visibility = Visibility.Collapsed;
        CodeView.Visibility = Visibility.Collapsed;
        MarkdownView.Visibility = Visibility.Collapsed;
        MessageHost.Visibility = Visibility.Collapsed;
        ExpandButton.Visibility = Visibility.Collapsed;
        LowResBadge.Visibility = Visibility.Collapsed;
        TruncatedBadge.Visibility = Visibility.Collapsed;

        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImageView, null);
        ImageView.Source = null;
        ClearInnerImage();
        PosterView.Source = null;
        PlainTextView.Clear();
        CodeView.Text = string.Empty;
        FileListView.ItemsSource = null;
        MessageDetail.Clear();

        StopVideo();
        ResetListState();
    }

    /// <summary>依類型填入內容，回傳希望的內容區大小（DIP，不含標題列）。</summary>
    private Size RenderContent(PreviewResult result)
    {
        var path = result.SourcePath ?? string.Empty;
        switch (result.Kind)
        {
            case PreviewKind.Image:
                return RenderImage(result, path);
            case PreviewKind.Archive:
            case PreviewKind.Folder:
                RenderList(result, path);
                return ListContentSize(result.Entries);
            case PreviewKind.Video:
                return RenderVideo(result, path);
            case PreviewKind.Text:
                RenderText(result, path);
                return DefaultContentSize();
            case PreviewKind.Error:
                SetHeader(path, Glyphs.Error, Path.GetDirectoryName(path));
                ShowMessage(Glyphs.Error, Strings.PreviewFailedTitle, result.ErrorMessage, result.ErrorDetail);
                return MessageSize;
            default:
                SetHeader(path, Glyphs.Unknown, Path.GetDirectoryName(path));
                ShowMessage(Glyphs.Unknown, Strings.UnsupportedFileType, null, null);
                return MessageSize;
        }
    }

    private Size RenderImage(PreviewResult result, string path)
    {
        Size natural;
        if (result.AnimatedGifData != null)
        {
            var gif = LoadGif(result.AnimatedGifData);
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImageView, gif);
            natural = new Size(gif.PixelWidth, gif.PixelHeight);
        }
        else if (result.Image != null)
        {
            WpfAnimatedGif.ImageBehavior.SetAnimatedSource(ImageView, null);
            var bitmap = ToBitmapSource(result.Image);
            ImageView.Source = bitmap;
            natural = new Size(bitmap.Width, bitmap.Height); // DIP
        }
        else
        {
            ShowMessage(Glyphs.Unknown, Strings.UnsupportedFileType, null, null);
            return MessageSize;
        }

        var dims = result.ImageWidth > 0 ? $"{result.ImageWidth} × {result.ImageHeight}" : null;
        var size = result.FileSize > 0 ? Formatting.FileSize(result.FileSize) : null;
        SetHeader(path, Glyphs.Image, Formatting.Join(dims, result.ImageFormat, size));
        LowResBadge.Visibility = result.IsPlaceholder ? Visibility.Visible : Visibility.Collapsed;
        ImageHost.Visibility = Visibility.Visible;

        return FitToMax(natural.Width + ImagePadding, natural.Height + ImagePadding);
    }

    /// <summary>
    /// 清單視窗依項目數調整高度，項目少時不留一大片空白。
    /// 清單裡有圖片就用完整高度：滑過圖片時右側要放圖片預覽。
    /// </summary>
    private Size ListContentSize(IReadOnlyList<ArchiveEntry>? entries)
    {
        const double columnHeader = 30, rowHeight = 28, padding = 16;
        var full = DefaultContentSize();
        var count = entries?.Count ?? 0;
        // 用 Visibility 屬性判斷收合：IsVisible 在視窗顯示前一律是 false
        if (count == 0 || FileListView.Visibility != Visibility.Visible)
            return new Size(full.Width, MinContentHeight);
        if (entries!.Any(e => e.IsImage))
            return full;

        var height = columnHeader + Math.Max(3, count) * rowHeight + padding;
        return new Size(full.Width, Math.Min(height, full.Height));
    }

    /// <summary>等比例縮進設定的視窗上限內（扣掉標題列）。</summary>
    private Size FitToMax(double width, double height)
    {
        var maxW = _settings.WindowWidth;
        var maxH = Math.Max(MinContentHeight, _settings.WindowHeight - HeaderHeight());
        if (width <= 0 || height <= 0)
            return new Size(maxW, maxH);

        var scale = Math.Min(1.0, Math.Min(maxW / width, maxH / height));
        return new Size(width * scale, height * scale);
    }

    private void RenderText(PreviewResult result, string path)
    {
        var size = Formatting.FileSize(result.FileSize);
        TruncatedBadge.Visibility = result.IsTruncated ? Visibility.Visible : Visibility.Collapsed;

        if (result.IsMarkdown && result.MarkdownHtml != null)
        {
            SetHeader(path, Glyphs.Document, Formatting.Join("Markdown", result.TextEncoding, size));
            _ = ShowMarkdownAsync(result.MarkdownHtml);
            return;
        }

        var highlighting = _settings.EnableSyntaxHighlighting ? GetSyntaxHighlighting(result.FileExtension) : null;
        if (highlighting != null)
        {
            SetHeader(path, Glyphs.Code, Formatting.Join(highlighting.Name, result.TextEncoding, size));
            CodeView.SyntaxHighlighting = highlighting;
            CodeView.Text = result.TextContent ?? string.Empty;
            CodeView.ScrollToHome();
            CodeView.Visibility = Visibility.Visible;
            return;
        }

        SetHeader(path, Glyphs.Document, Formatting.Join(result.TextEncoding, size));
        PlainTextView.Text = result.TextContent ?? string.Empty;
        PlainTextView.ScrollToHome();
        PlainTextView.Visibility = Visibility.Visible;
    }

    private void ApplyTextSettings()
    {
        var family = new FontFamily(_settings.TextFontFamily);
        PlainTextView.FontFamily = family;
        PlainTextView.FontSize = _settings.TextFontSize;
        CodeView.FontFamily = family;
        CodeView.FontSize = _settings.TextFontSize;
    }

    private static IHighlightingDefinition? GetSyntaxHighlighting(string? extension)
    {
        var name = (extension ?? string.Empty).ToLowerInvariant() switch
        {
            ".cs" => "C#",
            ".cpp" or ".c" or ".h" or ".hpp" => "C++",
            ".java" => "Java",
            ".js" or ".jsx" or ".mjs" or ".cjs" or ".json" or ".jsonc" => "JavaScript",
            ".ts" or ".tsx" => "JavaScript",
            ".py" => "Python",
            ".php" => "PHP",
            ".xml" or ".xaml" or ".config" or ".csproj" or ".props" or ".targets" or ".slnx" => "XML",
            ".html" or ".htm" or ".vue" or ".svelte" => "HTML",
            ".css" or ".scss" or ".sass" or ".less" => "CSS",
            ".sql" => "TSQL",
            ".ps1" or ".psm1" => "PowerShell",
            ".md" or ".markdown" => "MarkDown",
            _ => null, // 沒有對應定義的（.bat、.sh、.yaml…）走純文字，比套錯語法好讀
        };
        return name == null ? null : HighlightingManager.Instance.GetDefinition(name);
    }

    private void SetHeader(string path, string glyph, string? meta)
    {
        var name = Path.GetFileName(path.TrimEnd('\\', '/'));
        TitleText.Text = string.IsNullOrEmpty(name) ? path : name;
        TitleText.ToolTip = path;
        HeaderIcon.Text = glyph;
        MetaText.Text = meta ?? string.Empty;
    }

    private void ShowMessage(string glyph, string title, string? body, string? detail)
    {
        MessageIcon.Text = glyph;
        MessageIcon.Foreground = (Brush)FindResource(glyph == Glyphs.Error ? "Hp.Danger" : "Hp.TextTertiary");
        MessageTitle.Text = title;
        MessageBody.Text = body ?? string.Empty;
        MessageBody.Visibility = string.IsNullOrEmpty(body) ? Visibility.Collapsed : Visibility.Visible;
        MessageDetail.Text = detail ?? string.Empty;
        MessageDetail.Visibility = Visibility.Collapsed;
        DetailsButton.Visibility = string.IsNullOrEmpty(detail) ? Visibility.Collapsed : Visibility.Visible;
        MessageHost.Visibility = Visibility.Visible;
    }

    private void OnDetailsClick(object sender, RoutedEventArgs e)
    {
        MessageDetail.Visibility = Visibility.Visible;
        DetailsButton.Visibility = Visibility.Collapsed;
    }

    internal static BitmapSource ToBitmapSource(DecodedImage image)
    {
        var bitmap = BitmapSource.Create(
            image.Width, image.Height, image.DpiX, image.DpiY,
            PixelFormats.Pbgra32, null, image.Pixels, image.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapImage LoadGif(byte[] data)
    {
        var gif = new BitmapImage();
        gif.BeginInit();
        gif.CacheOption = BitmapCacheOption.OnLoad;
        gif.StreamSource = new MemoryStream(data, writable: false);
        gif.EndInit();
        gif.Freeze();
        return gif;
    }
}
