using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StudentConnect.Models;
using StudentConnect.Models.Assistant;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class AssistantController : BaseController
    {
        private readonly AssistantService _assistant = new AssistantService();
        private readonly AgentPermissionService _permService = new AgentPermissionService();
        private readonly AgentAuditService _auditService = new AgentAuditService();
        private readonly AgentConfirmationService _confirmService = new AgentConfirmationService();
        private readonly AgentConversationService _convService = new AgentConversationService();
        private readonly SocialTools _tools = new SocialTools();

        // GET: Assistant
        [HttpGet]
        public ActionResult Index(long? conversationId = null)
        {
            ViewBag.AgentName = _assistant.AgentName;
            ViewBag.ActiveTab = "Assistant";
            ViewBag.InitialConversationId = conversationId;
            return View();
        }

        // POST: Assistant/Ask
        [HttpPost]
        public async Task<JsonResult> Ask(ChatRequest request)
        {
            string agentName = _assistant.AgentName ?? "Trợ lý ảo Mr. T";

            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new ChatResponse
                {
                    AgentName = agentName,
                    Success = false,
                    Error = "Vui lòng nhập nội dung câu hỏi."
                });
            }

            int currentUserId = Session["UserID"] as int? ?? 0;

            try
            {
                // Nếu người dùng đã đăng nhập, tự động duy trì hoặc tạo phiên hội thoại
                if (currentUserId > 0)
                {
                    long targetConvId = request.ConversationId ?? 0;

                    using (var db = new TDMUEcoSystemEntities())
                    {
                        if (targetConvId <= 0)
                        {
                            // Tạo phiên mới với tiêu đề lấy từ tin nhắn đầu tiên
                            string title = request.Message.Trim();
                            if (title.Length > 80) title = title.Substring(0, 80) + "...";
                            var newConv = await _convService.CreateConversationAsync(db, currentUserId, title);
                            targetConvId = newConv.ConversationID;
                        }
                        else
                        {
                            // Xác thực quyền sở hữu phiên
                            var existing = await _convService.GetConversationByIdAsync(db, targetConvId, currentUserId);
                            if (existing == null)
                            {
                                string title = request.Message.Trim();
                                if (title.Length > 80) title = title.Substring(0, 80) + "...";
                                var newConv = await _convService.CreateConversationAsync(db, currentUserId, title);
                                targetConvId = newConv.ConversationID;
                            }
                        }
                    }

                    var response = await _assistant.AskInConversationAsync(request.Message, currentUserId, targetConvId);
                    response.ConversationId = targetConvId;
                    return Json(response);
                }
                else
                {
                    // Khách vãng lai: hỏi đáp trực tiếp không lưu database
                    var response = await _assistant.AskWithContextAsync(request.Message, currentUserId);
                    return Json(response);
                }
            }
            catch (ArgumentException ex)
            {
                return Json(new ChatResponse
                {
                    AgentName = agentName,
                    Success = false,
                    Error = ex.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new ChatResponse
                {
                    AgentName = agentName,
                    Success = false,
                    Error = "Không thể xử lý yêu cầu lúc này: " + ex.Message
                });
            }
        }

        // GET: Assistant/GetConversations
        [HttpGet]
        public async Task<JsonResult> GetConversations(int page = 1, int pageSize = 15, string keyword = null, string status = null)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để xem danh sách phiên hội thoại." }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                using (var db = new TDMUEcoSystemEntities())
                {
                    var list = await _convService.GetConversationsAsync(db, currentUserId, page, pageSize, keyword, status);
                    int total = await _convService.CountConversationsAsync(db, currentUserId, keyword, status);

                    var data = list.Select(c => new
                    {
                        c.ConversationID,
                        c.Title,
                        c.TaskID,
                        c.Status,
                        c.Summary,
                        c.MessageCount,
                        CreatedAt = c.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        LastMessageAt = c.LastMessageAt.ToString("dd/MM/yyyy HH:mm"),
                        UpdatedAt = c.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
                    }).ToList();

                    return Json(new
                    {
                        success = true,
                        data = data,
                        total = total,
                        page = page,
                        pageSize = pageSize,
                        totalPages = (int)Math.Ceiling((double)total / pageSize)
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi nạp danh sách phiên hội thoại: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Assistant/GetConversationDetail
        [HttpGet]
        public async Task<JsonResult> GetConversationDetail(long conversationId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);
            }

            try
            {
                using (var db = new TDMUEcoSystemEntities())
                {
                    var conv = await _convService.GetConversationByIdAsync(db, conversationId, currentUserId);
                    if (conv == null)
                    {
                        return Json(new { success = false, message = "Phiên hội thoại không tồn tại hoặc bạn không có quyền truy cập." }, JsonRequestBehavior.AllowGet);
                    }

                    var messages = await _convService.GetMessagesAsync(db, conversationId, currentUserId, 0, 100);

                    // Lấy thông tin tác vụ lịch liên kết nếu có
                    object linkedTask = null;
                    if (conv.TaskID.HasValue && conv.TaskID.Value > 0)
                    {
                        var task = await db.AgentTasks.AsNoTracking().FirstOrDefaultAsync(t => t.TaskID == conv.TaskID.Value && t.UserID == currentUserId);
                        if (task != null)
                        {
                            linkedTask = new
                            {
                                task.TaskID,
                                task.TaskName,
                                task.UserPrompt,
                                task.ScheduleType,
                                task.ScheduleConfig,
                                task.Status,
                                NextRunAt = task.NextRunAt.HasValue ? task.NextRunAt.Value.ToString("dd/MM/yyyy HH:mm") : null,
                                LastRunAt = task.LastRunAt.HasValue ? task.LastRunAt.Value.ToString("dd/MM/yyyy HH:mm") : null
                            };
                        }
                    }

                    // Lấy các action log phát sinh từ phiên này (materialize trước khi format CreatedAt)
                    var rawLogs = await db.AgentActionLogs
                        .AsNoTracking()
                        .Where(l => l.ConversationID == conversationId && l.UserID == currentUserId)
                        .OrderBy(l => l.CreatedAt)
                        .ToListAsync();

                    var linkedLogs = rawLogs.Select(l => new
                    {
                        l.LogID,
                        l.ActionName,
                        l.TargetType,
                        l.TargetID,
                        l.Status,
                        l.ResultSummary,
                        l.ErrorMessage,
                        CreatedAt = l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")
                    }).ToList();

                    var formattedMessages = messages.Select(m => new
                    {
                        m.MessageID,
                        m.SenderType,
                        m.Content,
                        m.ToolCallDetails,
                        m.ToolResultDetails,
                        m.ErrorMessage,
                        CreatedAt = m.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")
                    }).ToList();

                    return Json(new
                    {
                        success = true,
                        data = new
                        {
                            conv.ConversationID,
                            conv.Title,
                            conv.TaskID,
                            conv.Status,
                            conv.Summary,
                            conv.MessageCount,
                            CreatedAt = conv.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                            LastMessageAt = conv.LastMessageAt.ToString("dd/MM/yyyy HH:mm:ss"),
                            Messages = formattedMessages,
                            LinkedTask = linkedTask,
                            LinkedLogs = linkedLogs
                        }
                    }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi nạp chi tiết phiên hội thoại: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Assistant/NewConversation
        [HttpPost]
        public async Task<JsonResult> NewConversation(string title = null, long? taskId = null)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }

            try
            {
                using (var db = new TDMUEcoSystemEntities())
                {
                    var conv = await _convService.CreateConversationAsync(db, currentUserId, title, taskId);
                    return Json(new
                    {
                        success = true,
                        message = "Đã khởi tạo phiên hội thoại mới.",
                        conversationId = conv.ConversationID,
                        title = conv.Title
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tạo phiên mới: " + ex.Message });
            }
        }

        // POST: Assistant/DeleteConversation
        [HttpPost]
        public async Task<JsonResult> DeleteConversation(long conversationId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }

            try
            {
                using (var db = new TDMUEcoSystemEntities())
                {
                    bool deleted = await _convService.DeleteConversationAsync(db, conversationId, currentUserId);
                    if (!deleted)
                    {
                        return Json(new { success = false, message = "Không tìm thấy phiên hoặc bạn không có quyền xóa." });
                    }

                    return Json(new { success = true, message = "Đã xóa phiên hội thoại thành công." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa phiên hội thoại: " + ex.Message });
            }
        }

        // POST: Assistant/UpdateConversationTitle
        [HttpPost]
        public async Task<JsonResult> UpdateConversationTitle(long conversationId, string title)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return Json(new { success = false, message = "Tiêu đề không được để trống." });
            }

            string cleanTitle = title.Trim();
            if (cleanTitle.Length > 200)
            {
                cleanTitle = cleanTitle.Substring(0, 200);
            }

            try
            {
                using (var db = new TDMUEcoSystemEntities())
                {
                    bool ok = await _convService.UpdateConversationMetadataAsync(db, conversationId, currentUserId, title: cleanTitle);
                    if (!ok)
                    {
                        return Json(new { success = false, message = "Không thể cập nhật tiêu đề phiên (phiên không tồn tại hoặc không thuộc quyền sở hữu)." });
                    }

                    return Json(new { success = true, message = "Đã cập nhật tiêu đề phiên hội thoại thành công.", title = cleanTitle });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật tiêu đề: " + ex.Message });
            }
        }

        // GET: Assistant/GetPermissions
        [HttpGet]
        public async Task<JsonResult> GetPermissions()
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new TDMUEcoSystemEntities())
            {
                var perm = await _permService.GetOrCreatePermissionsAsync(db, currentUserId);
                return Json(new
                {
                    success = true,
                    data = new
                    {
                        perm.CanReadData,
                        perm.CanManageContent,
                        perm.CanInteract,
                        perm.CanAutomation,
                        perm.IsEnabled,
                        UpdatedAt = perm.UpdatedAt.ToString("dd/MM/yyyy HH:mm")
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Assistant/SavePermissions
        [HttpPost]
        public async Task<JsonResult> SavePermissions(bool canRead, bool canManage, bool canInteract, bool canAutomation, bool isEnabled)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }

            using (var db = new TDMUEcoSystemEntities())
            {
                var perm = await _permService.UpdatePermissionsAsync(db, currentUserId, canRead, canManage, canInteract, canAutomation, isEnabled);

                // Tự động tạm dừng các tác vụ tự động đang hoạt động nếu quyền bị thu hồi
                if (!isEnabled || !canAutomation)
                {
                    await AgentGuardService.OnUserPermissionChangedAsync(db, currentUserId, isEnabled, canAutomation);
                }

                return Json(new
                {
                    success = true,
                    message = "Đã lưu cài đặt quyền hạn Agent thành công!",
                    data = new
                    {
                        perm.CanReadData,
                        perm.CanManageContent,
                        perm.CanInteract,
                        perm.CanAutomation,
                        perm.IsEnabled
                    }
                });
            }
        }

        // GET: Assistant/GetTasks
        [HttpGet]
        public async Task<JsonResult> GetTasks()
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new TDMUEcoSystemEntities())
            {
                var rawTasks = await db.AgentTasks
                    .AsNoTracking()
                    .Where(t => t.UserID == currentUserId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(30)
                    .ToListAsync();

                var tasks = rawTasks.Select(t => new
                {
                    t.TaskID,
                    t.TaskName,
                    t.UserPrompt,
                    t.ScheduleType,
                    t.ScheduleConfig,
                    t.Status,
                    NextRunAt = t.NextRunAt.HasValue ? t.NextRunAt.Value.ToString("dd/MM/yyyy HH:mm") : null,
                    LastRunAt = t.LastRunAt.HasValue ? t.LastRunAt.Value.ToString("dd/MM/yyyy HH:mm") : null,
                    CreatedAt = t.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                }).ToList();

                return Json(new { success = true, data = tasks }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Assistant/PauseTask
        [HttpPost]
        public async Task<JsonResult> PauseTask(long taskId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            using (var db = new TDMUEcoSystemEntities())
            {
                var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.TaskID == taskId && t.UserID == currentUserId);
                if (task == null) return Json(new { success = false, message = "Không tìm thấy tác vụ." });

                task.Status = "Paused";
                task.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Đã tạm dừng tác vụ." });
            }
        }

        // POST: Assistant/ResumeTask
        [HttpPost]
        public async Task<JsonResult> ResumeTask(long taskId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            using (var db = new TDMUEcoSystemEntities())
            {
                var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.TaskID == taskId && t.UserID == currentUserId);
                if (task == null) return Json(new { success = false, message = "Không tìm thấy tác vụ." });

                task.Status = "Active";
                if (!task.NextRunAt.HasValue || task.NextRunAt.Value < DateTime.UtcNow)
                {
                    task.NextRunAt = DateTime.UtcNow.AddMinutes(1);
                }
                task.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Đã kích hoạt lại tác vụ. Lần chạy kế tiếp: " + task.NextRunAt?.ToString("dd/MM/yyyy HH:mm") });
            }
        }

        // POST: Assistant/CancelTask
        [HttpPost]
        public async Task<JsonResult> CancelTask(long taskId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            using (var db = new TDMUEcoSystemEntities())
            {
                var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.TaskID == taskId && t.UserID == currentUserId);
                if (task == null) return Json(new { success = false, message = "Không tìm thấy tác vụ." });

                task.Status = "Cancelled";
                task.NextRunAt = null;
                task.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Đã hủy tác vụ." });
            }
        }

        // POST: Assistant/RunNow
        [HttpPost]
        public async Task<JsonResult> RunNow(long taskId, bool isDryRun = false)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            var scheduler = new AgentSchedulerService();
            var res = await scheduler.ExecuteSingleTaskAsync(taskId, currentUserId, isDryRun);

            return Json(new
            {
                success = res.success,
                message = res.message,
                runId = res.run?.RunID,
                status = res.run?.Status,
                isDryRun = isDryRun,
                summary = res.run?.ResultSummary
            });
        }

        // POST: Assistant/UpdateTask
        [HttpPost]
        public async Task<JsonResult> UpdateTask(long taskId, string taskName, string userPrompt, string scheduleType, string scheduleConfig, string specificRunTime = null)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            if (string.IsNullOrWhiteSpace(taskName) || string.IsNullOrWhiteSpace(userPrompt))
            {
                return Json(new { success = false, message = "Tên tác vụ và yêu cầu không được để trống." });
            }

            using (var db = new TDMUEcoSystemEntities())
            {
                var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.TaskID == taskId && t.UserID == currentUserId);
                if (task == null) return Json(new { success = false, message = "Không tìm thấy tác vụ." });

                task.TaskName = taskName.Trim();
                task.UserPrompt = userPrompt.Trim();
                task.ScheduleType = (scheduleType ?? "once").Trim();
                task.ScheduleConfig = (scheduleConfig ?? "").Trim();
                task.UpdatedAt = DateTime.UtcNow;

                if (task.Status == "Active")
                {
                    if (!string.IsNullOrWhiteSpace(specificRunTime) && DateTime.TryParse(specificRunTime, out DateTime parsedDt))
                    {
                        if (parsedDt > DateTime.Now)
                        {
                            task.NextRunAt = parsedDt.ToUniversalTime();
                        }
                        else
                        {
                            task.NextRunAt = DateTime.UtcNow.AddMinutes(1);
                        }
                    }
                    else
                    {
                        string stLower = task.ScheduleType.ToLowerInvariant();
                        if (stLower.Contains("hour") || stLower == "hourly") task.NextRunAt = DateTime.UtcNow.AddHours(1);
                        else if (stLower.Contains("day") || stLower == "daily") task.NextRunAt = DateTime.UtcNow.AddDays(1);
                        else if (stLower.Contains("week") || stLower == "weekly") task.NextRunAt = DateTime.UtcNow.AddDays(7);
                        else if (stLower.Contains("interval") || stLower.Contains("minute"))
                        {
                            int mins = 30;
                            int.TryParse(task.ScheduleConfig, out mins);
                            if (mins < 1) mins = 5;
                            task.NextRunAt = DateTime.UtcNow.AddMinutes(mins);
                        }
                        else
                        {
                            task.NextRunAt = DateTime.UtcNow.AddMinutes(1);
                        }
                    }
                }

                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Đã cập nhật tác vụ thành công!" });
            }
        }

        // GET: Assistant/GetRunDetails
        [HttpGet]
        public async Task<JsonResult> GetRunDetails(long runId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);

            using (var db = new TDMUEcoSystemEntities())
            {
                var run = await db.AgentTaskRuns
                    .AsNoTracking()
                    .Include(r => r.AgentActionLogs)
                    .FirstOrDefaultAsync(r => r.RunID == runId && r.UserID == currentUserId);

                if (run == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin lượt chạy." }, JsonRequestBehavior.AllowGet);
                }

                var logs = run.AgentActionLogs.OrderBy(l => l.CreatedAt).Select(l => new
                {
                    l.LogID,
                    l.ActionName,
                    l.TargetType,
                    l.TargetID,
                    l.Status,
                    l.InputSummary,
                    l.ResultSummary,
                    l.ErrorMessage,
                    CreatedAt = l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        run.RunID,
                        run.TaskID,
                        run.Status,
                        StartedAt = run.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                        FinishedAt = run.FinishedAt.HasValue ? run.FinishedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : null,
                        run.ResultSummary,
                        run.ErrorMessage,
                        Logs = logs
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Assistant/GetTaskRuns
        [HttpGet]
        public async Task<JsonResult> GetTaskRuns(long taskId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);

            using (var db = new TDMUEcoSystemEntities())
            {
                var rawRuns = await db.AgentTaskRuns
                    .AsNoTracking()
                    .Where(r => r.TaskID == taskId && r.UserID == currentUserId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(10)
                    .ToListAsync();

                var runs = rawRuns.Select(r => new
                {
                    r.RunID,
                    r.TaskID,
                    r.Status,
                    StartedAt = r.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                    FinishedAt = r.FinishedAt.HasValue ? r.FinishedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : null,
                    r.ResultSummary,
                    r.ErrorMessage
                }).ToList();

                return Json(new { success = true, data = runs }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Assistant/GetWorkerStatus
        [HttpGet]
        public JsonResult GetWorkerStatus()
        {
            var heartbeat = AgentSchedulerService.GetLatestHeartbeat();
            bool isOnline = heartbeat != null && heartbeat.IsAlive;

            return Json(new
            {
                success = true,
                isOnline = isOnline,
                workerType = heartbeat?.WorkerType ?? "Chưa chạy",
                machineName = heartbeat?.MachineName ?? "—",
                lastHeartbeat = heartbeat?.LastHeartbeatLocal ?? "Chưa ghi nhận",
                message = isOnline
                    ? (heartbeat.WorkerType == "StandaloneWorker"
                        ? "Worker độc lập đang chạy nền (An toàn tuyệt đối khi tắt trình duyệt / IIS recycle)"
                        : "Web Background Timer đang chạy")
                    : "Chưa phát hiện Worker nền đang chạy. Bạn có thể khởi động StudentConnect.Worker.exe."
            }, JsonRequestBehavior.AllowGet);
        }

        // POST: Assistant/DeleteTask
        [HttpPost]
        public async Task<JsonResult> DeleteTask(long taskId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            }

            using (var db = new TDMUEcoSystemEntities())
            {
                var task = await db.AgentTasks.FirstOrDefaultAsync(t => t.TaskID == taskId && t.UserID == currentUserId);
                if (task == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tác vụ." });
                }

                // Xóa cascade runs và pending confirmations
                var runs = db.AgentTaskRuns.Where(r => r.TaskID == taskId).ToList();
                foreach (var r in runs)
                {
                    var logs = db.AgentActionLogs.Where(l => l.RunID == r.RunID).ToList();
                    db.AgentActionLogs.RemoveRange(logs);
                }
                db.AgentTaskRuns.RemoveRange(runs);

                var confs = db.AgentPendingConfirmations.Where(c => c.TaskID == taskId).ToList();
                db.AgentPendingConfirmations.RemoveRange(confs);

                db.AgentTasks.Remove(task);
                await db.SaveChangesAsync();

                return Json(new { success = true, message = "Đã xóa tác vụ thành công." });
            }
        }

        // GET: Assistant/GetLogs
        [HttpGet]
        public async Task<JsonResult> GetLogs(string keyword = null, string status = null)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);
            }

            using (var db = new TDMUEcoSystemEntities())
            {
                var logs = await _auditService.GetRecentLogsAsync(db, currentUserId, 50, keyword, status);
                var formatted = logs.Select(l => new
                {
                    l.LogID,
                    l.ActionName,
                    l.TargetType,
                    l.TargetID,
                    l.Status,
                    l.InputSummary,
                    l.ResultSummary,
                    l.ErrorMessage,
                    CreatedAt = l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")
                });

                return Json(new { success = true, data = formatted }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Assistant/GetLogDetail
        [HttpGet]
        public async Task<JsonResult> GetLogDetail(long logId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);

            using (var db = new TDMUEcoSystemEntities())
            {
                var log = await _auditService.GetLogByIdAsync(db, logId, currentUserId);
                if (log == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bản ghi nhật ký." }, JsonRequestBehavior.AllowGet);
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        log.LogID,
                        log.ActionName,
                        log.TargetType,
                        log.TargetID,
                        log.Status,
                        log.InputSummary,
                        log.ResultSummary,
                        log.ErrorMessage,
                        CreatedAt = log.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")
                    }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Assistant/UpdateLog
        [HttpPost]
        public async Task<JsonResult> UpdateLog(long logId, string resultSummary)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            using (var db = new TDMUEcoSystemEntities())
            {
                bool updated = await _auditService.UpdateLogNoteAsync(db, logId, currentUserId, resultSummary);
                if (!updated)
                {
                    return Json(new { success = false, message = "Không tìm thấy bản ghi nhật ký hoặc bạn không có quyền sửa đổi." });
                }

                return Json(new { success = true, message = "Đã cập nhật ghi chú / đính chính nhật ký thành công." });
            }
        }

        // POST: Assistant/DeleteLog
        [HttpPost]
        public async Task<JsonResult> DeleteLog(long logId)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0) return Json(new { success = false, message = "Vui lòng đăng nhập." });

            using (var db = new TDMUEcoSystemEntities())
            {
                bool deleted = await _auditService.DeleteLogAsync(db, logId, currentUserId);
                if (!deleted)
                {
                    return Json(new { success = false, message = "Không tìm thấy bản ghi nhật ký hoặc bạn không có quyền xóa." });
                }

                return Json(new { success = true, message = "Đã xóa bản ghi nhật ký thành công." });
            }
        }

        // POST: Assistant/ConfirmAction
        [HttpPost]
        public async Task<JsonResult> ConfirmAction(long confirmationId, bool approve)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId <= 0)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để xác nhận hành động." });
            }

            using (var db = new TDMUEcoSystemEntities())
            {
                var resolveResult = await _confirmService.TryResolveConfirmationAsync(db, confirmationId, currentUserId, approve);
                if (!resolveResult.success)
                {
                    return Json(new { success = false, message = resolveResult.message });
                }

                var conf = resolveResult.conf;

                if (!approve)
                {
                    await _auditService.LogActionAsync(db, currentUserId, conf.ActionName, "Confirmation", confirmationId, "Cancelled", conf.ActionPayload, "Người dùng đã từ chối thực hiện.", null);
                    return Json(new { success = true, approved = false, message = "Đã từ chối thực hiện hành động." });
                }

                // Tái kiểm tra quyền tại thời điểm duyệt (Permission Revocation check)
                var perm = await _permService.GetOrCreatePermissionsAsync(db, currentUserId);
                if (perm == null || !perm.IsEnabled || !perm.CanManageContent)
                {
                    await _auditService.LogActionAsync(db, currentUserId, conf.ActionName, "Confirmation", confirmationId, "Blocked", conf.ActionPayload, "Quyền Quản lý nội dung đã bị thu hồi trước khi xác nhận.", "PermissionRevoked");
                    return Json(new { success = false, approved = false, message = "Quyền quản lý nội dung của bạn đã bị thu hồi. Không thể thực hiện hành động." });
                }

                // Thực hiện hành động nếu người dùng duyệt
                string actionResult = "Đã thực hiện thành công.";
                try
                {
                    if (conf.ActionName == "delete_post")
                    {
                        var payload = JObject.Parse(conf.ActionPayload ?? "{}");
                        int postId = (int)(payload["postId"] ?? 0);
                        string itemType = (string)payload["itemType"] ?? "Post";
                        if (postId > 0)
                        {
                            var delRes = await _tools.DeletePostAsync(db, postId, itemType, currentUserId);
                            if (delRes["error"] != null)
                            {
                                throw new Exception((string)delRes["error"]);
                            }
                            actionResult = delRes["message"]?.ToString() ?? ("Đã xóa " + itemType + " #" + postId);
                        }
                    }

                    await _auditService.LogActionAsync(db, currentUserId, conf.ActionName, "Confirmation", confirmationId, "Succeeded", conf.ActionPayload, actionResult, null);
                    return Json(new { success = true, approved = true, message = actionResult });
                }
                catch (Exception ex)
                {
                    await _auditService.LogActionAsync(db, currentUserId, conf.ActionName, "Confirmation", confirmationId, "Failed", conf.ActionPayload, null, ex.Message);
                    return Json(new { success = false, approved = true, message = "Lỗi khi thực hiện: " + ex.Message });
                }
            }
        }
    }
}
