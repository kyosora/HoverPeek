namespace HoverPeek.Core.Settings;

public sealed class AppSettings
{
    /// <summary>
    /// 設定檔格式版本。2 = 2026-11 重構後（CenterWindow 開始生效、新增 ShowQuickThumbnail）。
    /// </summary>
    public const int CurrentVersion = 2;
    public int SettingsVersion { get; set; } = CurrentVersion;

    // 懸停行為
    public int HoverDelayMs { get; set; } = 400;
    public int JitterTolerancePx { get; set; } = 8;
    public int AutoCloseDelayMs { get; set; } = 400;

    // 預覽視窗（寬高是上限；圖片預覽會依圖片比例縮小視窗）
    public double WindowWidth { get; set; } = 800;
    public double WindowHeight { get; set; } = 600;
    public bool CenterWindow { get; set; } = false;  // true=螢幕中央, false=游標旁
    public int FadeInDurationMs { get; set; } = 60;
    public int FadeOutDurationMs { get; set; } = 60;

    // 圖片預覽
    public int ImageMaxDimension { get; set; } = 800;
    public bool EnableGifAnimation { get; set; } = true;
    public bool ShowQuickThumbnail { get; set; } = true;

    // 文字預覽
    public long TextMaxFileSizeMB { get; set; } = 1;
    public int TextMaxLines { get; set; } = 1000;
    public int TextFontSize { get; set; } = 12;
    public string TextFontFamily { get; set; } = "Cascadia Mono, Consolas";
    public bool EnableMarkdownRendering { get; set; } = true;
    public bool EnableSyntaxHighlighting { get; set; } = true;

    // 影片預覽
    public bool VideoAutoPlay { get; set; } = true;
    public bool VideoMuted { get; set; } = true;

    // 壓縮檔預覽
    public bool ArchiveAutoExpand { get; set; } = true;

    // 啟動設定
    public bool StartWithWindows { get; set; } = false;

    // 語言：新安裝時依 Windows 顯示語言自動選，之後以設定檔為準
    public string Language { get; set; } = Localization.SupportedLanguages.DetectFromSystem();

    public static AppSettings Default => new();

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

/// <summary>
/// 各數值設定的合法範圍。設定視窗驗證與載入時的修正共用這一份。
/// </summary>
public static class SettingsLimits
{
    public readonly record struct Range(double Min, double Max)
    {
        public bool Contains(double v) => v >= Min && v <= Max;
        public double Clamp(double v) => Math.Clamp(v, Min, Max);
    }

    public static readonly Range HoverDelayMs = new(100, 3000);
    public static readonly Range JitterTolerancePx = new(1, 50);
    public static readonly Range AutoCloseDelayMs = new(0, 3000);
    public static readonly Range WindowWidth = new(320, 3840);
    public static readonly Range WindowHeight = new(240, 2160);
    public static readonly Range FadeDurationMs = new(0, 1000);
    public static readonly Range ImageMaxDimension = new(200, 4096);
    public static readonly Range TextMaxFileSizeMB = new(1, 50);
    public static readonly Range TextMaxLines = new(50, 100000);
    public static readonly Range TextFontSize = new(8, 32);

    /// <summary>
    /// 把超出範圍的值拉回範圍內（手改設定檔、舊版本留下的怪值）。
    /// 回傳新物件，不改動傳入的設定。
    /// </summary>
    public static AppSettings Normalize(AppSettings s)
    {
        var n = s.Clone();
        n.HoverDelayMs = (int)HoverDelayMs.Clamp(s.HoverDelayMs);
        n.JitterTolerancePx = (int)JitterTolerancePx.Clamp(s.JitterTolerancePx);
        n.AutoCloseDelayMs = (int)AutoCloseDelayMs.Clamp(s.AutoCloseDelayMs);
        n.WindowWidth = WindowWidth.Clamp(s.WindowWidth);
        n.WindowHeight = WindowHeight.Clamp(s.WindowHeight);
        n.FadeInDurationMs = (int)FadeDurationMs.Clamp(s.FadeInDurationMs);
        n.FadeOutDurationMs = (int)FadeDurationMs.Clamp(s.FadeOutDurationMs);
        n.ImageMaxDimension = (int)ImageMaxDimension.Clamp(s.ImageMaxDimension);
        n.TextMaxFileSizeMB = (long)TextMaxFileSizeMB.Clamp(s.TextMaxFileSizeMB);
        n.TextMaxLines = (int)TextMaxLines.Clamp(s.TextMaxLines);
        n.TextFontSize = (int)TextFontSize.Clamp(s.TextFontSize);
        if (string.IsNullOrWhiteSpace(s.TextFontFamily))
            n.TextFontFamily = AppSettings.Default.TextFontFamily;
        if (!Localization.SupportedLanguages.IsSupported(s.Language))
            n.Language = AppSettings.Default.Language;
        return n;
    }

    /// <summary>
    /// 舊版設定檔升級。v1 的 CenterWindow 從未生效（一律置中），存的 true 只是預設值，
    /// 所以升級時改成新預設（游標旁）；淡入淡出也換成新預設，舊的 150ms 會拖慢體感。
    /// </summary>
    public static AppSettings Migrate(AppSettings s)
    {
        if (s.SettingsVersion >= AppSettings.CurrentVersion)
            return s;

        var m = s.Clone();
        var d = AppSettings.Default;
        m.CenterWindow = d.CenterWindow;
        if (s.FadeInDurationMs == 150) m.FadeInDurationMs = d.FadeInDurationMs;
        if (s.FadeOutDurationMs == 100) m.FadeOutDurationMs = d.FadeOutDurationMs;
        if (s.HoverDelayMs == 500) m.HoverDelayMs = d.HoverDelayMs;
        if (s.AutoCloseDelayMs == 600) m.AutoCloseDelayMs = d.AutoCloseDelayMs;
        m.ShowQuickThumbnail = true;
        m.SettingsVersion = AppSettings.CurrentVersion;
        return m;
    }
}
