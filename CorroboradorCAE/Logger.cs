using System;
using System.IO;

namespace SBOAddonProject1
{
    public static class Logger
    {
        private static readonly string LogDir = @"C:\ProgramData\CorroboradorCAE";
        private static readonly string LogPath = Path.Combine(LogDir, "Log_Front.txt");

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Error(string message, Exception ex = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $" | Exception: {ex.ToString()}";
            }
            WriteLog("ERROR", fullMessage);
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                if (!Directory.Exists(LogDir))
                {
                    Directory.CreateDirectory(LogDir);
                }

                string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, logLine);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error al escribir log en {LogPath}:\n{ex.Message}", "Error de Logger", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        public static string GetLogPath()
        {
            return LogPath;
        }
    }
}
