using System;
using System.Collections.Generic;
using Raylib_cs;
using GodsBoundaryRunner.Core;

namespace GodsBoundaryRunner.Systems
{
    public static class DebugTools
    {
        public static bool ConsoleOpen { get; private set; } = false;
        private static string inputBuffer = "";
        private static List<string> log = new List<string>();

        public static void ToggleConsole()
        {
            ConsoleOpen = !ConsoleOpen;
            inputBuffer = "";
        }

        public static void Log(string s)
        {
            log.Add(s);
            if (log.Count > 50) log.RemoveAt(0);
        }

        public static void Update()
        {
            // Toggle with F12
            if (Raylib.IsKeyPressed(KeyboardKey.F12)) ToggleConsole();

            if (!ConsoleOpen) return;

            foreach (KeyboardKey k in Enum.GetValues(typeof(KeyboardKey)))
            {
                if (Raylib.IsKeyPressed(k))
                {
                    // very small subset: capture letters, digits, backspace, enter
                    if (k >= KeyboardKey.A && k <= KeyboardKey.Z)
                    {
                        char c = (char)('a' + (k - KeyboardKey.A));
                        inputBuffer += c;
                    }
                    else if (k >= KeyboardKey.Zero && k <= KeyboardKey.Nine)
                    {
                        char c = (char)('0' + (k - KeyboardKey.Zero));
                        inputBuffer += c;
                    }
                    else if (k == KeyboardKey.Backspace && inputBuffer.Length > 0)
                    {
                        inputBuffer = inputBuffer.Substring(0, inputBuffer.Length - 1);
                    }
                    else if (k == KeyboardKey.Enter)
                    {
                        Execute(inputBuffer);
                        inputBuffer = "";
                    }
                }
            }
        }

        private static void Execute(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return;
            Log($"> {cmd}");
            var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0].ToLower())
            {
                case "setvol":
                    if (parts.Length >= 2 && float.TryParse(parts[1], out var v))
                    {
                        SettingsManager.Config.MasterVolume = Math.Clamp(v, 0f, 1f);
                        SettingsManager.Save();
                        Raylib.SetMasterVolume(SettingsManager.Config.MasterVolume);
                        Log($"MasterVolume set to {SettingsManager.Config.MasterVolume:F2}");
                    }
                    else Log("usage: setvol 0.0-1.0");
                    break;
                case "help":
                    Log("Commands: help, setvol <0-1>");
                    break;
                default:
                    Log("Unknown command. Type 'help'.");
                    break;
            }
        }

        public static void Draw()
        {
            // Draw profiler top-right
            int w = 220;
            int x = Constants.ScreenWidth - w - 12;
            int y = 12;
            string fps = $"FPS: {Raylib.GetFPS()}";
            string ms = $"Frame: {1000.0f / Math.Max(1, Raylib.GetFPS()):F1} ms";
            Raylib.DrawRectangle(x - 8, y - 8, w + 16, 56, new Color(0,0,0,120));
            Raylib.DrawText(fps, x, y, 16, Color.Lime);
            Raylib.DrawText(ms, x, y + 20, 12, Color.LightGray);

            if (ConsoleOpen)
            {
                // Draw console input and recent logs at bottom
                int ch = 180;
                Raylib.DrawRectangle(20, Constants.ScreenHeight - ch - 20, Constants.ScreenWidth - 40, ch, new Color(5,5,5,200));
                Raylib.DrawText($"> {inputBuffer}_", 30, Constants.ScreenHeight - ch, 14, Color.White);
                for (int i = 0; i < Math.Min(8, log.Count); i++)
                {
                    Raylib.DrawText(log[log.Count - 1 - i], 30, Constants.ScreenHeight - ch + 20 + i * 18, 12, Color.LightGray);
                }
            }
        }
    }
}
