using Newtonsoft.Json;
using StudentConnect.Models;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;

namespace StudentConnect.Models.Assistant
{
    public class WorkerHeartbeatInfo
    {
        public string WorkerType { get; set; }
        public string MachineName { get; set; }
        public int ProcessId { get; set; }
        public DateTime LastHeartbeatUtc { get; set; }
        public string LastHeartbeatLocal { get; set; }
        public int ProcessedCount { get; set; }
        public string Status { get; set; }
        public bool IsAlive => (DateTime.UtcNow - LastHeartbeatUtc).TotalSeconds < 120;
    }

    public class AgentSchedulerService : IRegisteredObject
    {
        private static AgentSchedulerService _instance;
        private static readonly object _lock = new object();

        private Timer _timer;
        private bool _isProcessing = false;
        private readonly AssistantService _assistant = new AssistantService();
        private readonly AgentPermissionService _permService = new AgentPermissionService();

        public static DateTime? LastTickAt { get; private set; }
        public static int LastProcessedCount { get; private set; }

        public AgentSchedulerService()
        {
            try
            {
                if (HostingEnvironment.IsHosted)
                {
                    HostingEnvironment.RegisterObject(this);
                }
            }
            catch { }
        }

        public static void Start()
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new AgentSchedulerService();
                    // Khởi chạy sau 10 giây và lặp lại mỗi 30 giây
                    _instance._timer = new Timer(
                        async _ => await _instance.TickAsync(),
                        null,
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(30)
                    );
                }
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                if (_instance != null)
                {
                    _instance.Stop(true);
                    _instance = null;
                }
            }
        }

        public void Stop(bool immediate)
        {
            try
            {
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                _timer?.Dispose();
                _timer = null;
            }
            catch { }

            try
            {
                if (HostingEnvironment.IsHosted)
                {
                    HostingEnvironment.UnregisterObject(this);
                }
            }
            catch { }
        }

        private async Task TickAsync()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                LastTickAt = DateTime.UtcNow;
                int count = await ProcessDueTasksAsync(false, msg => System.Diagnostics.Debug.WriteLine(msg));
                LastProcessedCount = count;

                // Ghi heartbeat cho Web Background Timer
                WriteWorkerHeartbeat("WebBackgroundTimer", count);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AgentSchedulerService] Error: " + ex.Message);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Quét và thực thi các tác vụ đến hạn.
        /// Sử dụng kỹ thuật Atomic Claiming Lease để đảm bảo không bị chạy trùng lặp giữa Web và Worker độc lập.
        /// </summary>
        public async Task<int> ProcessDueTasksAsync(bool isDryRun = false, Action<string> logger = null)
        {
            int totalProcessed = 0;

            using (var db = new TDMUEcoSystemEntities())
            {
                // 0. Phục hồi các tác vụ zombie bị treo quá 10 phút
                try
                {
                    await AgentGuardService.RecoverStaleTasksAsync(db);
                }
                catch (Exception ex)
                {
                    logger?.Invoke($"[RECOVER-ERROR] Lỗi phục hồi tác vụ treo: {ex.Message}");
                }

                var now = DateTime.UtcNow;

                // 1. Quét danh sách TaskID đến hạn (Status = 'Active' và NextRunAt <= UtcNow)
                var candidateTaskIds = await db.AgentTasks
                    .Where(t => t.Status == "Active" && t.NextRunAt != null && t.NextRunAt <= now)
                    .OrderBy(t => t.NextRunAt)
                    .Select(t => t.TaskID)
                    .Take(10)
                    .ToListAsync();

                if (!candidateTaskIds.Any())
                {
                    return 0;
                }

                foreach (var taskId in candidateTaskIds)
                {
                    var task = await db.AgentTasks.FindAsync(taskId);
                    if (task == null) continue;

                    // 2. ATOMIC LOCKING & FENCING: Khóa phân tán qua SQL Server AppLock và tạo RunID độc nhất
                    var lockResult = await AgentGuardService.TryAcquireTaskLockWithFencingAsync(db, taskId, task.UserID, isManual: false);
                    if (!lockResult.acquired)
                    {
                        logger?.Invoke($"[SKIP] Tác vụ #{taskId} dừng: {lockResult.reason}");
                        continue;
                    }

                    long runId = lockResult.runId;

                    // 3. Kiểm tra hạn mức tác vụ & khoảng cách chạy
                    var quotaCheck = await AgentGuardService.CheckCanExecuteTaskAsync(db, task, isManualRun: false);
                    if (!quotaCheck.allowed)
                    {
                        logger?.Invoke($"[QUOTA-SKIP] Tác vụ #{taskId} dừng: {quotaCheck.reason}");
                        task.Status = "Active";
                        if (quotaCheck.reason.Contains("ngày hôm nay"))
                        {
                            task.NextRunAt = AgentGuardService.GetTodayStartUtc().AddDays(1);
                        }
                        else if (quotaCheck.reason.Contains("chờ thêm"))
                        {
                            task.NextRunAt = DateTime.UtcNow.AddMinutes(5);
                        }
                        await AgentGuardService.ReleaseTaskLockAsync(db, task, runId, isSuccess: false, errorMessage: quotaCheck.reason);
                        continue;
                    }

                    // 4. Bỏ qua các lần chạy bị lỡ nếu trễ hơn 30 phút
                    AgentGuardService.SkipMissedRuns(task);

                    logger?.Invoke($"[START] Bắt đầu thực thi tác vụ #{taskId}: {task.TaskName} (User #{task.UserID}, Run #{runId})");

                    // 5. Kiểm tra quyền hạn tự động hóa của User
                    var perm = await _permService.GetOrCreatePermissionsAsync(db, task.UserID);
                    if (perm == null || !perm.IsEnabled || !perm.CanAutomation)
                    {
                        task.Status = "Paused";
                        task.UpdatedAt = DateTime.UtcNow;
                        await AgentGuardService.ReleaseTaskLockAsync(db, task, runId, isSuccess: false, errorMessage: "User chưa cấp quyền Automation.");
                        logger?.Invoke($"[BLOCKED] Tác vụ #{taskId} bị tạm dừng do User #{task.UserID} chưa cấp quyền Automation.");
                        continue;
                    }

                    string runResult = "";
                    string runError = null;
                    bool isSuccess = false;

                    try
                    {
                        runResult = await _assistant.ExecuteAutonomousTaskAsync(task, runId, isDryRun);
                        isSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        isSuccess = false;
                        runError = ex.Message;
                        logger?.Invoke($"[ERROR] Tác vụ #{taskId} gặp lỗi: {ex.Message}");
                    }

                    string finalSummary = (isDryRun ? "[DRY-RUN] " : "") + runResult;

                    // 6. Giải phóng lock an toàn và cập nhật lịch / retry với Fencing Token
                    await AgentGuardService.ReleaseTaskLockAsync(db, task, runId, isSuccess, runError, finalSummary);

                    totalProcessed++;

                    logger?.Invoke($"[DONE] Hoàn tất tác vụ #{taskId}: {(isSuccess ? "Succeeded" : "Failed")}. Chạy tiếp theo: {task.NextRunAt}");
                }
            }

            return totalProcessed;
        }

        /// <summary>
        /// Thực thi thủ công 1 tác vụ theo yêu cầu người dùng (Chạy ngay hoặc Chạy thử Dry-Run)
        /// </summary>
        public async Task<(bool success, string message, AgentTaskRun run)> ExecuteSingleTaskAsync(long taskId, int currentUserId, bool isDryRun = false)
        {
            using (var db = new TDMUEcoSystemEntities())
            {
                var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.TaskID == taskId && t.UserID == currentUserId);
                if (task == null)
                {
                    return (false, "Không tìm thấy tác vụ hoặc bạn không có quyền thực hiện.", null);
                }

                // Kiểm tra quyền
                var perm = await _permService.GetOrCreatePermissionsAsync(db, currentUserId);
                if (perm == null || !perm.IsEnabled || !perm.CanAutomation)
                {
                    return (false, "Bạn chưa cấp quyền 'Tác vụ tự động' (Automation) cho Agent. Vui lòng bật trong Cài đặt quyền Agent.", null);
                }

                long runId = 0;
                using (await AgentGuardService.AcquireUserLockAsync(task.UserID))
                {
                    // Kiểm tra hạn mức tác vụ & số lượng chạy trong ngày
                    var quotaCheck = await AgentGuardService.CheckCanExecuteTaskAsync(db, task, isManualRun: true);
                    if (!quotaCheck.allowed)
                    {
                        return (false, quotaCheck.reason, null);
                    }

                    // Khóa tác vụ (Atomic Cross-Process Lock with Fencing)
                    var lockRes = await AgentGuardService.TryAcquireTaskLockWithFencingAsync(db, taskId, task.UserID, isManual: true);
                    if (!lockRes.acquired)
                    {
                        return (false, lockRes.reason ?? "Tác vụ đang được thực thi bởi một tiến trình khác hoặc đang chạy ngầm. Vui lòng thử lại sau.", null);
                    }
                    runId = lockRes.runId;
                }

                string runResult = "";
                string runError = null;
                bool isSuccess = false;

                try
                {
                    runResult = await _assistant.ExecuteAutonomousTaskAsync(task, runId, isDryRun);
                    isSuccess = true;
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    runError = ex.Message;
                }

                string summary = (isDryRun ? "[CHẠY THỬ / DRY-RUN] " : "") + runResult;

                await AgentGuardService.ReleaseTaskLockAsync(db, task, runId, isSuccess, runError, summary);

                var run = await db.AgentTaskRuns.FindAsync(runId);

                string msg = isSuccess
                    ? (isDryRun ? "Chạy thử nghiệm thành công! Không có bài viết thật nào được tạo." : "Đã thực thi tác vụ thành công!")
                    : ("Thực thi tác vụ thất bại: " + runError);

                return (isSuccess, msg, run);
            }
        }

        /// <summary>
        /// Thực thi tác vụ trực tiếp từ trang Admin (bỏ qua điều kiện chủ sở hữu, thực thi với tư cách Quản trị viên)
        /// </summary>
        public async Task<(bool success, string message, AgentTaskRun run)> ExecuteSingleTaskByAdminAsync(long taskId, int adminUserId, bool isDryRun = false)
        {
            using (var db = new TDMUEcoSystemEntities())
            {
                var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.TaskID == taskId);
                if (task == null)
                {
                    return (false, "Không tìm thấy tác vụ trên hệ thống.", null);
                }

                // Khóa tác vụ (Atomic Cross-Process Lock with Fencing)
                var lockRes = await AgentGuardService.TryAcquireTaskLockWithFencingAsync(db, taskId, task.UserID, isManual: true);
                if (!lockRes.acquired)
                {
                    return (false, lockRes.reason ?? "Tác vụ đang được thực thi bởi một tiến trình khác. Vui lòng chờ tác vụ hoàn tất.", null);
                }

                long runId = lockRes.runId;
                string runResult = "";
                string runError = null;
                bool isSuccess = false;

                try
                {
                    runResult = await _assistant.ExecuteAutonomousTaskAsync(task, runId, isDryRun);
                    isSuccess = true;
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    runError = ex.Message;
                }

                string summary = (isDryRun ? "[CHẠY THỬ / DRY-RUN] " : "") + runResult;

                await AgentGuardService.ReleaseTaskLockAsync(db, task, runId, isSuccess, runError, summary);

                var run = await db.AgentTaskRuns.FindAsync(runId);

                string msg = isSuccess
                    ? (isDryRun ? "Chạy thử nghiệm thành công!" : "Đã thực thi tác vụ thành công!")
                    : ("Thực thi tác vụ thất bại: " + runError);

                return (isSuccess, msg, run);
            }
        }

        /// <summary>
        /// Ghi nhận Heartbeat của Worker (dùng file json chung để Web và Worker cùng nhận biết trạng thái)
        /// </summary>
        public static void WriteWorkerHeartbeat(string workerType, int processedCount)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string appDataPath = Path.Combine(baseDir, "App_Data");

                // Nếu thư mục không có App_Data (ví dụ chạy từ bin\Debug của worker)
                if (!Directory.Exists(appDataPath))
                {
                    string parent = Directory.GetParent(baseDir)?.FullName;
                    if (parent != null && Directory.Exists(Path.Combine(parent, "App_Data")))
                    {
                        appDataPath = Path.Combine(parent, "App_Data");
                    }
                    else
                    {
                        Directory.CreateDirectory(appDataPath);
                    }
                }

                string filePath = Path.Combine(appDataPath, "worker_heartbeat.json");
                var info = new WorkerHeartbeatInfo
                {
                    WorkerType = workerType,
                    MachineName = Environment.MachineName,
                    ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                    LastHeartbeatUtc = DateTime.UtcNow,
                    LastHeartbeatLocal = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"),
                    ProcessedCount = processedCount,
                    Status = "Active"
                };

                File.WriteAllText(filePath, JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch { }
        }

        /// <summary>
        /// Đọc Heartbeat mới nhất để hiển thị trạng thái Worker trên giao diện
        /// </summary>
        public static WorkerHeartbeatInfo GetLatestHeartbeat()
        {
            try
            {
                var candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "worker_heartbeat.json"),
                    @"E:\LTWEB\StudentConnect\StudentConnect.Worker\bin\Debug\App_Data\worker_heartbeat.json",
                    @"E:\LTWEB\StudentConnect\App_Data\worker_heartbeat.json"
                };

                WorkerHeartbeatInfo newest = null;

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            string json = File.ReadAllText(path);
                            var info = JsonConvert.DeserializeObject<WorkerHeartbeatInfo>(json);
                            if (info != null && (newest == null || info.LastHeartbeatUtc > newest.LastHeartbeatUtc))
                            {
                                newest = info;
                            }
                        }
                        catch { }
                    }
                }

                return newest;
            }
            catch { }

            return null;
        }
    }
}
