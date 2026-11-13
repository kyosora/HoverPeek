using System.Globalization;
using HoverPeek.Core.Localization;
using HoverPeek.Core.Settings;

namespace HoverPeek.Core.Tests;

public sealed class SupportedLanguagesTests
{
    [Theory]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("zh-HK", "zh-TW")]
    [InlineData("zh-MO", "zh-TW")]
    [InlineData("zh-Hant", "zh-TW")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-SG", "zh-CN")]
    [InlineData("zh-Hans", "zh-CN")]
    [InlineData("ja-JP", "ja")]
    [InlineData("ko-KR", "ko")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("fr-FR", "en")] // 不支援的語言退回英文
    public void Detect_MapsWindowsDisplayLanguage(string culture, string expected)
    {
        Assert.Equal(expected, SupportedLanguages.Detect(new CultureInfo(culture)));
    }

    [Fact]
    public void All_CodesAreUniqueAndValidCultures()
    {
        var codes = SupportedLanguages.All.Select(l => l.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var code in codes)
            Assert.Equal(code, new CultureInfo(code).Name);
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("ja")]
    [InlineData("ko")]
    public void Normalize_KeepsNewlySupportedLanguages(string code)
    {
        var settings = AppSettings.Default.Clone();
        settings.Language = code;

        Assert.Equal(code, SettingsLimits.Normalize(settings).Language);
    }

    [Fact]
    public void EveryLanguage_HasAResourceForEveryKey()
    {
        // 每種語言都要能取到每個字串，且不是退回 key 名稱本身（代表 resx 漏了）
        var keys = typeof(Strings).GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.GetMethod?.IsStatic == true)
            .Select(p => p.Name)
            .ToList();
        Assert.NotEmpty(keys);

        var manager = new System.Resources.ResourceManager("HoverPeek.Core.Localization.Strings", typeof(Strings).Assembly);
        foreach (var language in SupportedLanguages.All)
        {
            var culture = new CultureInfo(language.Code);
            var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
            var neutral = language.Code == "zh-TW"; // 繁中是 neutral 資源（Strings.resx），沒有獨立 resource set
            foreach (var key in keys)
            {
                var value = neutral ? manager.GetString(key, CultureInfo.InvariantCulture) : set?.GetString(key);
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language.Code} 缺少字串 {key}");
            }
        }
    }
}
