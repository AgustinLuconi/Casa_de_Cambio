using System;
using System.Diagnostics;
using System.IO;

namespace SistemaCambio.Services
{
    public static class AppLogger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SistemaCambio", "logs");

        public static void Warn(string source, Exception ex)
        {
            Warn(source, $"{ex.GetType().Name}: {ex.Message}");
        }

        public static void Warn(string source, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WARN [{source}] {message}";
            Debug.WriteLine(line);

            try
            {
                Directory.CreateDirectory(LogDirectory);
                var logFile = Path.Combine(LogDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFile, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
