using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Raylib_cs;

namespace GodsBoundaryRunner.Core
{
    public class InputMapping
    {
        public string Action { get; set; } = "";
        public List<KeyboardKey> Keys { get; set; } = new List<KeyboardKey>();
        public List<GamepadButton> Buttons { get; set; } = new List<GamepadButton>();
    }

    public class InputConfig
    {
        public List<InputMapping> Mappings { get; set; } = new List<InputMapping>();
    }

    public static class InputManager
    {
        private static readonly string path = Path.Combine(FilePaths.DataDir, "input_mappings.json");
        private static readonly System.Text.Json.JsonSerializerOptions jsonOptions = JsonOptions.Options;

        private static InputConfig config = CreateDefaultConfig();

        private static InputConfig CreateDefaultConfig()
        {
            return new InputConfig
            {
                Mappings = new List<InputMapping>
                {
                    new InputMapping { Action = "MoveRight", Keys = new List<KeyboardKey>{ KeyboardKey.D, KeyboardKey.Right } , Buttons = new List<GamepadButton>() },
                    new InputMapping { Action = "MoveLeft", Keys = new List<KeyboardKey>{ KeyboardKey.A, KeyboardKey.Left } , Buttons = new List<GamepadButton>() },
                    new InputMapping { Action = "Jump", Keys = new List<KeyboardKey>{ KeyboardKey.W, KeyboardKey.Up, KeyboardKey.Space } , Buttons = new List<GamepadButton>() },
                    new InputMapping { Action = "Down", Keys = new List<KeyboardKey>{ KeyboardKey.S, KeyboardKey.Down, KeyboardKey.LeftControl } , Buttons = new List<GamepadButton>() }
                }
            };
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var cfg = JsonSerializer.Deserialize<InputConfig>(json, jsonOptions);
                    if (cfg != null) config = cfg;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to load input mappings", ex);
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(config, jsonOptions);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to save input mappings", ex);
            }
        }

        private static InputMapping? FindMapping(string action)
        {
            return config.Mappings.Find(m => m.Action == action);
        }

        private static InputMapping EnsureMapping(string action)
        {
            var m = FindMapping(action);
            if (m == null)
            {
                m = new InputMapping { Action = action };
                config.Mappings.Add(m);
            }
            return m;
        }

        public static InputConfig GetConfig() => config;

        public static void SetMapping(string action, List<KeyboardKey> keys, List<GamepadButton> buttons)
        {
            var m = EnsureMapping(action);
            m.Keys = keys.Distinct().ToList();
            m.Buttons = buttons.Distinct().ToList();
            Save();
        }

        public static void AddKey(string action, KeyboardKey key)
        {
            var m = EnsureMapping(action);
            if (!m.Keys.Contains(key)) m.Keys.Add(key);
            Save();
        }

        public static void RemoveKey(string action, KeyboardKey key)
        {
            var m = FindMapping(action);
            if (m == null) return;
            m.Keys.RemoveAll(k => k == key);
            Save();
        }

        public static void AddButton(string action, GamepadButton btn)
        {
            var m = EnsureMapping(action);
            if (!m.Buttons.Contains(btn)) m.Buttons.Add(btn);
            Save();
        }

        public static void RemoveButton(string action, GamepadButton btn)
        {
            var m = FindMapping(action);
            if (m == null) return;
            m.Buttons.RemoveAll(b => b == btn);
            Save();
        }

        public static bool IsDown(string action)
        {
            var map = FindMapping(action);
            if (map == null) return false;

            foreach (var k in map.Keys)
            {
                if (Raylib.IsKeyDown(k)) return true;
            }

            // Gamepad fallback: check any available gamepad
            for (int gid = 0; gid < 4; gid++)
            {
                if (!Raylib.IsGamepadAvailable(gid)) continue;
                foreach (var b in map.Buttons)
                {
                    if (Raylib.IsGamepadButtonDown(gid, b)) return true;
                }
            }

            return false;
        }

        public static bool IsPressed(string action)
        {
            var map = FindMapping(action);
            if (map == null) return false;

            foreach (var k in map.Keys)
            {
                if (Raylib.IsKeyPressed(k)) return true;
            }

            for (int gid = 0; gid < 4; gid++)
            {
                if (!Raylib.IsGamepadAvailable(gid)) continue;
                foreach (var b in map.Buttons)
                {
                    if (Raylib.IsGamepadButtonPressed(gid, b)) return true;
                }
            }

            return false;
        }
    }
}
