using System.Text.Json;

namespace MicSentry;

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public int IdleMinutes { get; set; } = 5;
    public bool StartWithWindows { get; set; }
    public bool CheckForUpdates { get; set; }
    public List<string> ExcludedDeviceIds { get; set; } = new();

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MicSentry", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // corrupt or unreadable settings file — fall back to defaults rather than crash
        }

        return new AppSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
