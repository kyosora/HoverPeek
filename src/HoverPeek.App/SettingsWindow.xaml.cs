using System.Windows;
using System.Windows.Controls;
using HoverPeek.Core.Localization;
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

        // 語言
        SelectLanguageInComboBox(_currentSettings.Language);
    }

    private void SelectLanguageInComboBox(string language)
    {
        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if (item.Tag is string tag && tag == language)
            {
                LanguageComboBox.SelectedItem = item;
                return;
            }
        }
        LanguageComboBox.SelectedIndex = 0; // default zh-TW
    }

    private string GetSelectedLanguage()
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return tag;
        return "zh-TW";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var oldLanguage = _currentSettings.Language;
            var newLanguage = GetSelectedLanguage();

            var newSettings = new AppSettings
            {
                HoverDelayMs = int.Parse(HoverDelayTextBox.Text),
                JitterTolerancePx = int.Parse(JitterToleranceTextBox.Text),
                AutoCloseDelayMs = int.Parse(AutoCloseDelayTextBox.Text),

                WindowWidth = double.Parse(WindowWidthTextBox.Text),
                WindowHeight = double.Parse(WindowHeightTextBox.Text),
                CenterWindow = CenterWindowCheckBox.IsChecked == true,
                FadeInDurationMs = int.Parse(FadeInDurationTextBox.Text),
                FadeOutDurationMs = int.Parse(FadeOutDurationTextBox.Text),

                ImageMaxDimension = int.Parse(ImageMaxDimensionTextBox.Text),
                EnableGifAnimation = EnableGifAnimationCheckBox.IsChecked == true,

                TextMaxFileSizeMB = long.Parse(TextMaxFileSizeTextBox.Text),
                TextMaxLines = int.Parse(TextMaxLinesTextBox.Text),
                TextFontSize = int.Parse(TextFontSizeTextBox.Text),
                TextFontFamily = TextFontFamilyComboBox.Text,
                EnableMarkdownRendering = EnableMarkdownRenderingCheckBox.IsChecked == true,
                EnableSyntaxHighlighting = EnableSyntaxHighlightingCheckBox.IsChecked == true,

                VideoAutoPlay = VideoAutoPlayCheckBox.IsChecked == true,
                VideoMuted = VideoMutedCheckBox.IsChecked == true,

                ArchiveAutoExpand = ArchiveAutoExpandCheckBox.IsChecked == true,

                StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,

                Language = newLanguage
            };

            SetStartupRegistry(newSettings.StartWithWindows);
            _settingsService.SaveSettings(newSettings);

            var languageChanged = oldLanguage != newLanguage;
            var message = languageChanged
                ? $"{Strings.SaveSuccessMessage}\n\n{Strings.SaveSuccessLanguageChanged}"
                : Strings.SaveSuccessMessage;

            MessageBox.Show(
                message,
                Strings.TrayRunningTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Strings.Format("SaveErrorMessage", ex.Message),
                Strings.SaveErrorTitle,
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
            Strings.ResetConfirmMessage,
            Strings.TrayRunningTitle,
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
        }
    }
}
