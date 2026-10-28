using StudentConnect.Models.Assistant;
using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace StudentConnect.Worker
{
    class Program
    {
        private static bool _keepRunning = true;
        private static readonly string LogFilePath = GetLogFilePath();

        static async Task Main(string[] args)
        {
            // Thiết lập TLS 1.2+
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            // Tự động nạp cấu hình bí mật từ Config/AppSettings.config & Config/ConnectionStrings.config nếu chạy local/test
            LoadConfigurationFallbacks();

            // Xử lý cờ lệnh dòng lệnh
            bool runOnce = args.Any(a => a.Equals("--once", StringComparison.OrdinalIgnoreCase));
            bool isDryRun = args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));

            int intervalSeconds = 30;
            string intervalArg = args.FirstOrDefault(a => a.StartsWith("--interval=", StringComparison.OrdinalIgnoreCase));
            if (intervalArg != null)
            {
                int.TryParse(intervalArg.Substring("--interval=".Length), out intervalSeconds);
            }
            else
            {
                int.TryParse(ConfigurationManager.AppSettings["WorkerPollingIntervalSeconds"], out intervalSeconds);
            }
            if (intervalSeconds < 5) intervalSeconds = 5;

            // Đăng ký bắt Ctrl+C để thoát êm
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _keepRunning = false;
                Log(ConsoleColor.Yellow, "\n[WORKER] Nhận tín hiệu dừng (Ctrl+C). Đang hoàn tất chu kỳ hiện tại...");
            };

            // Banner
            PrintBanner(intervalSeconds, runOnce, isDryRun);

            var scheduler = new AgentSchedulerService();

            int cycleCount = 0;

            while (_keepRunning)
            {
                cycleCount++;
                DateTime startTick = DateTime.Now;

                try
                {
                    Log(ConsoleColor.DarkCyan, $"[{startTick:dd/MM/yyyy HH:mm:ss}] [Chu kỳ #{cycleCount}] Đang quét tác vụ đến hạn trong CSDL...");

                    int processed = await scheduler.ProcessDueTasksAsync(isDryRun, msg =>
                    {
                        if (msg.Contains("[START]")) Log(ConsoleColor.Cyan, "  " + msg);
                        else if (msg.Contains("[DONE]")) Log(ConsoleColor.Green, "  " + msg);
                        else if (msg.Contains("[BLOCKED]") || msg.Contains("[SKIP]")) Log(ConsoleColor.Yellow, "  " + msg);
                        else if (msg.Contains("[ERROR]")) Log(ConsoleColor.Red, "  " + msg);
                        else Log(ConsoleColor.Gray, "  " + msg);
                    });

                    // Ghi nhận Heartbeat
                    AgentSchedulerService.WriteWorkerHeartbeat("StandaloneWorker", processed);

                    if (processed > 0)
                    {
                        Log(ConsoleColor.Green, $"[{DateTime.Now:HH:mm:ss}] [Chu kỳ #{cycleCount}] Đã xử lý {processed} tác vụ thành công.");
                    }
                    else
                    {
                        Log(ConsoleColor.DarkGray, $"[{DateTime.Now:HH:mm:ss}] [Chu kỳ #{cycleCount}] Không có tác vụ nào đến hạn.");
                    }
                }
                catch (Exception ex)
                {
                    Log(ConsoleColor.Red, $"[{DateTime.Now:HH:mm:ss}] [ERROR Chu kỳ #{cycleCount}] Lỗi ngoại lệ: {ex.Message}");
                    LogFile($"[EXCEPTION] {ex}");
                }

                if (runOnce)
                {
                    Log(ConsoleColor.Magenta, "\n[WORKER] Đã hoàn thành cờ --once. Thoát tiến trình.");
                    break;
                }

                // Nghỉ giữa các chu kỳ quét
                for (int i = 0; i < intervalSeconds && _keepRunning; i++)
                {
                    await Task.Delay(1000);
                }
            }

            Log(ConsoleColor.Yellow, "[WORKER] Tiến trình Worker đã dừng an toàn.");
        }

        private static void PrintBanner(int interval, bool runOnce, bool dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"==========================================================================");
            Console.WriteLine(@"       TDMU STUDENTCONNECT - AI AGENT MR. T INDEPENDENT WORKER            ");
            Console.WriteLine(@"==========================================================================");
            Console.ResetColor();
            Console.WriteLine($"  - Thời gian khởi động  : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            Console.WriteLine($"  - Chu kỳ quét          : {interval} giây");
            Console.WriteLine($"  - Chế độ               : {(runOnce ? "Chạy 1 lần (--once)" : "Chạy nền liên tục (Loop)")}");
            Console.WriteLine($"  - Chế độ Dry-Run       : {(dryRun ? "BẬT (Không ghi DB)" : "TẮT (Ghi dữ liệu thật)")}");
            Console.WriteLine($"  - File log             : {LogFilePath}");
            Console.WriteLine($"  - Trạng thái           : Sẵn sàng thực thi tác vụ khi web đóng!");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine("  Mẹo: Nhấn Ctrl+C để dừng tiến trình Worker an toàn.");
            Console.WriteLine("--------------------------------------------------------------------------\n");
            Console.ResetColor();
        }

        private static void Log(ConsoleColor color, string message)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;

            LogFile(message);
        }

        private static void LogFile(string message)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private static string GetLogFilePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string appData = Path.Combine(baseDir, "App_Data");
            if (!Directory.Exists(appData))
            {
                string parent = Directory.GetParent(baseDir)?.FullName;
                if (parent != null && Directory.Exists(Path.Combine(parent, "App_Data")))
                {
                    appData = Path.Combine(parent, "App_Data");
                }
            }
            return Path.Combine(appData, "worker.log");
        }

        private static void LoadConfigurationFallbacks()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string configDir = FindConfigDirectory(baseDir);
            if (string.IsNullOrEmpty(configDir)) return;

            // 1. AppSettings fallback (GEMINI_API_KEY & Model)
            string appSettingsPath = Path.Combine(configDir, "AppSettings.config");
            if (File.Exists(appSettingsPath))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(appSettingsPath);
                    var keyNode = doc.SelectSingleNode("//add[@key='GeminiApiKey']");
                    string keyVal = keyNode?.Attributes?["value"]?.Value;
                    if (!string.IsNullOrWhiteSpace(keyVal) && string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["GeminiApiKey"]))
                    {
                        Environment.SetEnvironmentVariable("GEMINI_API_KEY", keyVal);
                        
                        try
                        {
                            var appSettings = ConfigurationManager.AppSettings;
                            var readOnlyField = typeof(NameObjectCollectionBase).GetField("_readOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                            readOnlyField?.SetValue(appSettings, false);
                            appSettings["GeminiApiKey"] = keyVal;
                            readOnlyField?.SetValue(appSettings, true);
                        }
                        catch { }
                    }

                    var modelNode = doc.SelectSingleNode("//add[@key='GeminiModel']");
                    string modelVal = modelNode?.Attributes?["value"]?.Value;
                    if (!string.IsNullOrWhiteSpace(modelVal))
                    {
                        try
                        {
                            var appSettings = ConfigurationManager.AppSettings;
                            var readOnlyField = typeof(NameObjectCollectionBase).GetField("_readOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                            readOnlyField?.SetValue(appSettings, false);
                            appSettings["GeminiModel"] = modelVal;
                            readOnlyField?.SetValue(appSettings, true);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // 2. ConnectionStrings fallback
            string connStringsPath = Path.Combine(configDir, "ConnectionStrings.config");
            if (File.Exists(connStringsPath))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(connStringsPath);
                    var connNode = doc.SelectSingleNode("//add[@name='TDMUEcoSystemEntities']");
                    string connStr = connNode?.Attributes?["connectionString"]?.Value;
                    if (!string.IsNullOrWhiteSpace(connStr))
                    {
                        var currentConn = ConfigurationManager.ConnectionStrings["TDMUEcoSystemEntities"];
                        if (currentConn == null || string.IsNullOrWhiteSpace(currentConn.ConnectionString) || currentConn.ConnectionString.Contains("YOUR_SERVER"))
                        {
                            var coll = ConfigurationManager.ConnectionStrings;
                            var fColl = typeof(ConfigurationElementCollection).GetField("bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                            fColl?.SetValue(coll, false);

                            if (currentConn == null)
                            {
                                coll.Add(new ConnectionStringSettings("TDMUEcoSystemEntities", connStr, "System.Data.EntityClient"));
                            }
                            else
                            {
                                var fElem = typeof(ConfigurationElement).GetField("_bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
                                fElem?.SetValue(currentConn, false);
                                currentConn.ConnectionString = connStr;
                                fElem?.SetValue(currentConn, true);
                            }
                            fColl?.SetValue(coll, true);
                        }
                    }
                }
                catch { }
            }
        }

        private static string FindConfigDirectory(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            for (int i = 0; i < 5 && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, "Config");
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "ConnectionStrings.config")))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
