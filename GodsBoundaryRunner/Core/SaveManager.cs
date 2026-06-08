using System;
using System.IO;
using System.Text.Json;

namespace GodsBoundaryRunner.Core
{
    public class SaveData
    {
        public float LastCheckpointX { get; set; } = 200f;
        public int Ankhs { get; set; } = 0;
        public LevelID CurrentLevel { get; set; } = LevelID.Tehuti;
    }

    public static class SaveManager
    {
        private static readonly string path = Path.Combine(FilePaths.DataDir, "save_slot.json");
        public static SaveData Data { get; private set; } = new SaveData();

        public static void Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var d = JsonSerializer.Deserialize<SaveData>(json, JsonOptions.Options);
                    if (d != null) Data = d;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to load save data", ex);
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Data, JsonOptions.Options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to save data", ex);
            }
        }
    }
}
