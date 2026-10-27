using System.Data.SqlClient;
using System.Configuration;
using StudentConnect.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudentConnect.Models.Assistant
{
    /// <summary>
    /// Dịch vụ trung tâm kiểm soát hạn mức, chống spam mạng xã hội, ngăn chạy trùng lặp
    /// và bảo vệ tài nguyên API Gemini cho hệ thống AI Agent TDMUEcoSystem.
    /// </summary>
    public static class AgentGuardService
    {
        private static readonly TimeZoneInfo SystemTimeZone = GetSystemTimeZone();
        private static readonly SemaphoreSlim _geminiConcurrencyThrottle = new SemaphoreSlim(5, 5);
        private static readonly Semaphore _geminiCrossProcessSemaphore = InitCrossProcessSemaphore();
        private static string _sqlConnectionString;

        // Bộ đếm in-memory kết hợp đồng bộ cơ sở dữ liệu xuyên tiến trình
        private static readonly ConcurrentDictionary<string, int> _dailyUserGeminiCalls = new ConcurrentDictionary<string, int>();
        private static readonly ConcurrentDictionary<string, int> _dailyGlobalGeminiCalls = new ConcurrentDictionary<string, int>();
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> _userActionLocks = new ConcurrentDictionary<int, SemaphoreSlim>();

        private static Semaphore InitCrossProcessSemaphore()
        {
            try
            {
                bool createdNew;
                return new Semaphore(5, 5, @"Global\TDMU_Gemini_GlobalThrottle", out createdNew);
            }
            catch
            {
                return null;
            }
        }

        public static string GetSqlConnectionString()
        {
            if (!string.IsNullOrEmpty(_sqlConnectionString)) return _sqlConnectionString;
            try
            {
                using (var db = new TDMUEcoSystemEntities())
                {
                    _sqlConnectionString = db.Database.Connection.ConnectionString;
                }
            }
            catch
            {
                var setting = ConfigurationManager.ConnectionStrings["TDMUEcoSystemEntities"];
                if (setting != null)
                {
                    var efBuilder = new System.Data.Entity.Core.EntityClient.EntityConnectionStringBuilder(setting.ConnectionString);
                    _sqlConnectionString = efBuilder.ProviderConnectionString;
                }
            }
            return _sqlConnectionString;
        }

        /// <summary>
        /// Khóa tuần tự hóa các hành động ghi dữ liệu của cùng 1 người dùng xuyên mọi tiến trình OS (Web, Worker, Console)
        /// kết hợp In-Process SemaphoreSlim và SQL Server sp_getapplock để ngăn chặn triệt để Race Condition vượt hạn mức.
        /// </summary>
        public static async Task<IDisposable> AcquireUserLockAsync(int userId, int timeoutMs = 10000)
        {
            var sem = _userActionLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
            bool entered = await sem.WaitAsync(timeoutMs);
            if (!entered) throw new TimeoutException("Hệ thống đang xử lý yêu cầu khác của bạn. Vui lòng thử lại sau giây lát.");

            SqlConnection conn = null;
            try
            {
                string connStr = GetSqlConnectionString();
                if (!string.IsNullOrEmpty(connStr))
                {
                    conn = new SqlConnection(connStr);
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            DECLARE @res int;
                            EXEC @res = sp_getapplock @Resource = @lockRes, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = @timeout;
                            SELECT @res;";
                        cmd.Parameters.AddWithValue("@lockRes", $"TDMU_UserLock_{userId}");
                        cmd.Parameters.AddWithValue("@timeout", timeoutMs);

                        int res = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        if (res < 0)
                        {
                            conn.Dispose();
                            sem.Release();
                            throw new TimeoutException($"Không thể nhận khóa người dùng #{userId} xuyên tiến trình (sp_getapplock: {res}).");
                        }
                    }
                }
                return new UserLockReleaser(sem, conn, $"TDMU_UserLock_{userId}");
            }
            catch
            {
                conn?.Dispose();
                sem.Release();
                throw;
            }
        }

        private class UserLockReleaser : IDisposable
        {
            private readonly SemaphoreSlim _sem;
            private readonly SqlConnection _conn;
            private readonly string _lockRes;
            private int _disposed;

            public UserLockReleaser(SemaphoreSlim sem, SqlConnection conn = null, string lockRes = null)
            {
                _sem = sem;
                _conn = conn;
                _lockRes = lockRes;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    try
                    {
                        if (_conn != null && _conn.State == System.Data.ConnectionState.Open && !string.IsNullOrEmpty(_lockRes))
                        {
                            using (var cmd = _conn.CreateCommand())
                            {
                                cmd.CommandText = "EXEC sp_releaseapplock @Resource = @lockRes, @LockOwner = 'Session';";
                                cmd.Parameters.AddWithValue("@lockRes", _lockRes);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        _conn?.Dispose();
                        try { _sem.Release(); } catch { }
                    }
                }
            }
        }

        private static TimeZoneInfo GetSystemTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("VN_UTC7", TimeSpan.FromHours(7), "Vietnam Time", "Vietnam Time");
            }
        }

        /// <summary>
        /// Lấy thời điểm bắt đầu ngày hiện tại (00:00:00) theo giờ Việt Nam quy đổi sang UTC.
        /// Giúp chốt hạn mức ngày chính xác theo múi giờ sinh viên Việt Nam.
        /// </summary>
        public static DateTime GetTodayStartUtc()
        {
            DateTime nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SystemTimeZone);
            DateTime todayStartLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(todayStartLocal, SystemTimeZone);
        }

        // =========================================================================
        // 1. KIỂM SOÁT HẠN MỨC TẠO VÀ THỰC THI TÁC VỤ (TASK QUOTAS)
        // =========================================================================

        /// <summary>
        /// Kiểm tra xem người dùng có đủ điều kiện tạo thêm tác vụ tự động mới hay không.
        /// Mặc định: Tối đa 5 tác vụ đang hoạt động / người dùng.
        /// </summary>
        public static async Task<(bool allowed, string reason)> CheckCanCreateTaskAsync(TDMUEcoSystemEntities db, int userId)
        {
            if (userId <= 0) return (false, "Người dùng không hợp lệ.");

            var config = AgentConfigService.GetConfig();
            if (config.AgentStatus != "Active")
            {
                return (false, $"Trợ lý ảo hiện đang ở trạng thái '{config.AgentStatus}' ({config.StatusReason}). Tạm thời không nhận tác vụ mới.");
            }

            int activeCount = await db.AgentTasks.CountAsync(t => t.UserID == userId && (t.Status == "Active" || t.Status == "Running"));
            if (activeCount >= config.MaxActiveTasksPerUser)
            {
                return (false, $"Bạn đã đạt giới hạn tối đa {config.MaxActiveTasksPerUser} tác vụ đang hoạt động. Vui lòng tạm dừng hoặc xóa bớt tác vụ cũ để tạo mới.");
            }

            return (true, null);
        }

        /// <summary>
        /// Kiểm tra điều kiện để thực thi một tác vụ (Scheduler hoặc Chạy ngay).
        /// Kiểm tra: Bật/tắt toàn hệ thống, quyền Automation, giới hạn 1 tác vụ chạy đồng thời/user,
        /// giới hạn 10 lượt chạy/ngày/user, và khoảng cách tối thiểu 5 phút giữa 2 lần chạy.
        /// </summary>
        public static async Task<(bool allowed, string reason)> CheckCanExecuteTaskAsync(TDMUEcoSystemEntities db, AgentTask task, bool isManualRun)
        {
            if (task == null) return (false, "Tác vụ không tồn tại.");

            var config = AgentConfigService.GetConfig();

            // 1. Kiểm tra trạng thái toàn hệ thống
            if (config.AgentStatus == "Disabled")
            {
                return (false, "Agent toàn hệ thống đang bị vô hiệu hóa bởi Quản trị viên.");
            }
            if (config.AgentStatus == "Maintenance" && !isManualRun)
            {
                return (false, "Agent đang bảo trì hệ thống. Tác vụ tự động tạm hoãn.");
            }

            // 2. Kiểm tra quyền của User
            var perm = await db.AgentPermissions.FirstOrDefaultAsync(p => p.UserID == task.UserID);
            if (perm == null || !perm.IsEnabled || !perm.CanAutomation)
            {
                return (false, "Người dùng chưa được cấp quyền hoặc đã bị thu hồi quyền 'Tác vụ tự động' (Automation).");
            }

            // 3. Mỗi người dùng chỉ có tối đa 1 tác vụ chạy đồng thời
            DateTime staleThreshold = DateTime.UtcNow.AddMinutes(-10);
            bool hasOtherRunning = await db.AgentTaskRuns.AnyAsync(r =>
                r.UserID == task.UserID &&
                r.Status == "Running" &&
                r.StartedAt > staleThreshold
            );
            if (hasOtherRunning)
            {
                return (false, $"Tài khoản của bạn đang có một tác vụ khác đang chạy. Mỗi người dùng chỉ được chạy tối đa {config.MaxConcurrentRunsPerUser} tác vụ tại một thời điểm.");
            }

            // 4. Giới hạn số lượt chạy trong ngày (mặc định 10 lần/ngày)
            DateTime todayStartUtc = GetTodayStartUtc();
            int runsToday = await db.AgentTaskRuns.CountAsync(r =>
                r.UserID == task.UserID &&
                r.StartedAt >= todayStartUtc
            );
            if (runsToday >= config.MaxDailyTaskRunsPerUser)
            {
                return (false, $"Bạn đã đạt hạn mức tối đa {config.MaxDailyTaskRunsPerUser} lượt thực thi tác vụ trong ngày hôm nay. Hạn mức sẽ được làm mới vào 00:00 ngày mai.");
            }

            // 5. Khoảng cách tối thiểu giữa 2 lần chạy cùng tác vụ (mặc định 5 phút)
            if (task.LastRunAt.HasValue)
            {
                double minsSinceLast = (DateTime.UtcNow - task.LastRunAt.Value).TotalMinutes;
                if (minsSinceLast < config.MinIntervalBetweenRunsMinutes)
                {
                    int waitSeconds = (int)Math.Ceiling((config.MinIntervalBetweenRunsMinutes - minsSinceLast) * 60);
                    return (false, $"Tác vụ này vừa mới được chạy cách đây {minsSinceLast:0.0} phút. Vui lòng chờ thêm {waitSeconds} giây trước khi chạy lại (Tối thiểu {config.MinIntervalBetweenRunsMinutes} phút/lần).");
                }
            }

            return (true, null);
        }

        // =========================================================================
        // 2. KHÓA PHÂN TÁN XUYÊN TIẾN TRÌNH & CHỐNG CHẠY TRÙNG (ATOMIC LOCKING)
        // =========================================================================

        /// <summary>
        /// Khóa phân tán nguyên tử với Lease và Fencing Token trên SQL Server.
        /// Đảm bảo duy nhất 1 Worker (Web Timer, Standalone Worker, hoặc RunNow) được thực thi tại 1 thời điểm.
        /// Loại bỏ hoàn toàn lỗ hổng worker khác chiếm lại khóa sau 5s khi tác vụ cũ vẫn đang chạy.
        /// Trả về (acquired, runId, reason). RunID chính là Fencing Token độc nhất của lượt chạy này.
        /// </summary>
        public static async Task<(bool acquired, long runId, string reason)> TryAcquireTaskLockWithFencingAsync(
            TDMUEcoSystemEntities db,
            long taskId,
            int userId,
            bool isManual)
        {
            var config = AgentConfigService.GetConfig();
            int leaseSeconds = Math.Max(10, config.TaskTimeoutSeconds);
            DateTime nowUtc = DateTime.UtcNow;
            DateTime leaseThreshold = nowUtc.AddSeconds(-leaseSeconds);

            string lockResource = $"TDMU_TaskLock_{taskId}";
            string connStr = GetSqlConnectionString();

            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                using (var tran = conn.BeginTransaction(System.Data.IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        // 1. Nhận khóa độc quyền ngắn hạn qua sp_getapplock để tuần tự hóa việc cấp lease
                        using (var cmdLock = conn.CreateCommand())
                        {
                            cmdLock.Transaction = tran;
                            cmdLock.CommandText = @"
                                DECLARE @res int;
                                EXEC @res = sp_getapplock @Resource = @lockRes, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000;
                                SELECT @res;";
                            cmdLock.Parameters.AddWithValue("@lockRes", lockResource);
                            int res = Convert.ToInt32(await cmdLock.ExecuteScalarAsync());
                            if (res < 0)
                            {
                                return (false, 0, "Không thể nhận khóa phân tán cho tác vụ (Hệ thống đang bận).");
                            }
                        }

                        // 2. Kiểm tra task có tồn tại và đang 'Active' không
                        using (var cmdTask = conn.CreateCommand())
                        {
                            cmdTask.Transaction = tran;
                            cmdTask.CommandText = "SELECT Status, NextRunAt, UserID FROM dbo.AgentTasks WITH (UPDLOCK) WHERE TaskID = @taskId";
                            cmdTask.Parameters.AddWithValue("@taskId", taskId);
                            using (var reader = await cmdTask.ExecuteReaderAsync())
                            {
                                if (!await reader.ReadAsync())
                                {
                                    return (false, 0, "Tác vụ không tồn tại.");
                                }
                                string status = reader["Status"]?.ToString();
                                if (status != "Active")
                                {
                                    return (false, 0, $"Tác vụ không ở trạng thái Active (hiện tại: {status}).");
                                }
                                DateTime? nextRunAt = reader["NextRunAt"] as DateTime?;
                                if (!isManual && nextRunAt.HasValue && nextRunAt.Value > nowUtc)
                                {
                                    return (false, 0, "Tác vụ chưa đến hạn chạy theo lịch trình.");
                                }
                            }
                        }

                        // 3. Kiểm tra xem có Run nào đang giữ lease hợp lệ (Status = 'Running' và StartedAt > leaseThreshold)
                        long activeRunId = 0;
                        DateTime activeStartedAt = DateTime.MinValue;
                        using (var cmdRun = conn.CreateCommand())
                        {
                            cmdRun.Transaction = tran;
                            cmdRun.CommandText = @"
                                SELECT TOP 1 RunID, StartedAt 
                                FROM dbo.AgentTaskRuns WITH (UPDLOCK) 
                                WHERE TaskID = @taskId AND Status = 'Running' 
                                ORDER BY StartedAt DESC";
                            cmdRun.Parameters.AddWithValue("@taskId", taskId);
                            using (var reader = await cmdRun.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    activeRunId = Convert.ToInt64(reader["RunID"]);
                                    activeStartedAt = Convert.ToDateTime(reader["StartedAt"]);
                                }
                            }
                        }

                        if (activeRunId > 0)
                        {
                            if (activeStartedAt > leaseThreshold)
                            {
                                // Lease vẫn đang có hiệu lực -> Chặn tuyệt đối mọi worker khác chiếm quyền
                                double remainingSecs = (activeStartedAt.AddSeconds(leaseSeconds) - nowUtc).TotalSeconds;
                                return (false, 0, $"Tác vụ đang được thực thi bởi Run #{activeRunId} (Lease còn {remainingSecs:0}s).");
                            }
                            else
                            {
                                // Worker cũ đã chết hoặc treo quá hạn lease -> Thu hồi (Fencing out) worker cũ
                                using (var cmdExpire = conn.CreateCommand())
                                {
                                    cmdExpire.Transaction = tran;
                                    cmdExpire.CommandText = @"
                                        UPDATE dbo.AgentTaskRuns 
                                        SET Status = 'Failed', FinishedAt = SYSUTCDATETIME(), ErrorMessage = @errMsg 
                                        WHERE RunID = @runId AND Status = 'Running'";
                                    cmdExpire.Parameters.AddWithValue("@errMsg", $"Lease timeout: Tiến trình bị gián đoạn quá {leaseSeconds}s. Khóa được thu hồi.");
                                    cmdExpire.Parameters.AddWithValue("@runId", activeRunId);
                                    await cmdExpire.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // 4. Kiểm tra giới hạn 1 tác vụ chạy đồng thời / user (MaxConcurrentRunsPerUser = 1)
                        using (var cmdUserRun = conn.CreateCommand())
                        {
                            cmdUserRun.Transaction = tran;
                            cmdUserRun.CommandText = @"
                                SELECT COUNT(1) 
                                FROM dbo.AgentTaskRuns WITH (UPDLOCK) 
                                WHERE UserID = @userId AND TaskID <> @taskId AND Status = 'Running' AND StartedAt > @leaseThreshold";
                            cmdUserRun.Parameters.AddWithValue("@userId", userId);
                            cmdUserRun.Parameters.AddWithValue("@taskId", taskId);
                            cmdUserRun.Parameters.AddWithValue("@leaseThreshold", leaseThreshold);
                            int userRunning = Convert.ToInt32(await cmdUserRun.ExecuteScalarAsync());
                            if (userRunning > 0)
                            {
                                return (false, 0, "Tài khoản của bạn đang có một tác vụ khác đang thực thi.");
                            }
                        }

                        // 5. Cập nhật NextRunAt và UpdatedAt của AgentTasks
                        using (var cmdUpdateTask = conn.CreateCommand())
                        {
                            cmdUpdateTask.Transaction = tran;
                            cmdUpdateTask.CommandText = @"
                                UPDATE dbo.AgentTasks 
                                SET NextRunAt = DATEADD(second, @leaseSecs, SYSUTCDATETIME()), UpdatedAt = SYSUTCDATETIME() 
                                WHERE TaskID = @taskId AND Status = 'Active'";
                            cmdUpdateTask.Parameters.AddWithValue("@leaseSecs", leaseSeconds + 60);
                            cmdUpdateTask.Parameters.AddWithValue("@taskId", taskId);
                            await cmdUpdateTask.ExecuteNonQueryAsync();
                        }

                        // 6. Tạo AgentTaskRun mới với trạng thái 'Running' và nhận RunID (Fencing Token)
                        long newRunId = 0;
                        using (var cmdInsertRun = conn.CreateCommand())
                        {
                            cmdInsertRun.Transaction = tran;
                            cmdInsertRun.CommandText = @"
                                DECLARE @prevAttempt int = 0;
                                SELECT TOP 1 @prevAttempt = CASE WHEN Status = 'Failed' THEN AttemptCount ELSE 0 END
                                FROM dbo.AgentTaskRuns WITH (UPDLOCK)
                                WHERE TaskID = @taskId
                                ORDER BY RunID DESC;

                                INSERT INTO dbo.AgentTaskRuns (TaskID, UserID, StartedAt, Status, AttemptCount, CreatedAt)
                                VALUES (@taskId, @userId, SYSUTCDATETIME(), 'Running', ISNULL(@prevAttempt, 0) + 1, SYSUTCDATETIME());
                                SELECT SCOPE_IDENTITY();";
                            cmdInsertRun.Parameters.AddWithValue("@taskId", taskId);
                            cmdInsertRun.Parameters.AddWithValue("@userId", userId);
                            newRunId = Convert.ToInt64(await cmdInsertRun.ExecuteScalarAsync());
                        }

                        tran.Commit();
                        return (true, newRunId, null);
                    }
                    catch (Exception ex)
                    {
                        tran.Rollback();
                        return (false, 0, "Lỗi nhận khóa tác vụ: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Khóa nguyên tử tác vụ trên SQL Server (Phương thức tương thích ngược).
        /// </summary>
        public static async Task<bool> TryAcquireTaskLockAsync(TDMUEcoSystemEntities db, long taskId, bool isManual)
        {
            var task = await db.AgentTasks.FindAsync(taskId);
            if (task == null) return false;
            var res = await TryAcquireTaskLockWithFencingAsync(db, taskId, task.UserID, isManual);
            return res.acquired;
        }

        /// <summary>
        /// Xác minh Fencing Token trước khi thực hiện hành động ghi dữ liệu.
        /// Nếu worker cũ đã mất quyền sở hữu (bị thu hồi lease bởi worker mới hoặc timeout), trả về false.
        /// </summary>
        public static async Task<bool> VerifyFencingTokenAsync(TDMUEcoSystemEntities db, long runId)
        {
            if (runId <= 0) return true;

            string sql = "SELECT Status FROM dbo.AgentTaskRuns WITH (NOLOCK) WHERE RunID = @p0";
            string currentStatus = await db.Database.SqlQuery<string>(sql, runId).FirstOrDefaultAsync();

            return string.Equals(currentStatus, "Running", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gia hạn Lease an toàn (Heartbeat) khi tác vụ đang xử lý các lượt gọi AI kéo dài.
        /// </summary>
        public static async Task<bool> RenewTaskLeaseAsync(TDMUEcoSystemEntities db, long runId)
        {
            if (runId <= 0) return false;

            int affected = await db.Database.ExecuteSqlCommandAsync(@"
                UPDATE dbo.AgentTaskRuns 
                SET StartedAt = SYSUTCDATETIME() 
                WHERE RunID = @p0 AND Status = 'Running'
            ", runId);

            return affected > 0;
        }

        /// <summary>
        /// Giải phóng khóa tác vụ an toàn. Chỉ chủ sở hữu hợp lệ (runId đang 'Running') mới được giải phóng và cập nhật lịch trình.
        /// </summary>
        public static async Task<bool> ReleaseTaskLockAsync(
            TDMUEcoSystemEntities db,
            AgentTask task,
            long runId,
            bool isSuccess,
            string errorMessage = null,
            string resultSummary = null,
            int? overrideAttempt = null)
        {
            var config = AgentConfigService.GetConfig();
            DateTime nowUtc = DateTime.UtcNow;

            if (runId > 0)
            {
                // 1. Chỉ cập nhật AgentTaskRuns nếu chính runId này vẫn đang là 'Running'
                string updateRunSql = @"
                    UPDATE dbo.AgentTaskRuns 
                    SET Status = @p0, FinishedAt = SYSUTCDATETIME(), ErrorMessage = @p1, ResultSummary = @p3 
                    WHERE RunID = @p2 AND Status = 'Running'";
                string finalStatus = isSuccess ? "Succeeded" : "Failed";
                int affected = await db.Database.ExecuteSqlCommandAsync(updateRunSql, finalStatus, errorMessage, runId, (object)resultSummary ?? DBNull.Value);

                if (affected == 0)
                {
                    // Worker này đã bị tước quyền (Fenced out do timeout hoặc zombie recovery). Tuyệt đối KHÔNG ghi đè trạng thái của AgentTasks!
                    return false;
                }
            }

            // 2. Cập nhật AgentTasks
            task.LastRunAt = nowUtc;
            task.UpdatedAt = nowUtc;

            if (isSuccess)
            {
                // Reset và tính NextRunAt theo lịch
                string stLower = (task.ScheduleType ?? "once").ToLowerInvariant();
                if (stLower.Contains("hour") || stLower == "hourly")
                {
                    task.Status = "Active";
                    task.NextRunAt = nowUtc.AddHours(1);
                }
                else if (stLower.Contains("day") || stLower == "daily")
                {
                    task.Status = "Active";
                    task.NextRunAt = nowUtc.AddDays(1);
                }
                else if (stLower.Contains("week") || stLower == "weekly")
                {
                    task.Status = "Active";
                    task.NextRunAt = nowUtc.AddDays(7);
                }
                else if (stLower.Contains("interval") || stLower.Contains("minute"))
                {
                    int mins = 30;
                    int.TryParse(task.ScheduleConfig, out mins);
                    if (mins < config.MinIntervalBetweenRunsMinutes) mins = config.MinIntervalBetweenRunsMinutes;
                    task.Status = "Active";
                    task.NextRunAt = nowUtc.AddMinutes(mins);
                }
                else
                {
                    task.Status = "Completed";
                    task.NextRunAt = null;
                }
            }
            else
            {
                // Thất bại: Áp dụng Exponential Backoff có giới hạn (Tối đa 3 lần retry)
                // Kiểm tra xem lỗi có phải 401/403 (Authentication/Authorization) không -> Dừng ngay
                bool isAuthError = errorMessage != null && (errorMessage.Contains("401") || errorMessage.Contains("403") || errorMessage.Contains("Permission"));
                int currentAttempt = overrideAttempt ?? 1;
                if (!overrideAttempt.HasValue && runId > 0)
                {
                    var run = await db.AgentTaskRuns.FindAsync(runId);
                    if (run != null) currentAttempt = run.AttemptCount;
                }

                if (!isAuthError && currentAttempt <= config.MaxTaskRetryAttempts)
                {
                    int backoffMins = Math.Min(30, (int)Math.Pow(2, currentAttempt - 1) * 5);
                    task.Status = "Active";
                    task.NextRunAt = nowUtc.AddMinutes(backoffMins);
                }
                else
                {
                    task.Status = "Paused";
                    task.NextRunAt = null;
                }
            }

            await db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Giải phóng khóa tác vụ (Phương thức tương thích ngược).
        /// </summary>
        public static async Task ReleaseTaskLockAsync(
            TDMUEcoSystemEntities db,
            AgentTask task,
            AgentTaskRun run,
            bool isSuccess,
            string errorMessage = null)
        {
            long runId = run != null ? run.RunID : 0;
            string summary = run != null ? run.ResultSummary : null;
            int? attempt = run != null ? (int?)run.AttemptCount : null;
            await ReleaseTaskLockAsync(db, task, runId, isSuccess, errorMessage, summary, attempt);
        }

        /// <summary>
        /// Tự động phục hồi các tác vụ bị treo (Zombie Tasks) do timeout, mất mạng hoặc ứng dụng khởi động lại.
        /// </summary>
        public static async Task<int> RecoverStaleTasksAsync(TDMUEcoSystemEntities db)
        {
            DateTime staleThreshold = DateTime.UtcNow.AddMinutes(-10);

            // 1. Cập nhật các run đang bị treo sang 'Failed'
            var staleRuns = await db.AgentTaskRuns
                .Where(r => r.Status == "Running" && r.StartedAt < staleThreshold)
                .ToListAsync();

            foreach (var r in staleRuns)
            {
                r.Status = "Failed";
                r.FinishedAt = DateTime.UtcNow;
                r.ErrorMessage = "Tác vụ bị gián đoạn do timeout quá 10 phút hoặc ứng dụng khởi động lại.";
            }

            if (staleRuns.Any())
            {
                await db.SaveChangesAsync();
            }

            return staleRuns.Count;
        }

        /// <summary>
        /// Bỏ qua các lịch chạy bị bỏ lỡ quá xa trong quá khứ (không chạy dồn dập hàng loạt sau khi bật lại server).
        /// </summary>
        public static void SkipMissedRuns(AgentTask task)
        {
            if (task == null || !task.NextRunAt.HasValue) return;

            DateTime nowUtc = DateTime.UtcNow;
            if (task.NextRunAt.Value < nowUtc.AddMinutes(-30))
            {
                // Lịch đã quá hạn hơn 30 phút -> Nhảy thẳng sang mốc thời gian tiếp theo
                task.NextRunAt = nowUtc.AddMinutes(2);
            }
        }

        // =========================================================================
        // 3. CHỐNG SPAM MẠNG XÃ HỘI & PHÁT HIỆN TRÙNG LẶP (ANTI-SPAM & SIMILARITY)
        // =========================================================================

        /// <summary>
        /// Kiểm tra bài viết có vi phạm hạn mức đăng bài hoặc trùng lặp với bài gần đây không.
        /// </summary>
        public static async Task<(bool allowed, string reason)> CheckPostSpamAsync(TDMUEcoSystemEntities db, int userId, string content)
        {
            if (userId <= 0) return (false, "Yêu cầu đăng nhập.");

            var config = AgentConfigService.GetConfig();
            DateTime todayStart = GetTodayStartUtc();

            // 1. Hạn mức đăng bài trong ngày qua Agent (mặc định 5 bài/ngày)
            int postsToday = await db.AgentActionLogs.CountAsync(l =>
                l.UserID == userId &&
                l.ActionName == "create_post" &&
                l.Status == "Succeeded" &&
                l.CreatedAt >= todayStart
            );
            if (postsToday >= config.MaxDailyPostsPerUser)
            {
                return (false, $"Tài khoản đã đạt hạn mức tối đa {config.MaxDailyPostsPerUser} bài viết được đăng tự động qua Agent trong ngày. Vui lòng thử lại vào ngày mai.");
            }

            // 2. Kiểm tra trùng lặp nội dung với các bài viết gần đây của người dùng
            string normNew = NormalizeTextForComparison(content);
            if (string.IsNullOrEmpty(normNew)) return (false, "Nội dung bài viết không được để trống.");

            DateTime windowStartUtc = DateTime.UtcNow.AddHours(-config.DuplicateCheckWindowHours);
            DateTime windowStartLocal = DateTime.Now.AddHours(-config.DuplicateCheckWindowHours);
            var recentPosts = await db.Posts
                .Where(p => p.UserID == userId && (p.CreatedAt >= windowStartUtc || p.CreatedAt >= windowStartLocal))
                .Select(p => p.Content)
                .ToListAsync();

            foreach (var oldContent in recentPosts)
            {
                string normOld = NormalizeTextForComparison(oldContent);
                if (normNew == normOld)
                {
                    return (false, $"Phát hiện nội dung trùng lặp 100% với một bài viết bạn đã đăng trong vòng {config.DuplicateCheckWindowHours} giờ qua. Vui lòng đổi nội dung mới.");
                }

                double similarity = CalculateStringSimilarity(normNew, normOld);
                if (similarity >= config.DuplicateSimilarityThreshold)
                {
                    return (false, $"Nội dung bài viết quá giống ({similarity * 100:0.0}%) với bài viết bạn đã đăng gần đây. Vui lòng điều chỉnh để tránh bị hệ thống coi là spam.");
                }
            }

            return (true, null);
        }

        /// <summary>
        /// Kiểm tra bình luận có vi phạm hạn mức hoặc trùng lặp trên cùng bài viết hay không.
        /// </summary>
        public static async Task<(bool allowed, string reason)> CheckCommentSpamAsync(TDMUEcoSystemEntities db, int userId, int postId, string content)
        {
            var config = AgentConfigService.GetConfig();
            DateTime todayStart = GetTodayStartUtc();

            int commentsToday = await db.AgentActionLogs.CountAsync(l =>
                l.UserID == userId &&
                l.ActionName == "add_comment" &&
                l.Status == "Succeeded" &&
                l.CreatedAt >= todayStart
            );
            if (commentsToday >= config.MaxDailyCommentsPerUser)
            {
                return (false, $"Tài khoản đã đạt hạn mức tối đa {config.MaxDailyCommentsPerUser} bình luận/ngày qua Agent.");
            }

            // Kiểm tra trùng lặp bình luận trên cùng bài viết trong vòng 2 giờ
            string normNew = NormalizeTextForComparison(content);
            DateTime windowStartUtc = DateTime.UtcNow.AddHours(-config.DuplicateCheckWindowHours);
            DateTime windowStartLocal = DateTime.Now.AddHours(-config.DuplicateCheckWindowHours);
            var recentComments = await db.PostComments
                .Where(c => c.UserID == userId && c.PostID == postId && (c.CreatedAt >= windowStartUtc || c.CreatedAt >= windowStartLocal))
                .Select(c => c.Content)
                .ToListAsync();

            foreach (var oldComment in recentComments)
            {
                string normOld = NormalizeTextForComparison(oldComment);
                if (normNew == normOld || CalculateStringSimilarity(normNew, normOld) >= 0.90)
                {
                    return (false, "Bình luận này đã được bạn gửi trên bài viết gần đây. Tránh gửi lặp lại.");
                }
            }

            return (true, null);
        }

        /// <summary>
        /// Kiểm tra hạn mức tương tác (Thích/React) trong ngày.
        /// </summary>
        public static async Task<(bool allowed, string reason)> CheckReactionSpamAsync(TDMUEcoSystemEntities db, int userId)
        {
            var config = AgentConfigService.GetConfig();
            DateTime todayStart = GetTodayStartUtc();

            int reactsToday = await db.AgentActionLogs.CountAsync(l =>
                l.UserID == userId &&
                l.ActionName == "react_to_post" &&
                l.Status == "Succeeded" &&
                l.CreatedAt >= todayStart
            );
            if (reactsToday >= config.MaxDailyReactionsPerUser)
            {
                return (false, $"Tài khoản đã đạt hạn mức tối đa {config.MaxDailyReactionsPerUser} lượt thích/ngày qua Agent.");
            }

            return (true, null);
        }

        /// <summary>
        /// Kiểm tra hạn mức Theo dõi (Follow) trong ngày.
        /// </summary>
        public static async Task<(bool allowed, string reason)> CheckFollowSpamAsync(TDMUEcoSystemEntities db, int userId)
        {
            var config = AgentConfigService.GetConfig();
            DateTime todayStart = GetTodayStartUtc();

            int followsToday = await db.AgentActionLogs.CountAsync(l =>
                l.UserID == userId &&
                l.ActionName == "follow_user" &&
                l.Status == "Succeeded" &&
                l.CreatedAt >= todayStart
            );
            if (followsToday >= config.MaxDailyFollowsPerUser)
            {
                return (false, $"Tài khoản đã đạt hạn mức tối đa {config.MaxDailyFollowsPerUser} lượt theo dõi/ngày qua Agent.");
            }

            return (true, null);
        }

        /// <summary>
        /// Chuẩn hóa chuỗi để so sánh chống spam: chuyển FormC Unicode, viết thường, gộp khoảng trắng liên tiếp và cắt viền.
        /// Giúp phát hiện các biến thể Unicode (NFC vs NFD), khoảng trắng tab/newline hoặc ký tự tàng hình.
        /// </summary>
        public static string NormalizeTextForComparison(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string normalized = input.Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
            return normalized;
        }

        /// <summary>
        /// Tính toán độ tương đồng giữa hai chuỗi văn bản theo thuật toán Levenshtein Distance chuẩn hóa (0.0 đến 1.0).
        /// </summary>
        public static double CalculateStringSimilarity(string s1, string s2)
        {
            string a = NormalizeTextForComparison(s1);
            string b = NormalizeTextForComparison(s2);

            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

            if (a == b) return 1.0;

            int n = a.Length;
            int m = b.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (b[j - 1] == a[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            int distance = d[n, m];
            int maxLen = Math.Max(n, m);
            return 1.0 - ((double)distance / maxLen);
        }

        // =========================================================================
        // 4. ĐỒNG BỘ THU HỒI QUYỀN (PERMISSION REVOCATION SYNCHRONIZATION)
        // =========================================================================

        /// <summary>
        /// Khi quyền của người dùng bị tắt hoặc thu hồi (CanAutomation = false hoặc IsEnabled = false),
        /// tự động chuyển toàn bộ tác vụ đang 'Active' của người đó sang 'Paused'.
        /// </summary>
        public static async Task<int> OnUserPermissionChangedAsync(TDMUEcoSystemEntities db, int userId, bool isEnabled, bool canAutomation)
        {
            if (!isEnabled || !canAutomation)
            {
                int pausedCount = await db.Database.ExecuteSqlCommandAsync(@"
                    UPDATE dbo.AgentTasks 
                    SET Status = 'Paused', UpdatedAt = SYSUTCDATETIME() 
                    WHERE UserID = @p0 AND Status IN ('Active', 'Running')
                ", userId);

                return pausedCount;
            }
            return 0;
        }

        // =========================================================================
        // 5. BẢO VỆ TÀI NGUYÊN & HẠN MỨC GEMINI API (RESOURCE PROTECTION)
        // =========================================================================

        public static (bool allowed, string reason) CheckGeminiQuota(int userId)
        {
            var config = AgentConfigService.GetConfig();
            DateTime todayStartUtc = GetTodayStartUtc();

            // 1. Kiểm tra qua cơ sở dữ liệu để đồng bộ chính xác giữa Web và Worker
            try
            {
                string connStr = GetSqlConnectionString();
                if (!string.IsNullOrEmpty(connStr))
                {
                    using (var conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                                SELECT 
                                    COUNT(CASE WHEN ActionName = 'gemini_call' THEN 1 END) AS GlobalCount,
                                    COUNT(CASE WHEN ActionName = 'gemini_call' AND UserID = @userId THEN 1 END) AS UserCount
                                FROM dbo.AgentActionLogs WITH (NOLOCK)
                                WHERE ActionName = 'gemini_call' AND CreatedAt >= @todayStart";
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@todayStart", todayStartUtc);
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    int globalDb = Convert.ToInt32(reader["GlobalCount"]);
                                    int userDb = Convert.ToInt32(reader["UserCount"]);

                                    if (globalDb >= config.MaxDailyGeminiRequestsGlobal)
                                    {
                                        return (false, "Hệ thống AI Agent đã đạt giới hạn ngân sách API toàn cầu trong ngày hôm nay. Vui lòng quay lại vào ngày mai.");
                                    }
                                    if (userId > 0 && userDb >= config.MaxDailyGeminiRequestsPerUser)
                                    {
                                        return (false, $"Bạn đã đạt hạn mức tối đa {config.MaxDailyGeminiRequestsPerUser} lượt hỏi đáp AI trong ngày hôm nay.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Kiểm tra bổ trợ in-memory
            string todayKey = DateTime.UtcNow.ToString("yyyyMMdd");
            string userKey = $"{userId}_{todayKey}";

            int globalCount = _dailyGlobalGeminiCalls.GetOrAdd(todayKey, 0);
            if (globalCount >= config.MaxDailyGeminiRequestsGlobal)
            {
                return (false, "Hệ thống AI Agent đã đạt giới hạn ngân sách API toàn cầu trong ngày hôm nay. Vui lòng quay lại vào ngày mai.");
            }

            if (userId > 0)
            {
                int userCount = _dailyUserGeminiCalls.GetOrAdd(userKey, 0);
                if (userCount >= config.MaxDailyGeminiRequestsPerUser)
                {
                    return (false, $"Bạn đã đạt hạn mức tối đa {config.MaxDailyGeminiRequestsPerUser} lượt hỏi đáp AI trong ngày hôm nay.");
                }
            }

            return (true, null);
        }

        public static void RecordGeminiCall(int userId)
        {
            string todayKey = DateTime.UtcNow.ToString("yyyyMMdd");
            string userKey = $"{userId}_{todayKey}";

            _dailyGlobalGeminiCalls.AddOrUpdate(todayKey, 1, (_, old) => old + 1);
            if (userId > 0)
            {
                _dailyUserGeminiCalls.AddOrUpdate(userKey, 1, (_, old) => old + 1);
            }

            // Ghi nhận đồng bộ vào database để các tiến trình khác nhìn thấy ngay
            try
            {
                string connStr = GetSqlConnectionString();
                if (!string.IsNullOrEmpty(connStr))
                {
                    using (var conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = @"
                                INSERT INTO dbo.AgentActionLogs (UserID, ActionName, TargetType, Status, CreatedAt)
                                VALUES (@userId, 'gemini_call', 'GeminiApi', 'Succeeded', SYSUTCDATETIME())";
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch { }
        }

        public static async Task<bool> AcquireGeminiThrottleAsync(int timeoutMs = 15000)
        {
            bool inProcAcquired = await _geminiConcurrencyThrottle.WaitAsync(timeoutMs);
            if (!inProcAcquired) return false;

            if (_geminiCrossProcessSemaphore != null)
            {
                try
                {
                    bool crossAcquired = await Task.Run(() => _geminiCrossProcessSemaphore.WaitOne(timeoutMs));
                    if (!crossAcquired)
                    {
                        _geminiConcurrencyThrottle.Release();
                        return false;
                    }
                }
                catch
                {
                    // Fallback to in-proc
                }
            }
            return true;
        }

        public static void ReleaseGeminiThrottle()
        {
            try
            {
                _geminiCrossProcessSemaphore?.Release();
            }
            catch { }
            try
            {
                _geminiConcurrencyThrottle.Release();
            }
            catch { }
        }
    }
}
