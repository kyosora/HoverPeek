using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HoverPeek.Core.Diagnostics;
using HoverPeek.Core.Localization;
using HoverPeek.Core.Preview;

namespace HoverPeek.UI;

/// <summary>清單中的一列（壓縮檔項目或資料夾項目）。</summary>
public sealed class ListEntryItem
{
    public ListEntryItem(ArchiveEntry entry)
    {
        Entry = entry;
    }

    public ArchiveEntry Entry { get; }
    public string Name => Entry.Name;
    public string SizeText => Entry.IsDirectory ? string.Empty : Formatting.FileSize(Entry.Size);
    public string ModifiedText => Entry.LastModified?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
    public string Glyph => Entry.IsDirectory ? Glyphs.Folder : Entry.IsImage ? Glyphs.Image : Glyphs.Document;
}

public sealed partial class PreviewWindow
{
    private static readonly TimeSpan InnerImageDelay = TimeSpan.FromMilliseconds(80);

    private readonly Stack<string> _folderHistory = new();
    private PreviewKind _listKind;
    private string? _listPath;
    private bool _listExpanded;
    private ListEntryItem? _pendingInnerItem;
    private string? _innerImageEntry;
    private DispatcherTimer? _innerImageTimer;
    private GifPlayer? _innerGifPlayer;

    private void ResetListState()
    {
        _folderHistory.Clear();
        _listPath = null;
        _pendingInnerItem = null;
        _innerImageEntry = null;
        _innerImageTimer?.Stop();
        BackButton.Visibility = Visibility.Collapsed;
    }

    private void RenderList(PreviewResult result, string path)
    {
        _listKind = result.Kind;
        _listPath = path;

        var entries = result.Entries ?? Array.Empty<ArchiveEntry>();
        var isArchive = result.Kind == PreviewKind.Archive;
        SetHeader(
            path,
            isArchive ? Glyphs.Archive : Glyphs.Folder,
            Formatting.Join(
                Strings.Format("MetaItems", entries.Count),
                isArchive ? Formatting.FileSize(result.FileSize) : null));

        FileListView.ItemsSource = entries.Select(e => new ListEntryItem(e)).ToList();
        ClearInnerImage();

        // 資料夾一律展開；壓縮檔依設定
        _listExpanded = !isArchive || _settings.ArchiveAutoExpand;
        UpdateListExpansion();
        BackButton.Visibility = _folderHistory.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ListHost.Visibility = Visibility.Visible;
    }

    private void UpdateListExpansion()
    {
        FileListView.Visibility = _listExpanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandButton.Content = _listExpanded ? Strings.CollapseFileList : Strings.ExpandFileList;
        ExpandButton.Visibility = _listKind == PreviewKind.Archive ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        _listExpanded = !_listExpanded;
        UpdateListExpansion();
        if (!_listExpanded)
            ClearInnerImage();
    }

    private void ClearInnerImage()
    {
        _innerGifPlayer?.Dispose();
        _innerGifPlayer = null;
        InnerImageView.Source = null;
        InnerImageColumn.Width = new GridLength(0);
        _innerImageEntry = null;
    }

    private void OnFileListMouseMove(object sender, MouseEventArgs e)
    {
        var item = ItemUnderMouse(e);
        if (item == null || !item.Entry.IsImage || item.Entry.FullPath == _innerImageEntry)
        {
            _pendingInnerItem = null;
            return;
        }

        if (ReferenceEquals(item, _pendingInnerItem))
            return;

        _pendingInnerItem = item;
        _innerImageTimer ??= CreateInnerImageTimer();
        _innerImageTimer.Stop();
        _innerImageTimer.Start();
    }

    private void OnFileListMouseLeave(object sender, MouseEventArgs e)
    {
        _pendingInnerItem = null;
        _innerImageTimer?.Stop();
    }

    private DispatcherTimer CreateInnerImageTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = InnerImageDelay };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            var item = _pendingInnerItem;
            if (item != null)
                await LoadInnerImageAsync(item);
        };
        return timer;
    }

    private async Task LoadInnerImageAsync(ListEntryItem item)
    {
        var listPath = _listPath;
        if (listPath == null)
            return;

        PreviewResult result;
        try
        {
            result = _listKind == PreviewKind.Archive
                ? await _host.PreviewArchiveEntryAsync(listPath, item.Entry.FullPath, _anchor.DpiScale)
                : await _host.PreviewPathAsync(item.Entry.FullPath, _anchor.DpiScale);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"清單內圖片預覽失敗：{item.Entry.FullPath}", ex);
            return;
        }

        // 回來時游標已經移到別列、或整個預覽換了，就丟掉
        if (!ReferenceEquals(_pendingInnerItem, item) || _listPath != listPath || result.Kind != PreviewKind.Image)
            return;

        if (result.Image == null)
            return;

        _innerGifPlayer?.Dispose();
        InnerImageView.Source = ToBitmapSource(result.Image);
        _innerGifPlayer = result.AnimatedGifData == null
            ? null
            : GifPlayer.Start(result.AnimatedGifData, Math.Max(result.Image.Width, result.Image.Height), _anchor.DpiScale, InnerImageView);

        _innerImageEntry = item.Entry.FullPath;
        InnerImageColumn.Width = new GridLength(1, GridUnitType.Star);
    }

    private async void OnFileListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_listKind != PreviewKind.Folder || _listPath == null || ItemUnderMouse(e) is not { } item)
            return;

        _folderHistory.Push(_listPath);
        await OpenFromListAsync(item.Entry.FullPath);
    }

    private async void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_folderHistory.Count == 0)
            return;
        await OpenFromListAsync(_folderHistory.Pop());
    }

    /// <summary>在預覽視窗內打開資料夾或檔案，保留瀏覽歷史，不重新定位視窗。</summary>
    private async Task OpenFromListAsync(string path)
    {
        PreviewResult result;
        try
        {
            result = await _host.PreviewPathAsync(path, _anchor.DpiScale);
        }
        catch (Exception ex)
        {
            result = PreviewResult.FromException(path, ex);
        }

        var history = _folderHistory.ToArray();
        ResetContent();
        foreach (var p in history.Reverse())
            _folderHistory.Push(p);

        _current = result;
        RenderContent(result);
        BackButton.Visibility = _folderHistory.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private ListEntryItem? ItemUnderMouse(MouseEventArgs e)
    {
        var element = FileListView.InputHitTest(e.GetPosition(FileListView)) as DependencyObject;
        while (element != null && element is not ListViewItem)
            element = VisualTreeHelper.GetParent(element);
        return (element as ListViewItem)?.Content as ListEntryItem;
    }
}
