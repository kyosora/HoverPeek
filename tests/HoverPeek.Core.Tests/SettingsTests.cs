using System.Text.Json;
using HoverPeek.Core.Settings;

namespace HoverPeek.Core.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void Normalize_ClampsAndRepairsValuesWithoutMutatingInput()
    {
        var input = new AppSettings
        {
            HoverDelayMs = 5,
            ImageMaxDimension = 99_999,
            TextFontFamily = " ",
            Language = "unknown",
        };

        var normalized = SettingsLimits.Normalize(input);

        Assert.NotSame(input, normalized);
        Assert.Equal(100, normalized.HoverDelayMs);
        Assert.Equal(4096, normalized.ImageMaxDimension);
        Assert.Equal(AppSettings.Default.TextFontFamily, normalized.TextFontFamily);
        Assert.Equal(AppSettings.Default.Language, normalized.Language);
        Assert.Equal(5, input.HoverDelayMs);
        Assert.Equal(99_999, input.ImageMaxDimension);
        Assert.Equal(" ", input.TextFontFamily);
        Assert.Equal("unknown", input.Language);
    }

    [Fact]
    public void Migrate_VersionOne_UpdatesOldDefaultsAndPreservesCustomValues()
    {
        var old = new AppSettings
        {
            SettingsVersion = 1,
            CenterWindow = true,
            FadeInDurationMs = 150,
            FadeOutDurationMs = 100,
            HoverDelayMs = 500,
            AutoCloseDelayMs = 600,
        };

        var migrated = SettingsLimits.Migrate(old);

        Assert.False(migrated.CenterWindow);
        Assert.Equal(60, migrated.FadeInDurationMs);
        Assert.Equal(60, migrated.FadeOutDurationMs);
        Assert.Equal(400, migrated.HoverDelayMs);
        Assert.Equal(400, migrated.AutoCloseDelayMs);
        Assert.Equal(AppSettings.CurrentVersion, migrated.SettingsVersion);
        Assert.True(old.CenterWindow);

        old.HoverDelayMs = 900;
        Assert.Equal(900, SettingsLimits.Migrate(old).HoverDelayMs);
    }

    [Fact]
    public void Migrate_CurrentVersion_ReturnsSameInstance()
    {
        var current = new AppSettings();

        Assert.Same(current, SettingsLimits.Migrate(current));
    }

    [Fact]
    public void SettingsService_MissingFile_ReturnsDefaults()
    {
        using var temp = new TempDirectory();

        var service = new SettingsService(temp.File("settings.json"));

        Assert.Equal(AppSettings.Default.HoverDelayMs, service.Current.HoverDelayMs);
        Assert.False(File.Exists(service.SettingsFilePath));
    }

    [Fact]
    public void SettingsService_UnversionedVersionOneJson_MigratesAndRewritesFile()
    {
        using var temp = new TempDirectory();
        var path = temp.File("settings.json");
        File.WriteAllText(path, """
            {
              "CenterWindow": true,
              "FadeInDurationMs": 150,
              "FadeOutDurationMs": 100,
              "HoverDelayMs": 500,
              "AutoCloseDelayMs": 600
            }
            """);

        var service = new SettingsService(path);

        Assert.False(service.Current.CenterWindow);
        using var saved = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(AppSettings.CurrentVersion, saved.RootElement.GetProperty("SettingsVersion").GetInt32());
    }

    [Fact]
    public void SettingsService_CorruptJson_ReturnsDefaultsWithoutThrowing()
    {
        using var temp = new TempDirectory();
        var path = temp.File("settings.json");
        File.WriteAllText(path, "{ definitely not json");

        var service = new SettingsService(path);

        Assert.Equal(AppSettings.Default.HoverDelayMs, service.Current.HoverDelayMs);
    }

    [Fact]
    public void SaveSettings_NormalizesRaisesEventAndRemovesTemporaryFile()
    {
        using var temp = new TempDirectory();
        var path = temp.File("settings.json");
        var service = new SettingsService(path);
        AppSettings? observed = null;
        service.SettingsChanged += settings => observed = settings;

        service.SaveSettings(new AppSettings { HoverDelayMs = 5, ImageMaxDimension = 99_999 });

        Assert.NotNull(observed);
        Assert.Equal(100, observed.HoverDelayMs);
        Assert.Equal(4096, observed.ImageMaxDimension);
        Assert.Equal(AppSettings.CurrentVersion, observed.SettingsVersion);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Current_ReturnsCopy()
    {
        using var temp = new TempDirectory();
        var service = new SettingsService(temp.File("settings.json"));
        var copy = service.Current;

        copy.HoverDelayMs = 1_234;

        Assert.NotEqual(1_234, service.Current.HoverDelayMs);
    }
}
