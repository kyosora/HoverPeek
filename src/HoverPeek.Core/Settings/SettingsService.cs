using System.Text.Json;
using HoverPeek.Core.Localization;

namespace HoverPeek.Core.Settings;

public sealed class SettingsService
{
    public event Action<AppSettings>? SettingsChanged;

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HoverPeek",
        "settings.json");

    private AppSettings _currentSettings;

    public SettingsService()
    {
        _currentSettings = LoadSettings();
    }

    public AppSettings Current => _currentSettings;

    public AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                var defaultSettings = AppSettings.Default;
                SaveSettings(defaultSettings);
                return defaultSettings;
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings ?? AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(SettingsFilePath, json);
            _currentSettings = settings;
            SettingsChanged?.Invoke(settings);
        }
        catch (Exception ex)
        {
            throw new Exception(Strings.Format("SaveSettingsFailed", ex.Message), ex);
        }
    }

    public void UpdateSettings(Action<AppSettings> updateAction)
    {
        var settings = LoadSettings();
        updateAction(settings);
        SaveSettings(settings);
    }
}
