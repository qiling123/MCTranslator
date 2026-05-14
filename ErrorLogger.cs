using System;
using System.IO;

namespace MCTranslator
{
    public static class ErrorLogger
    {
        private static readonly string logFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCTranslator", "error.log");

        public static string LogFilePath => logFile;

        public static void Log(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(logFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }
    }
}
