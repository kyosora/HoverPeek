using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace HoverPeek.UI.Themes;

/// <summary>
/// 跟隨 Windows「應用程式模式」切換淺色／深色，系統改設定時即時套用。
/// </summary>
public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static readonly Uri LightUri = new("pack://application:,,,/HoverPeek.UI;component/Themes/Light.xaml");
    private static readonly Uri DarkUri = new("pack://application:,,,/HoverPeek.UI;component/Themes/Dark.xaml");
    private static readonly Uri ControlsUri = new("pack://application:,,,/HoverPeek.UI;component/Themes/Controls.xaml");

    private static readonly List<WeakReference<Window>> Tracked = new();
    private static bool _listening;

    public static bool IsDark { get; private set; } = ReadSystemIsDark();

    public static event Action? ThemeChanged;

    /// <summary>讓視窗套用主題並追蹤系統變更。在 InitializeComponent 之前呼叫，DynamicResource 第一次解析就有值。</summary>
    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        ApplyResources(window.Resources, IsDark);
        window.Language = System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        window.SourceInitialized += (_, _) => ApplyWindowFrame(window, IsDark);

        lock (Tracked)
            Tracked.Add(new WeakReference<Window>(window));

        if (!_listening)
        {
            _listening = true;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;

        var dark = ReadSystemIsDark();
        if (dark == IsDark)
            return;

        IsDark = dark;
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            lock (Tracked)
            {
                Tracked.RemoveAll(w => !w.TryGetTarget(out _));
                foreach (var weak in Tracked)
                {
                    if (!weak.TryGetTarget(out var window))
                        continue;
                    ApplyResources(window.Resources, dark);
                    ApplyWindowFrame(window, dark);
                }
            }
            ThemeChanged?.Invoke();
        });
    }

    private static void ApplyResources(ResourceDictionary target, bool dark)
    {
        var merged = target.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source;
            if (src == LightUri || src == DarkUri || src == ControlsUri)
                merged.RemoveAt(i);
        }

        merged.Add(new ResourceDictionary { Source = dark ? DarkUri : LightUri });
        merged.Add(new ResourceDictionary { Source = ControlsUri });

        // 視窗自己的 Resources 優先於合併字典：依介面語言覆寫 UI 字型（樣式都用 DynamicResource 取字型）
        target["Hp.UiFont"] = UiFontFor(CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// 各語言的 UI 字型。拉丁字母一律 Segoe UI，CJK 依語言挑對應字型：
    /// 用錯字型時日文漢字會變成中文字形，簡體字在繁中字型裡缺字會混用其他字型。
    /// </summary>
    public static FontFamily UiFontFor(CultureInfo culture)
    {
        var cjk = culture.Name switch
        {
            "zh-TW" or "zh-HK" or "zh-MO" => "Microsoft JhengHei UI",
            _ when culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) => "Microsoft YaHei UI",
            _ when culture.TwoLetterISOLanguageName == "ja" => "Yu Gothic UI, Meiryo UI",
            _ when culture.TwoLetterISOLanguageName == "ko" => "Malgun Gothic",
            _ => "Microsoft JhengHei UI", // 其他語言用不到 CJK，保留原本的後備字型
        };
        return new FontFamily($"Segoe UI Variable Text, Segoe UI, {cjk}");
    }

    /// <summary>Win11：深色標題列／圓角／邊框色。舊版 Windows 不支援的屬性會直接失敗，不影響顯示。</summary>
    public static void ApplyWindowFrame(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var useDark = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));

        var corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        if (window.TryFindResource("Hp.Stroke") is SolidColorBrush stroke)
        {
            var c = stroke.Color;
            var colorRef = c.R | (c.G << 8) | (c.B << 16); // COLORREF = 0x00BBGGRR
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));
        }
    }

    private static bool ReadSystemIsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
