using System;
using System.IO;
using Rage;

namespace EmsMod.Utils
{
    public static class Log
    {
        private const string Prefix = "[EmsMod]";

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(Exception ex, string context)
        {
            Write("ERROR", $"{context}: {ex}");
        }

        private static void Write(string level, string message)
        {
            string line = $"{Prefix} [{level}] {message}";

            Game.LogTrivial(line);
            AppendToFile(line);
        }

        private static void AppendToFile(string line)
        {
            try
            {
                Directory.CreateDirectory(ModPaths.LogsDirectory);
                string path = Path.Combine(ModPaths.LogsDirectory, "EmsMod.log");
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never itself throw and take down the plugin.
            }
        }
    }
}
