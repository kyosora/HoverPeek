using System.Windows;
using HoverPeek.Core.Settings;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace HoverPeek.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private AppSettings _currentSettings;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _currentSettings = _settingsService.Current;
        LoadSettingsToUI();
    }

    private void LoadSettingsToUI()
    {
        // 懸停行為
        HoverDelayTextBox.Text = _currentSettings.HoverDelayMs.ToString();
        JitterToleranceTextBox.Text = _currentSettings.JitterTolerancePx.ToString();
        AutoCloseDelayTextBox.Text = _currentSettings.AutoCloseDelayMs.ToString();

        // 預覽視窗
        WindowWidthTextBox.Text = _currentSettings.WindowWidth.ToString();
        WindowHeightTextBox.Text = _currentSettings.WindowHeight.ToString();
        CenterWindowCheckBox.IsChecked = _currentSettings.CenterWindow;
        FadeInDurationTextBox.Text = _currentSettings.FadeInDurationMs.ToString();
        FadeOutDurationTextBox.Text = _currentSettings.FadeOutDurationMs.ToString();

        // 圖片預覽
        ImageMaxDimensionTextBox.Text = _currentSettings.ImageMaxDimension.ToString();
        EnableGifAnimationCheckBox.IsChecked = _currentSettings.EnableGifAnimation;

        // 文字預覽
        TextMaxFileSizeTextBox.Text = _currentSettings.TextMaxFileSizeMB.ToString();
        TextMaxLinesTextBox.Text = _currentSettings.TextMaxLines.ToString();
        TextFontSizeTextBox.Text = _currentSettings.TextFontSize.ToString();
        TextFontFamilyComboBox.Text = _currentSettings.TextFontFamily;
        EnableMarkdownRenderingCheckBox.IsChecked = _currentSettings.EnableMarkdownRendering;
        EnableSyntaxHighlightingCheckBox.IsChecked = _currentSettings.EnableSyntaxHighlighting;

        // 影片預覽
        VideoAutoPlayCheckBox.IsChecked = _currentSettings.VideoAutoPlay;
        VideoMutedCheckBox.IsChecked = _currentSettings.VideoMuted;

        // 壓縮檔預覽
        ArchiveAutoExpandCheckBox.IsChecked = _currentSettings.ArchiveAutoExpand;

        // 啟動設定
        StartWithWindowsCheckBox.IsChecked = _currentSettings.StartWithWindows;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // 從 UI 讀取設定
            var newSettings = new AppSettings
            {
                // 懸停行為
                HoverDelayMs = int.Parse(HoverDelayTextBox.Text),
                JitterTolerancePx = int.Parse(JitterToleranceTextBox.Text),
                AutoCloseDelayMs = int.Parse(AutoCloseDelayTextBox.Text),

                // 預覽視窗
                WindowWidth = double.Parse(WindowWidthTextBox.Text),
                WindowHeight = double.Parse(WindowHeightTextBox.Text),
                CenterWindow = CenterWindowCheckBox.IsChecked == true,
                FadeInDurationMs = int.Parse(FadeInDurationTextBox.Text),
                FadeOutDurationMs = int.Parse(FadeOutDurationTextBox.Text),

                // 圖片預覽
                ImageMaxDimension = int.Parse(ImageMaxDimensionTextBox.Text),
                EnableGifAnimation = EnableGifAnimationCheckBox.IsChecked == true,

                // 文字預覽
                TextMaxFileSizeMB = long.Parse(TextMaxFileSizeTextBox.Text),
                TextMaxLines = int.Parse(TextMaxLinesTextBox.Text),
                TextFontSize = int.Parse(TextFontSizeTextBox.Text),
                TextFontFamily = TextFontFamilyComboBox.Text,
                EnableMarkdownRendering = EnableMarkdownRenderingCheckBox.IsChecked == true,
                EnableSyntaxHighlighting = EnableSyntaxHighlightingCheckBox.IsChecked == true,

                // 影片預覽
                VideoAutoPlay = VideoAutoPlayCheckBox.IsChecked == true,
                VideoMuted = VideoMutedCheckBox.IsChecked == true,

                // 壓縮檔預覽
                ArchiveAutoExpand = ArchiveAutoExpandCheckBox.IsChecked == true,

                // 啟動設定
                StartWithWindows = StartWithWindowsCheckBox.IsChecked == true
            };

            // 處理開機啟動
            SetStartupRegistry(newSettings.StartWithWindows);

            // 儲存設定
            _settingsService.SaveSettings(newSettings);

            MessageBox.Show(
                "設定已儲存！\n\n部分設定需要重新啟動 HoverPeek 才會生效。",
                "HoverPeek",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"儲存設定失敗：\n\n{ex.Message}",
                "HoverPeek 錯誤",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "確定要重設為預設值嗎？\n\n這將會清除所有自訂設定。",
            "HoverPeek",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _currentSettings = AppSettings.Default;
            LoadSettingsToUI();
        }
    }

    private void SetStartupRegistry(bool enable)
    {
        const string appName = "HoverPeek";
        var executablePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrEmpty(executablePath))
            return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

            if (key == null)
                return;

            if (enable)
            {
                key.SetValue(appName, $"\"{executablePath}\"");
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch
        {
            // 忽略註冊表錯誤
        }
    }
}
