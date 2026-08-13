// settings_service.cs — saves app settings to %appdata%\PZManager\settings.json
using PZManager.Models;
using System.IO;
using System.Text.Json;

namespace PZManager.Services
{
    public static class SettingsService
    {
        private static readonly string settings_path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PZManager", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(settings_path))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settings_path)) ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settings_path)!);
                File.WriteAllText(settings_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
