using System.Diagnostics;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using HoverPeek.Core.Diagnostics;
using HoverPeek.Core.Localization;
using HoverPeek.Core.Settings;
using HoverPeek.UI.Themes;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace HoverPeek.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private NumericField[] _numericFields = [];
    private FrameworkElement[] _sections = [];

    public SettingsWindow(SettingsService settingsService)
    {
        ThemeManager.Attach(this);
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HoverPeek;component/SettingsStyles.xaml", UriKind.Absolute),
        });
        InitializeComponent();

        _settingsService = settingsService;
        PopulateLanguages();
        ConfigureNumericFields();
        LoadSettings(_settingsService.Current);
        LoadAboutInfo();

        _sections = [
            HoverSection, WindowSection, ImageSection, TextSection,
            VideoSection, ArchiveSection, GeneralSection, AboutSection,
        ];
        NavigationList.SelectedIndex = 0;
        ShowSection(0);
    }

    private void ConfigureNumericFields()
    {
        var fields = new (NumericField Field, SettingsLimits.Range Range)[]
        {
            (HoverDelayField, SettingsLimits.HoverDelayMs),
            (JitterToleranceField, SettingsLimits.JitterTolerancePx),
            (AutoCloseDelayField, SettingsLimits.AutoCloseDelayMs),
            (WindowWidthField, SettingsLimits.WindowWidth),
            (WindowHeightField, SettingsLimits.WindowHeight),
            (FadeInDurationField, SettingsLimits.FadeDurationMs),
            (ImageMaxDimensionField, SettingsLimits.ImageMaxDimension),
            (TextMaxFileSizeField, SettingsLimits.TextMaxFileSizeMB),
            (TextMaxLinesField, SettingsLimits.TextMaxLines),
            (TextFontSizeField, SettingsLimits.TextFontSize),
        };

        _numericFields = fields.Select(pair => pair.Field).ToArray();
        foreach (var (field, range) in fields)
        {
            field.SetRange(range);
            field.ValidityChanged += (_, _) => UpdateSaveState();
        }
    }

    private void LoadSettings(AppSettings settings)
    {
        HoverDelayField.Text = settings.HoverDelayMs.ToString();
        JitterToleranceField.Text = settings.JitterTolerancePx.ToString();
        AutoCloseDelayField.Text = settings.AutoCloseDelayMs.ToString();
        WindowWidthField.Text = settings.WindowWidth.ToString("0");
        WindowHeightField.Text = settings.WindowHeight.ToString("0");
        CenterWindowToggle.IsChecked = settings.CenterWindow;
        FadeInDurationField.Text = settings.FadeInDurationMs.ToString();
        ImageMaxDimensionField.Text = settings.ImageMaxDimension.ToString();
        EnableGifAnimationToggle.IsChecked = settings.EnableGifAnimation;
        ShowQuickThumbnailToggle.IsChecked = settings.ShowQuickThumbnail;
        TextMaxFileSizeField.Text = settings.TextMaxFileSizeMB.ToString();
        TextMaxLinesField.Text = settings.TextMaxLines.ToString();
        TextFontSizeField.Text = settings.TextFontSize.ToString();
        TextFontFamilyCombo.Text = settings.TextFontFamily;
        EnableMarkdownRenderingToggle.IsChecked = settings.EnableMarkdownRendering;
        EnableSyntaxHighlightingToggle.IsChecked = settings.EnableSyntaxHighlighting;
        VideoAutoPlayToggle.IsChecked = settings.VideoAutoPlay;
        VideoMutedToggle.IsChecked = settings.VideoMuted;
        ArchiveAutoExpandToggle.IsChecked = settings.ArchiveAutoExpand;
        StartWithWindowsToggle.IsChecked = settings.StartWithWindows;
        SelectLanguage(settings.Language);
        SaveErrorText.Text = string.Empty;
        UpdateSaveState();
    }

    /// <summary>語言選單用各語言的原生名稱，切到看不懂的語言時也找得回來。</summary>
    private void PopulateLanguages()
    {
        // 每個語言名稱用該語言自己的字型，例如在繁中介面裡「简体中文」也要用簡體字型才不會缺字
        foreach (var language in SupportedLanguages.All)
        {
            LanguageCombo.Items.Add(new ComboBoxItem
            {
                Content = language.NativeName,
                Tag = language.Code,
                FontFamily = ThemeManager.UiFontFor(new System.Globalization.CultureInfo(language.Code)),
            });
        }

        // 選單框裡顯示的選中項目用的是 ComboBox 本身的字型，跟著換
        LanguageCombo.SelectionChanged += (_, _) =>
        {
            if (LanguageCombo.SelectedItem is ComboBoxItem item)
                LanguageCombo.FontFamily = item.FontFamily;
        };
    }

    private void SelectLanguage(string language)
    {
        LanguageCombo.SelectedItem = LanguageCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => Equals(item.Tag, language));
        LanguageCombo.SelectedIndex = Math.Max(LanguageCombo.SelectedIndex, 0);
    }

    private string GetSelectedLanguage() =>
        LanguageCombo.SelectedItem is ComboBoxItem { Tag: string language } ? language : SupportedLanguages.Fallback;

    private void UpdateSaveState()
    {
        if (SaveButton != null)
            SaveButton.IsEnabled = _numericFields.All(field => field.IsValueValid);
    }

    private void OnNavigationSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || NavigationList.SelectedIndex < 0 || _sections.Length == 0)
            return;
        ShowSection(NavigationList.SelectedIndex);
    }

    private void ShowSection(int selectedIndex)
    {
        for (var index = 0; index < _sections.Length; index++)
            _sections[index].Visibility = index == selectedIndex ? Visibility.Visible : Visibility.Collapsed;

        string[] titles = [
            Strings.TabHoverBehavior, Strings.TabPreviewWindow, Strings.SectionImage, Strings.SectionText,
            Strings.SectionVideo, Strings.SectionArchive, Strings.SectionGeneral, Strings.TabAbout,
        ];
        SectionTitle.Text = titles[selectedIndex];
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_numericFields.Any(field => !field.IsValueValid))
            return;

        var settings = _settingsService.Current;
        var oldLanguage = settings.Language;
        settings.HoverDelayMs = HoverDelayField.GetInt32();
        settings.JitterTolerancePx = JitterToleranceField.GetInt32();
        settings.AutoCloseDelayMs = AutoCloseDelayField.GetInt32();
        settings.WindowWidth = WindowWidthField.GetInt32();
        settings.WindowHeight = WindowHeightField.GetInt32();
        settings.CenterWindow = CenterWindowToggle.IsChecked == true;
        settings.FadeInDurationMs = FadeInDurationField.GetInt32();
        settings.ImageMaxDimension = ImageMaxDimensionField.GetInt32();
        settings.EnableGifAnimation = EnableGifAnimationToggle.IsChecked == true;
        settings.ShowQuickThumbnail = ShowQuickThumbnailToggle.IsChecked == true;
        settings.TextMaxFileSizeMB = TextMaxFileSizeField.GetInt64();
        settings.TextMaxLines = TextMaxLinesField.GetInt32();
        settings.TextFontSize = TextFontSizeField.GetInt32();
        settings.TextFontFamily = TextFontFamilyCombo.Text;
        settings.EnableMarkdownRendering = EnableMarkdownRenderingToggle.IsChecked == true;
        settings.EnableSyntaxHighlighting = EnableSyntaxHighlightingToggle.IsChecked == true;
        settings.VideoAutoPlay = VideoAutoPlayToggle.IsChecked == true;
        settings.VideoMuted = VideoMutedToggle.IsChecked == true;
        settings.ArchiveAutoExpand = ArchiveAutoExpandToggle.IsChecked == true;
        settings.StartWithWindows = StartWithWindowsToggle.IsChecked == true;
        settings.Language = GetSelectedLanguage();

        try
        {
            SetStartupRegistry(settings.StartWithWindows);
            _settingsService.SaveSettings(settings);
        }
        catch (IOException ex)
        {
            SaveErrorText.Text = Strings.Format("SaveErrorMessage", ex.Message);
            AppLog.Warn("設定視窗儲存失敗", ex);
            return;
        }

        if (!string.Equals(oldLanguage, settings.Language, StringComparison.Ordinal))
        {
            MessageBox.Show(Strings.SaveSuccessLanguageChanged, Strings.TrayRunningTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        Close();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(Strings.ResetConfirmMessage, Strings.TrayRunningTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
            LoadSettings(AppSettings.Default);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void LoadAboutInfo()
    {
        var version = typeof(SettingsWindow).Assembly.GetName().Version;
        AboutAppName.Text = Strings.AboutAppName;
        AboutVersion.Text = version == null ? "v1.0.0" : $"v{version.Major}.{version.Minor}.{version.Build}";
        AboutFramework.Text = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
    }

    private void OnHyperlinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        OpenWithShell(e.Uri.AbsoluteUri, "開啟 GitHub 連結失敗");
        e.Handled = true;
    }

    private void OnOpenSettingsFolderClick(object sender, RoutedEventArgs e)
    {
        var folder = Path.GetDirectoryName(_settingsService.SettingsFilePath);
        if (!string.IsNullOrEmpty(folder))
            OpenFolder(folder, "開啟設定檔資料夾失敗");
    }

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e) =>
        OpenFolder(AppLog.LogDirectory, "開啟日誌資料夾失敗");

    private static void OpenFolder(string folder, string logMessage)
    {
        try
        {
            Directory.CreateDirectory(folder);
            OpenWithShell(folder, logMessage);
        }
        catch (Exception ex)
        {
            AppLog.Warn(logMessage, ex);
        }
    }

    private static void OpenWithShell(string target, string logMessage)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Warn(logMessage, ex);
        }
    }

    private static void SetStartupRegistry(bool enable)
    {
        const string appName = "HoverPeek";
        var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(executablePath))
            return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null)
            {
                AppLog.Warn("無法開啟 Windows 啟動登錄機碼");
                return;
            }

            if (enable)
                key.SetValue(appName, $"\"{executablePath}\"");
            else
                key.DeleteValue(appName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            AppLog.Warn("更新 Windows 開機啟動設定失敗", ex);
        }
    }
}
