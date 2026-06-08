using System;
using System.IO;
using System.Text.Json;

namespace GodsBoundaryRunner.Core
{
    public class Settings
    {
        public float GravityMultiplier { get; set; } = 1.0f;
        public float MaxRunSpeedMultiplier { get; set; } = 1.0f;
        public float MasterVolume { get; set; } = 1.0f;
        public bool StartFullscreen { get; set; } = true;
    }

    public static class SettingsManager
    {
        private static readonly string configPath = Path.Combine(FilePaths.DataDir, "user_settings.json");
        public static Settings Config { get; private set; } = new Settings();

        public static void Load()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var cfg = JsonSerializer.Deserialize<Settings>(json, JsonOptions.Options);
                    if (cfg != null) Config = cfg;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to load settings", ex);
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Config, JsonOptions.Options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to save settings", ex);
            }
        }
    }
}
