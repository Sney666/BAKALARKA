using System;
using System.Globalization;
using System.IO;
using System.Text;
using RogueCards.Domain;
namespace RogueCards.Persistence
{
    public sealed class FileCombatLogger : ICombatLogger
    {
        private readonly string _logsDir;
        public FileCombatLogger(string logsDir)
        {
            _logsDir = logsDir;
        }

        public string StartNewRunLog()
        {
            Directory.CreateDirectory(_logsDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string filePath = Path.Combine(_logsDir, $"combat_log_{stamp}.txt");
            filePath = EnsureUniquePath(filePath);

            Append(filePath, "=== New Rogue Cards Run Started ===");
            return filePath;
        }

        public void Append(string logPath, string message)
        {
            if (string.IsNullOrWhiteSpace(logPath) || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                string? directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                string cleanMessage = message.Replace("\r", " ").Replace("\n", " ").Trim();
                File.AppendAllText(logPath, $"[{timestamp}] {cleanMessage}{Environment.NewLine}", Encoding.UTF8);
            }
            catch (IOException)
            {
                // Ignore combat logging I/O error to avoid interrupting gameplay.
            }
        }

        private static string EnsureUniquePath(string basePath)
        {
            if (!File.Exists(basePath))
            {
                return basePath;
            }

            string dir = Path.GetDirectoryName(basePath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(basePath);
            string ext = Path.GetExtension(basePath);

            for (int suffix = 1; suffix <= 99; suffix++)
            {
                string candidate = Path.Combine(dir, $"{name}_{suffix}{ext}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return basePath;
        }
    }
}