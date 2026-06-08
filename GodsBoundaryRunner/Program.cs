using GodsBoundaryRunner.Systems;

namespace GodsBoundaryRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            // Minimal runtime logging to help diagnose startup failures
            var logPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "dotnet-run.log");
            try
            {
                System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:u}] Starting GodsBoundaryRunner\n");
                GameEngine engine = new GameEngine();
                engine.Initialize();
                engine.Run();
                System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:u}] Exited cleanly\n");
            }
            catch (Exception ex)
            {
                try
                {
                    var msg = $"[{DateTime.UtcNow:u}] Unhandled exception: {ex}\n";
                    System.IO.File.AppendAllText(logPath, msg);
                }
                catch { }
                // Re-throw after logging to preserve default behavior when running interactively
                throw;
            }
        }
    }
}
