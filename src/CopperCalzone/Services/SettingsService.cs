using System.Text.Json;
using CopperCalzone.Models;

namespace CopperCalzone.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static SettingsService? _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();

    private readonly string _settingsDirPath;
    private readonly string _settingsFilePath;

    public string SettingsFilePath => _settingsFilePath;
    public string SettingsDirectoryPath => _settingsDirPath;

    public AppSettings Current { get; private set; }

    public SettingsService(string? customFilePath = null)
    {
        if (!string.IsNullOrEmpty(customFilePath))
        {
            _settingsFilePath = customFilePath;
            _settingsDirPath = Path.GetDirectoryName(customFilePath) ?? AppContext.BaseDirectory;
        }
        else
        {
            string baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            if (string.IsNullOrWhiteSpace(baseFolder))
            {
                baseFolder = AppContext.BaseDirectory;
            }

            _settingsDirPath = Path.Combine(baseFolder, "CopperCalzone");
            _settingsFilePath = Path.Combine(_settingsDirPath, "settings.json");
        }

        Current = Load();
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    Current = loaded;
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsService] Warning: Failed to load settings from '{_settingsFilePath}': {ex.Message}");
        }

        Current = new AppSettings();
        return Current;
    }

    public bool Save(AppSettings? settings = null)
    {
        try
        {
            if (settings != null)
            {
                Current = settings;
            }

            Directory.CreateDirectory(_settingsDirPath);

            string json = JsonSerializer.Serialize(Current, JsonOptions);
            string tmpPath = _settingsFilePath + ".tmp";

            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _settingsFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsService] Error saving settings to '{_settingsFilePath}': {ex.Message}");
            return false;
        }
    }

    public AppSettings ResetToDefaults()
    {
        Current = new AppSettings();
        Save(Current);
        return Current;
    }
}
