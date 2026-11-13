using System.Globalization;

namespace HoverPeek.Core.Localization;

/// <summary>
/// 介面支援的語言。新增語言時：在這裡加一筆，再用 tools/build_locale_resx.py 產生 Strings.{code}.resx。
/// </summary>
public static class SupportedLanguages
{
    /// <param name="Code">CultureInfo 名稱，同時也是 resx 檔名後綴（Strings.zh-CN.resx）。</param>
    /// <param name="NativeName">用該語言自己寫的名稱：不管目前介面是哪種語言，使用者都認得出自己的語言。</param>
    public sealed record Language(string Code, string NativeName);

    public const string Fallback = "en";

    public static IReadOnlyList<Language> All { get; } = new[]
    {
        new Language("zh-TW", "繁體中文"),
        new Language("zh-CN", "简体中文"),
        new Language("en", "English"),
        new Language("ja", "日本語"),
        new Language("ko", "한국어"),
        new Language("ru", "Русский"),
    };

    public static bool IsSupported(string? code) =>
        code != null && All.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 依 Windows 顯示語言挑最接近的介面語言（只在第一次啟動、還沒有設定檔時使用）。
    /// 中文要分繁簡：zh-TW / zh-HK / zh-MO / zh-Hant → 繁體；其餘中文 → 簡體。
    /// </summary>
    public static string Detect(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        for (var c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            switch (c.Name)
            {
                case "zh-TW" or "zh-HK" or "zh-MO" or "zh-Hant":
                    return "zh-TW";
                case "zh-CN" or "zh-SG" or "zh-Hans":
                    return "zh-CN";
            }
        }

        var neutral = culture.TwoLetterISOLanguageName;
        if (neutral == "zh")
            return "zh-CN";
        return All.FirstOrDefault(l => l.Code == neutral)?.Code ?? Fallback;
    }

    public static string DetectFromSystem() => Detect(CultureInfo.InstalledUICulture);
}
