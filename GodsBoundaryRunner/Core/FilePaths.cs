using System;
using System.IO;

namespace GodsBoundaryRunner.Core
{
    public static class FilePaths
    {
        public static readonly string DataDir;

        static FilePaths()
        {
            try
            {
                DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GODS");
                Directory.CreateDirectory(DataDir);
            }
            catch
            {
                // Fallback to base directory if AppData isn't writable
                DataDir = AppContext.BaseDirectory;
            }
        }
    }
}
