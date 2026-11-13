using System.IO;
using System.Windows;
using System.Windows.Input;
using HoverPeek.Core.Diagnostics;
using HoverPeek.Core.Localization;
using HoverPeek.Core.Preview;
using LibVLCSharp.Shared;

namespace HoverPeek.UI;

public sealed partial class PreviewWindow
{
    private static LibVLC? _libVlc;
    private static Task? _warmUp;

    private MediaPlayer? _player;
    private Task _pendingStop = Task.CompletedTask;
    private string? _playingPath;

    /// <summary>
    /// 在背景先初始化 LibVLC（載入 plugins 要幾百毫秒），第一次預覽影片時就不用等。
    /// 重複呼叫只會做一次。
    /// </summary>
    public static Task WarmUpVideoAsync() => _warmUp ??= Task.Run(() =>
    {
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC("--no-video-title-show", "--quiet");
        }
        catch (Exception ex)
        {
            AppLog.Error("LibVLC 初始化失敗，影片預覽將無法使用", ex);
        }
    });

    private Size RenderVideo(PreviewResult result, string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        SetHeader(path, Glyphs.Video, Formatting.Join(ext, Formatting.FileSize(result.FileSize)));

        Size natural = new(16, 9);
        if (result.Image != null)
        {
            var poster = ToBitmapSource(result.Image);
            PosterView.Source = poster;
            natural = new Size(poster.Width, poster.Height);
        }

        PosterView.Visibility = Visibility.Visible;
        PosterView.Cursor = _settings.VideoAutoPlay ? null : Cursors.Hand;
        PosterView.MouseLeftButtonUp -= OnPosterClick;
        PosterView.MouseLeftButtonUp += OnPosterClick;
        VideoPlayer.Visibility = Visibility.Visible;

        if (_settings.VideoAutoPlay)
            _ = PlayAsync(path);

        // 沒有海報時用 16:9 撐滿寬度
        return result.Image == null
            ? FitToMax(_settings.WindowWidth, _settings.WindowWidth * 9 / 16)
            : FitToMax(natural.Width, natural.Height);
    }

    private void OnPosterClick(object sender, MouseButtonEventArgs e)
    {
        if (_current?.Kind == PreviewKind.Video && _current.VideoFilePath is { } path && _playingPath != path)
            _ = PlayAsync(path);
    }

    private async Task PlayAsync(string path)
    {
        await WarmUpVideoAsync();
        await _pendingStop;

        // 等待期間使用者可能已經移到別的檔案
        if (_libVlc == null || !IsVisible || _current?.VideoFilePath != path)
        {
            if (_libVlc == null)
                ShowVideoError();
            return;
        }

        if (_player == null)
        {
            _player = new MediaPlayer(_libVlc) { EnableHardwareDecoding = true };
            _player.Playing += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                if (_playingPath != null && _current?.VideoFilePath == _playingPath)
                    PosterView.Visibility = Visibility.Collapsed;
            });
            _player.EncounteredError += (_, _) => Dispatcher.BeginInvoke(ShowVideoError);
        }

        VideoPlayer.MediaPlayer = _player;
        using var media = new Media(_libVlc, path, FromType.FromPath);
        media.AddOption(":input-repeat=65535"); // 預覽短片時循環播放
        if (_settings.VideoMuted)
            media.AddOption(":no-audio");

        _playingPath = path;
        _player.Play(media);
    }

    private void ShowVideoError()
    {
        if (_current?.Kind != PreviewKind.Video)
            return;
        StopVideo();
        VideoPlayer.Visibility = Visibility.Collapsed;
        ShowMessage(Glyphs.Video, Strings.VideoCannotPlay, _current.SourcePath, null);
    }

    /// <summary>VLC 的 Stop 是同步呼叫，可能卡住幾百毫秒，丟到背景做。</summary>
    private void StopVideo()
    {
        _playingPath = null;
        var player = _player;
        if (player == null)
            return;

        var previous = _pendingStop;
        _pendingStop = Task.Run(async () =>
        {
            await previous.ConfigureAwait(false);
            try
            {
                player.Stop();
            }
            catch (Exception ex)
            {
                AppLog.Warn("停止影片播放失敗", ex);
            }
        });
    }

    private void DisposeVideo()
    {
        StopVideo();
        var player = _player;
        _player = null;
        if (player != null)
            _pendingStop.ContinueWith(_ => player.Dispose(), TaskScheduler.Default);
    }
}
