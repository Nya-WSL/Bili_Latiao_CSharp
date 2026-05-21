using System.IO;

namespace Bili_Latiao_CSharp.Services
{
    public static class LogService
    {
        private static readonly string AppFolder = Directory.GetCurrentDirectory();
        private static readonly string LogsFolder = Path.Combine(AppFolder, "logs");
        private static readonly string log_file = Path.Combine(LogsFolder, "bili_latiao.log");

        private static void EnsureLogsFolder()
        {
            if (!Directory.Exists(LogsFolder))
                Directory.CreateDirectory(LogsFolder);
        }

        public static void RotateExistingLog()
        {
            if (File.Exists(log_file))
            {
                var lastWrite = File.GetLastWriteTime(log_file);
                var archiveName = $"bili_latiao_{lastWrite:yyyy-MM-dd-HHmm}.log";
                var archivePath = Path.Combine(LogsFolder, archiveName);
                try
                {
                    File.Move(log_file, archivePath);
                }
                catch { /* 改名失败不影响正常运行 */ }
            }
        }

        public static void Info(string message)
        {
            EnsureLogsFolder();
            string log_entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message} {Environment.NewLine}";
            File.AppendAllText(log_file, log_entry);
        }

        public static void Error(Exception e, string context = "")
        {
            EnsureLogsFolder();
            string log_entry = ($"[ERROR] {context}: {e.Message}\n{e.StackTrace}");
            File.AppendAllText(log_file, log_entry);
        }

        internal static void Error(string errorMsg)
        {
            EnsureLogsFolder();
            string log_entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {errorMsg}{Environment.NewLine}";
            File.AppendAllText(log_file, log_entry);
        }
    }
}
