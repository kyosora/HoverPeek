using System.Text.Json;
using HoverPeek.Core.Diagnostics;
using HoverPeek.Core.Localization;

namespace HoverPeek.Core.Settings;

public sealed class SettingsService
{
    public event Action<AppSettings>? SettingsChanged;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsFilePath;
    private AppSettings _currentSettings;

    public SettingsService(string? settingsFilePath = null)
    {
        _settingsFilePath = settingsFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HoverPeek",
            "settings.json");
        _currentSettings = LoadSettings();
    }

    /// <summary>目前設定的副本；改它不會影響服務內部狀態，要生效請呼叫 SaveSettings。</summary>
    public AppSettings Current => _currentSettings.Clone();

    public string SettingsFilePath => _settingsFilePath;

    private AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
            return AppSettings.Default;

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.Default;

            // 沒有 SettingsVersion 欄位的舊檔反序列化後會拿到 C# 預設值，這裡用 JSON 判斷真正版本
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(nameof(AppSettings.SettingsVersion), out _))
                loaded.SettingsVersion = 1;

            var upgraded = SettingsLimits.Normalize(SettingsLimits.Migrate(loaded));
            if (upgraded.SettingsVersion != loaded.SettingsVersion)
                TrySave(upgraded);
            return upgraded;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // 設定檔壞掉不該讓程式起不來；保留原檔讓使用者自己看，改用預設值
            AppLog.Warn($"讀取設定檔失敗，改用預設值：{_settingsFilePath}", ex);
            return AppSettings.Default;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = SettingsLimits.Normalize(settings);
        normalized.SettingsVersion = AppSettings.CurrentVersion;

        try
        {
            WriteFile(normalized);
        }
        catch (Exception ex)
        {
            throw new IOException(Strings.Format("SaveSettingsFailed", ex.Message), ex);
        }

        _currentSettings = normalized;
        SettingsChanged?.Invoke(normalized.Clone());
    }

    private void TrySave(AppSettings settings)
    {
        try
        {
            WriteFile(settings);
        }
        catch (Exception ex)
        {
            AppLog.Warn("升級後的設定檔寫回失敗，下次啟動會再升級一次", ex);
        }
    }

    private void WriteFile(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // 先寫暫存檔再取代，寫到一半當機也不會留下半個 JSON
        var temp = _settingsFilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, _settingsFilePath, overwrite: true);
    }
}
