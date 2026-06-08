using System;
using System.IO;

namespace GodsBoundaryRunner.Core
{
    public static class Logger
    {
        private static readonly string logPath = Path.Combine(AppContext.BaseDirectory, "dotnet-run.log");

        public static void Log(string message, Exception? ex = null)
        {
            try
            {
                var text = $"[{DateTime.UtcNow:u}] {message}" + (ex != null ? $" -- {ex}\n" : "\n");
                File.AppendAllText(logPath, text);
            }
            catch { }
        }
    }
}
