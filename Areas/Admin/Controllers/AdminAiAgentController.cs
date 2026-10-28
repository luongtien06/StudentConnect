using StudentConnect.Helpers;
using Newtonsoft.Json;
using StudentConnect.Models;
using StudentConnect.Models.Assistant;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;

namespace StudentConnect.Areas.Admin.Controllers
{
    public class AdminAiAgentController : AdminBaseController
    {
        private readonly AgentSchedulerService _scheduler = new AgentSchedulerService();
        private readonly AiApiClient _aiClient = new AiApiClient();
        private readonly AgentPermissionService _permService = new AgentPermissionService();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (filterContext.Result != null) return;

            string currentRole = Session["AdminRole"] as string ?? "";
            bool isSuperAdmin = string.Equals(currentRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (isSuperAdmin)
            {
                ViewBag.IsSuperAdmin = true;
                ViewBag.CanAutomation = true;
                ViewBag.CanManageContent = true;
                ViewBag.CanInteract = true;
                ViewBag.CanReadData = true;
                ViewBag.CanDeleteLogs = true;
                return;
            }

            // Với 4 Admin con: Kiểm tra quyền được cấp qua bảng AgentPermissions
            string email = Session["AdminEmail"] as string ?? "";
            var adminUser = db.Users.FirstOrDefault(u => u.Email == email && u.IsAdmin);
            if (adminUser == null)
            {
                DenyAccess(filterContext, "Tài khoản quản trị viên không hợp lệ hoặc đã bị khóa.");
                return;
            }

            var perm = db.AgentPermissions.FirstOrDefault(p => p.UserID == adminUser.UserID);
            if (perm == null && adminUser.AdminRole == "AdminAiAgent")
            {
                perm = new AgentPermission
                {
                    UserID = adminUser.UserID,
                    IsEnabled = true,
                    CanAutomation = true,
                    CanManageContent = true,
                    CanInteract = true,
                    CanReadData = true,
                    UpdatedAt = DateTime.Now,
                    CreatedAt = DateTime.Now
                };
                db.AgentPermissions.Add(perm);
                db.SaveChanges();
            }

            if (perm == null || !perm.IsEnabled)
            {
                DenyAccess(filterContext, "Bạn chưa được cấp quyền truy cập vào mô-đun Quản trị AI Agent. Vui lòng liên hệ Admin tối cao để được cấp quyền.");
                return;
            }

            ViewBag.IsSuperAdmin = false;
            ViewBag.CanAutomation = perm.CanAutomation;
            ViewBag.CanManageContent = perm.CanManageContent;
            ViewBag.CanInteract = perm.CanInteract;
            ViewBag.CanReadData = perm.CanReadData;
            ViewBag.CanDeleteLogs = string.Equals(currentRole, "AdminAiAgent", StringComparison.OrdinalIgnoreCase) || 
                                    (perm.CanManageContent && perm.CanAutomation);
        }

        private void DenyAccess(ActionExecutingContext filterContext, string message)
        {
            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.Result = new JsonResult
                {
                    Data = new { success = false, message = message },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                TempData["ErrorMessage"] = message;
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new { controller = "AdminHome", action = "Index", area = "Admin" }));
            }
        }

        private bool CheckPermission(string actionType, out string errorMsg)
        {
            string currentRole = Session["AdminRole"] as string ?? "";
            if (string.Equals(currentRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                errorMsg = null;
                return true;
            }

            string email = Session["AdminEmail"] as string ?? "";
            var adminUser = db.Users.FirstOrDefault(u => u.Email == email && u.IsAdmin);
            if (adminUser == null)
            {
                errorMsg = "Không tìm thấy thông tin quản trị viên.";
                return false;
            }

            var perm = db.AgentPermissions.FirstOrDefault(p => p.UserID == adminUser.UserID);
            if (perm == null || !perm.IsEnabled)
            {
                errorMsg = "Tài khoản của bạn đã bị thu hồi quyền truy cập AI Agent.";
                return false;
            }

            switch (actionType)
            {
                case "Automation":
                    if (!perm.CanAutomation) { errorMsg = "Bạn không có quyền quản lý và thực thi Tác vụ tự động."; return false; }
                    break;
                case "ManageContent":
                    if (!perm.CanManageContent) { errorMsg = "Bạn không có quyền chỉnh sửa Cấu hình Agent."; return false; }
                    break;
                case "Interact":
                    if (!perm.CanInteract) { errorMsg = "Bạn không có quyền thao tác cấu hình API & Công cụ."; return false; }
                    break;
                case "ReadData":
                    if (!perm.CanReadData) { errorMsg = "Bạn không có quyền xem dữ liệu và nhật ký."; return false; }
                    break;
                case "DeleteLogs":
                    if (!string.Equals(currentRole, "AdminAiAgent", StringComparison.OrdinalIgnoreCase) && 
                        (!perm.CanManageContent || !perm.CanAutomation))
                    {
                        errorMsg = "Bạn không có quyền dọn dẹp hoặc xóa nhật ký kiểm toán AI Agent.";
                        return false;
                    }
                    break;
                case "SuperAdminOnly":
                    errorMsg = "Chức năng này chỉ dành riêng cho Admin tối cao (SuperAdmin).";
                    return false;
            }

            errorMsg = null;
            return true;
        }

        private string MaskApiKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "Chưa cấu hình";
            string trimmed = key.Trim();
            if (trimmed.Length <= 8) return "••••••••";
            return trimmed.Substring(0, 7) + "••••••••••••••••" + trimmed.Substring(trimmed.Length - 4);
        }

        // ══════════════════════════════════════════════════════
        // GET: Admin/AdminAiAgent/Index
        // ══════════════════════════════════════════════════════
        public ActionResult Index()
        {
            var vm = new AdminAiAgentViewModel
            {
                IsSuperAdmin = ViewBag.IsSuperAdmin ?? false,
                CanAutomation = ViewBag.CanAutomation ?? false,
                CanManageContent = ViewBag.CanManageContent ?? false,
                CanInteract = ViewBag.CanInteract ?? false,
                CanReadData = ViewBag.CanReadData ?? false,
                CanDeleteLogs = ViewBag.CanDeleteLogs ?? false
            };

            // 0. Tổng quan (Overview stats)
            var stats = new OverviewStats();
            stats.TotalTasks = db.AgentTasks.Count();
            stats.ActiveTasks = db.AgentTasks.Count(t => t.Status == "Active");
            stats.PausedTasks = db.AgentTasks.Count(t => t.Status == "Paused");
            stats.CompletedTasks = db.AgentTasks.Count(t => t.Status == "Completed");
            stats.CancelledTasks = db.AgentTasks.Count(t => t.Status == "Cancelled");

            stats.TotalRuns = db.AgentTaskRuns.Count();
            stats.SucceededRuns = db.AgentTaskRuns.Count(r => r.Status == "Succeeded");
            stats.FailedRuns = db.AgentTaskRuns.Count(r => r.Status == "Failed");
            stats.SuccessRate = stats.TotalRuns > 0 ? Math.Round((double)stats.SucceededRuns / stats.TotalRuns * 100, 1) : 100.0;

            stats.TotalLogs = db.AgentActionLogs.Count();
            var todayUtc = DateTime.UtcNow.Date;
            stats.TodayLogs = db.AgentActionLogs.Count(l => l.CreatedAt >= todayUtc);

            stats.IsSchedulerRunning = AgentSchedulerService.LastTickAt.HasValue &&
                                       (DateTime.UtcNow - AgentSchedulerService.LastTickAt.Value).TotalMinutes < 5;
            stats.LastSchedulerTick = AgentSchedulerService.LastTickAt;
            stats.WorkerHeartbeat = AgentSchedulerService.GetLatestHeartbeat();

            stats.RecentFailedRuns = db.AgentTaskRuns
                .Include(r => r.AgentTask)
                .Include(r => r.User)
                .Where(r => r.Status == "Failed")
                .OrderByDescending(r => r.StartedAt)
                .Take(6)
                .ToList();

            stats.RecentErrorLogs = db.AgentActionLogs
                .Include(l => l.User)
                .Where(l => l.Status == "Failed")
                .OrderByDescending(l => l.CreatedAt)
                .Take(6)
                .ToList();

            vm.Stats = stats;

            // 1. Quản lý tác vụ & lịch chạy
            vm.Tasks = db.AgentTasks
                .Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt)
                .Take(150)
                .ToList();

            // 2. Quản lý nhật ký
            vm.Logs = db.AgentActionLogs
                .Include(l => l.User)
                .OrderByDescending(l => l.CreatedAt)
                .Take(150)
                .ToList();

            // 3. Cấu hình Agent
            vm.Config = AgentConfigService.GetConfig();

            // 4. Quản lý API & Quyền hạn
            string rawKey = _aiClient.ApiKey;
            vm.HasApiKey = !string.IsNullOrWhiteSpace(rawKey);
            vm.MaskedApiKey = MaskApiKey(rawKey);

            // Tải danh sách 4 Admin con để phân quyền (chỉ dành cho SuperAdmin xem/quản lý)
            var subAdminUsers = db.Users
                .Where(u => u.IsAdmin && u.AdminRole != "SuperAdmin")
                .OrderBy(u => u.AdminRole)
                .ThenBy(u => u.Username)
                .ToList();

            var adminIds = subAdminUsers.Select(u => u.UserID).ToList();
            var permissionsMap = db.AgentPermissions
                .Where(p => adminIds.Contains(p.UserID))
                .ToDictionary(p => p.UserID, p => p);

            vm.SubAdmins = subAdminUsers.Select(u =>
            {
                permissionsMap.TryGetValue(u.UserID, out var perm);
                return new SubAdminPermissionItem
                {
                    UserID = u.UserID,
                    Username = u.Username,
                    Email = u.Email,
                    AdminRole = u.AdminRole,
                    Avatar = u.Avatar,
                    IsEnabled = perm != null && perm.IsEnabled,
                    CanAutomation = perm != null && perm.CanAutomation,
                    CanManageContent = perm != null && perm.CanManageContent,
                    CanInteract = perm != null && perm.CanInteract,
                    CanReadData = perm != null && perm.CanReadData,
                    UpdatedAt = perm?.UpdatedAt
                };
            }).ToList();

            return View(vm);
        }

        // ══════════════════════════════════════════════════════
        // TAB 1: QUẢN LÝ TÁC VỤ & LỊCH CHẠY
        // ══════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> CreateTask(string taskName, string userPrompt, string taskType, string scheduleType, string scheduleConfig, DateTime? nextRunAt)
        {
            if (!CheckPermission("Automation", out string err))
                return Json(new { success = false, message = err });

            if (string.IsNullOrWhiteSpace(taskName) || string.IsNullOrWhiteSpace(userPrompt))
                return Json(new { success = false, message = "Vui lòng nhập tên tác vụ và nội dung chỉ thị cho Agent." });

            string adminEmail = Session["AdminEmail"] as string ?? "";
            var adminUser = db.Users.FirstOrDefault(u => u.Email == adminEmail);
            int creatorId = adminUser?.UserID ?? 1002;

            DateTime scheduledNext = nextRunAt ?? DateTime.UtcNow;
            string cleanScheduleType = string.IsNullOrWhiteSpace(scheduleType) ? "Once" : scheduleType.Trim();

            var task = new AgentTask
            {
                UserID = creatorId,
                TaskName = taskName.Trim(),
                UserPrompt = userPrompt.Trim(),
                TaskType = string.IsNullOrWhiteSpace(taskType) ? "Automation" : taskType.Trim(),
                ScheduleType = cleanScheduleType,
                ScheduleConfig = scheduleConfig?.Trim() ?? "{}",
                TimeZone = "SE Asia Standard Time",
                Status = "Active",
                NextRunAt = scheduledNext,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.AgentTasks.Add(task);
            await db.SaveChangesAsync();

            // Ghi log kiểm toán
            LogAdminAction("create_task", "AgentTask", task.TaskID, "Success",
                JsonConvert.SerializeObject(new { task.TaskName, task.ScheduleType, task.NextRunAt }),
                "Tạo tác vụ tự động mới thành công.");

            return Json(new { success = true, message = "Đã tạo tác vụ tự động thành công!", taskId = task.TaskID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> EditTask(long taskId, string taskName, string userPrompt, string scheduleType, string scheduleConfig, string status, DateTime? nextRunAt)
        {
            if (!CheckPermission("Automation", out string err))
                return Json(new { success = false, message = err });

            var task = await db.AgentTasks.FindAsync(taskId);
            if (task == null)
                return Json(new { success = false, message = "Không tìm thấy tác vụ." });

            task.TaskName = taskName?.Trim() ?? task.TaskName;
            task.UserPrompt = userPrompt?.Trim() ?? task.UserPrompt;
            task.ScheduleType = scheduleType?.Trim() ?? task.ScheduleType;
            task.ScheduleConfig = scheduleConfig?.Trim() ?? task.ScheduleConfig;
            task.Status = status?.Trim() ?? task.Status;
            if (nextRunAt.HasValue) task.NextRunAt = nextRunAt.Value;
            task.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            LogAdminAction("edit_task", "AgentTask", task.TaskID, "Success",
                JsonConvert.SerializeObject(new { task.TaskName, task.Status, task.NextRunAt }),
                "Cập nhật thông tin tác vụ thành công.");

            return Json(new { success = true, message = "Cập nhật tác vụ thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> RunTaskNow(long taskId, bool isDryRun = false)
        {
            if (!CheckPermission("Automation", out string err))
                return Json(new { success = false, message = err });

            string adminEmail = Session["AdminEmail"] as string ?? "";
            var adminUser = db.Users.FirstOrDefault(u => u.Email == adminEmail);
            int adminId = adminUser?.UserID ?? 1002;

            var result = await _scheduler.ExecuteSingleTaskByAdminAsync(taskId, adminId, isDryRun);

            LogAdminAction("run_task_now", "AgentTask", taskId, result.success ? "Success" : "Failed",
                JsonConvert.SerializeObject(new { taskId, isDryRun }),
                result.message);

            return Json(new
            {
                success = result.success,
                message = result.message,
                runId = result.run?.RunID,
                status = result.run?.Status,
                resultSummary = result.run?.ResultSummary,
                errorMessage = result.run?.ErrorMessage
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> PauseTask(long taskId)
        {
            if (!CheckPermission("Automation", out string err))
                return Json(new { success = false, message = err });

            var task = await db.AgentTasks.FindAsync(taskId);
            if (task == null) return Json(new { success = false, message = "Không tìm thấy tác vụ." });

            task.Status = "Paused";
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            LogAdminAction("pause_task", "AgentTask", taskId, "Success", null, "Tạm dừng tác vụ.");
            return Json(new { success = true, message = "Đã tạm dừng tác vụ!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> ResumeTask(long taskId)
        {
            if (!CheckPermission("Automation", out string err))
                return Json(new { success = false, message = err });

            var task = await db.AgentTasks.FindAsync(taskId);
            if (task == null) return Json(new { success = false, message = "Không tìm thấy tác vụ." });

            task.Status = "Active";
            if (!task.NextRunAt.HasValue || task.NextRunAt < DateTime.UtcNow)
            {
                task.NextRunAt = DateTime.UtcNow.AddMinutes(1);
            }
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            LogAdminAction("resume_task", "AgentTask", taskId, "Success", null, "Tiếp tục kích hoạt tác vụ.");
            return Json(new { success = true, message = "Đã tiếp tục kích hoạt tác vụ!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> CancelTask(long taskId)
        {
            if (!CheckPermission("Automation", out string err))
                return Json(new { success = false, message = err });

            var task = await db.AgentTasks.FindAsync(taskId);
            if (task == null) return Json(new { success = false, message = "Không tìm thấy tác vụ." });

            task.Status = "Cancelled";
            task.NextRunAt = null;
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            LogAdminAction("cancel_task", "AgentTask", taskId, "Success", null, "Hủy tác vụ.");
            return Json(new { success = true, message = "Đã hủy tác vụ!" });
        }

        [HttpGet]
        public async Task<JsonResult> GetTaskRuns(long taskId)
        {
            if (!CheckPermission("ReadData", out string err))
                return Json(new { success = false, message = err }, JsonRequestBehavior.AllowGet);

            var rawRuns = await db.AgentTaskRuns
                .Where(r => r.TaskID == taskId)
                .OrderByDescending(r => r.StartedAt)
                .Take(50)
                .ToListAsync();

            var runs = rawRuns.Select(r => new
            {
                r.RunID,
                r.TaskID,
                r.Status,
                StartedAt = r.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                FinishedAt = r.FinishedAt.HasValue ? r.FinishedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—",
                r.AttemptCount,
                r.ResultSummary,
                r.ErrorMessage
            }).ToList();

            return Json(new { success = true, runs }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> TriggerSchedulerTick()
        {
            if (!CheckPermission("Automation", out string err))
                return Json(new { success = false, message = err });

            int processed = await _scheduler.ProcessDueTasksAsync(false, msg => System.Diagnostics.Debug.WriteLine(msg));

            LogAdminAction("trigger_scheduler", "System", null, "Success", null,
                $"Kích hoạt kiểm tra lịch ngầm thủ công, xử lý {processed} tác vụ.");

            return Json(new { success = true, message = $"Đã quét và kích hoạt lịch trình thành công! Đã xử lý {processed} tác vụ đến hạn.", processedCount = processed });
        }

        // ══════════════════════════════════════════════════════
        // TAB 2: QUẢN LÝ NHẬT KÝ (LOGS)
        // ══════════════════════════════════════════════════════

        [HttpGet]
        public async Task<JsonResult> FilterLogs(string keyword, string status, string actionName, string dateRange)
        {
            if (!CheckPermission("ReadData", out string err))
                return Json(new { success = false, message = err }, JsonRequestBehavior.AllowGet);

            var query = db.AgentActionLogs.Include(l => l.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(l => l.ActionName.ToLower().Contains(keyword) ||
                                         (l.InputSummary != null && l.InputSummary.ToLower().Contains(keyword)) ||
                                         (l.ResultSummary != null && l.ResultSummary.ToLower().Contains(keyword)) ||
                                         (l.ErrorMessage != null && l.ErrorMessage.ToLower().Contains(keyword)) ||
                                         (l.User != null && (l.User.Username.ToLower().Contains(keyword) || l.User.Email.ToLower().Contains(keyword))));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                if (string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(l => l.Status == "Succeeded" || l.Status == "Success");
                }
                else
                {
                    query = query.Where(l => l.Status == status);
                }
            }

            if (!string.IsNullOrWhiteSpace(actionName) && actionName != "All")
            {
                query = query.Where(l => l.ActionName == actionName);
            }

            if (!string.IsNullOrWhiteSpace(dateRange))
            {
                if (dateRange == "today")
                {
                    DateTime todayStartUtc = DateTimeHelper.VietnamToUtc(DateTimeHelper.VietnamNow.Date);
                    query = query.Where(l => l.CreatedAt >= todayStartUtc);
                }
                else if (dateRange == "7days")
                {
                    DateTime sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                    query = query.Where(l => l.CreatedAt >= sevenDaysAgo);
                }
                else if (dateRange == "30days")
                {
                    DateTime thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                    query = query.Where(l => l.CreatedAt >= thirtyDaysAgo);
                }
            }

            var rawLogs = await query.OrderByDescending(l => l.CreatedAt).Take(200).ToListAsync();

            var logs = rawLogs.Select(l => new
            {
                l.LogID,
                UserName = l.User != null ? (l.User.DisplayName ?? l.User.Username) : ("User #" + l.UserID),
                l.ActionName,
                l.TargetType,
                l.TargetID,
                l.Status,
                InputSnippet = l.InputSummary != null && l.InputSummary.Length > 80 ? l.InputSummary.Substring(0, 80) + "..." : l.InputSummary,
                ResultSnippet = l.ResultSummary != null && l.ResultSummary.Length > 80 ? l.ResultSummary.Substring(0, 80) + "..." : l.ResultSummary,
                l.ErrorMessage,
                CreatedAtFormatted = l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")
            }).ToList();

            return Json(new { success = true, logs }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public async Task<JsonResult> GetLogDetail(long logId)
        {
            if (!CheckPermission("ReadData", out string err))
                return Json(new { success = false, message = err }, JsonRequestBehavior.AllowGet);

            var log = await db.AgentActionLogs.Include(l => l.User).FirstOrDefaultAsync(l => l.LogID == logId);
            if (log == null)
                return Json(new { success = false, message = "Không tìm thấy bản ghi nhật ký." }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                log = new
                {
                    log.LogID,
                    log.UserID,
                    UserName = log.User != null ? (log.User.DisplayName ?? log.User.Username) : ("User #" + log.UserID),
                    UserEmail = log.User?.Email ?? "—",
                    log.RunID,
                    log.ConversationID,
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

        /// <summary>
        /// Xem trước số lượng bản ghi thỏa mãn điều kiện dọn dẹp nhật ký (Read-only Preview)
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> CountLogsToDelete(string scope = "today", int days = 1, string status = "All")
        {
            if (!CheckPermission("ReadData", out string err))
                return Json(new { success = false, message = err }, JsonRequestBehavior.AllowGet);

            try
            {
                var (count, desc) = await BuildLogsDeleteQueryCriteria(scope, days, status);
                return Json(new { success = true, count = count, criteriaDesc = desc }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private async Task<(int count, string desc)> BuildLogsDeleteQueryCriteria(string scope, int days, string status)
        {
            IQueryable<AgentActionLog> query = db.AgentActionLogs;

            // 1. Ánh xạ trạng thái linh hoạt
            string statusDesc = "tất cả trạng thái";
            if (string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(l => l.Status == "Succeeded" || l.Status == "Success");
                statusDesc = "trạng thái Thành công (Succeeded)";
            }
            else if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(l => l.Status == "Failed");
                statusDesc = "trạng thái Thất bại (Failed)";
            }
            else if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(l => l.Status == "Pending");
                statusDesc = "trạng thái Chờ xác nhận (Pending)";
            }
            else if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(l => l.Status == status);
                statusDesc = $"trạng thái '{status}'";
            }

            // 2. Ánh xạ mốc thời gian chuẩn hóa
            string timeDesc = "";
            if (string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase))
            {
                timeDesc = "toàn bộ thời gian";
            }
            else if (string.Equals(scope, "today", StringComparison.OrdinalIgnoreCase) || scope == "1" || (scope == "range" && days == 1))
            {
                DateTime todayStartUtc = DateTimeHelper.VietnamToUtc(DateTimeHelper.VietnamNow.Date);
                query = query.Where(l => l.CreatedAt < todayStartUtc);
                timeDesc = $"cũ hơn hôm nay (trước {DateTimeHelper.VietnamNow.Date:dd/MM/yyyy})";
            }
            else
            {
                int safeDays = days > 0 ? days : 7;
                DateTime cutoffUtc = DateTime.UtcNow.AddDays(-safeDays);
                query = query.Where(l => l.CreatedAt < cutoffUtc);
                timeDesc = $"cũ hơn {safeDays} ngày (trước {cutoffUtc.ToLocalTime():dd/MM/yyyy HH:mm})";
            }

            int count = await query.CountAsync();
            return (count, $"{timeDesc}, {statusDesc}");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteOldLogs(string scope = "today", int days = 1, string status = "All")
        {
            if (!CheckPermission("DeleteLogs", out string err))
                return Json(new { success = false, message = err });

            // Rào chắn bảo vệ an toàn: Ngăn chặn xóa nhầm toàn bộ nếu days <= 0 mà không chọn rõ ràng 'all'
            if (!string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scope, "today", StringComparison.OrdinalIgnoreCase) &&
                scope != "1" && days <= 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn phạm vi thời gian hoặc nhập số ngày hợp lệ (tối thiểu 1 ngày)." });
            }

            try
            {
                bool isAll = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase);
                bool isBeforeToday = string.Equals(scope, "today", StringComparison.OrdinalIgnoreCase) || scope == "1" || (scope == "range" && days == 1);

                bool isSuccessStatus = string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(status, "Succeeded", StringComparison.OrdinalIgnoreCase);
                bool isSpecificStatus = !string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase);

                DateTime? cutoffUtc = null;
                string criteriaDesc = "";

                if (isAll)
                {
                    criteriaDesc = "toàn bộ nhật ký";
                }
                else if (isBeforeToday)
                {
                    cutoffUtc = DateTimeHelper.VietnamToUtc(DateTimeHelper.VietnamNow.Date);
                    criteriaDesc = $"nhật ký cũ hơn hôm nay (trước {DateTimeHelper.VietnamNow.Date:dd/MM/yyyy})";
                }
                else
                {
                    int safeDays = days > 0 ? days : 7;
                    cutoffUtc = DateTime.UtcNow.AddDays(-safeDays);
                    criteriaDesc = $"nhật ký cũ hơn {safeDays} ngày (trước {cutoffUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm})";
                }

                if (isSuccessStatus)
                {
                    criteriaDesc += " [trạng thái Thành công]";
                }
                else if (isSpecificStatus)
                {
                    criteriaDesc += $" [trạng thái '{status}']";
                }

                int deleted = 0;
                using (var trans = db.Database.BeginTransaction())
                {
                    if (isAll)
                    {
                        if (!isSpecificStatus)
                        {
                            deleted = await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.AgentActionLogs");
                        }
                        else if (isSuccessStatus)
                        {
                            deleted = await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.AgentActionLogs WHERE Status IN ('Success', 'Succeeded')");
                        }
                        else
                        {
                            deleted = await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.AgentActionLogs WHERE Status = @p0", status);
                        }
                    }
                    else
                    {
                        if (!isSpecificStatus)
                        {
                            deleted = await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.AgentActionLogs WHERE CreatedAt < @p0", cutoffUtc.Value);
                        }
                        else if (isSuccessStatus)
                        {
                            deleted = await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.AgentActionLogs WHERE CreatedAt < @p0 AND Status IN ('Success', 'Succeeded')", cutoffUtc.Value);
                        }
                        else
                        {
                            deleted = await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.AgentActionLogs WHERE CreatedAt < @p0 AND Status = @p1", cutoffUtc.Value, status);
                        }
                    }
                    trans.Commit();
                }

                int remainingCount = await db.AgentActionLogs.CountAsync();

                LogAdminAction("delete_old_logs", "AgentActionLog", null, "Success",
                    JsonConvert.SerializeObject(new { scope, days, status, deletedCount = deleted }),
                    $"Đã dọn dẹp và xóa {deleted} bản ghi ({criteriaDesc}). Còn lại: {remainingCount} bản ghi.");

                if (deleted == 0)
                {
                    return Json(new
                    {
                        success = true,
                        deletedCount = 0,
                        remainingCount = remainingCount,
                        message = $"Không có bản ghi nhật ký nào thỏa mãn điều kiện ({criteriaDesc}) để dọn dẹp."
                    });
                }

                return Json(new
                {
                    success = true,
                    deletedCount = deleted,
                    remainingCount = remainingCount,
                    message = $"Đã dọn dẹp thành công {deleted} bản ghi ({criteriaDesc})! (Còn lại {remainingCount} bản ghi)"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Đã xảy ra lỗi khi dọn dẹp nhật ký: " + ex.Message
                });
            }
        }

        /// <summary>
        /// Xóa hàng loạt các bản ghi nhật ký được chọn trực tiếp qua checkbox
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteSelectedLogs(long[] logIds)
        {
            if (!CheckPermission("DeleteLogs", out string err))
                return Json(new { success = false, message = err });

            if (logIds == null || logIds.Length == 0)
            {
                var rawIds = Request.Form.GetValues("logIds[]") ?? Request.Form.GetValues("logIds");
                if (rawIds != null && rawIds.Length > 0)
                {
                    logIds = rawIds.Select(s => long.TryParse(s, out var v) ? v : 0).Where(v => v > 0).ToArray();
                }
            }

            if (logIds == null || logIds.Length == 0)
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một bản ghi nhật ký để xóa." });

            if (logIds.Length > 200)
                return Json(new { success = false, message = "Mỗi lần chỉ được chọn xóa tối đa 200 bản ghi." });

            var validIds = logIds.Where(id => id > 0).Distinct().ToList();
            if (validIds.Count == 0)
                return Json(new { success = false, message = "Danh sách mã bản ghi không hợp lệ." });

            try
            {
                int deleted = 0;
                using (var trans = db.Database.BeginTransaction())
                {
                    string idList = string.Join(",", validIds);
                    deleted = await db.Database.ExecuteSqlCommandAsync($"DELETE FROM dbo.AgentActionLogs WHERE LogID IN ({idList})");
                    trans.Commit();
                }

                int remainingCount = await db.AgentActionLogs.CountAsync();

                LogAdminAction("delete_selected_logs", "AgentActionLog", null, "Success",
                    JsonConvert.SerializeObject(new { count = validIds.Count, deletedCount = deleted }),
                    $"Đã xóa thủ công {deleted} bản ghi nhật ký được chọn. Còn lại: {remainingCount} bản ghi.");

                return Json(new
                {
                    success = true,
                    deletedCount = deleted,
                    remainingCount = remainingCount,
                    message = $"Đã xóa thành công {deleted} bản ghi nhật ký được chọn!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi khi xóa các bản ghi được chọn: " + ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteSingleLog(long logId)
        {
            if (!CheckPermission("DeleteLogs", out string err))
                return Json(new { success = false, message = err });

            if (logId <= 0)
                return Json(new { success = false, message = "Mã bản ghi nhật ký không hợp lệ." });

            try
            {
                var log = await db.AgentActionLogs.FindAsync(logId);
                if (log == null)
                    return Json(new { success = false, message = "Không tìm thấy bản ghi nhật ký cần xóa." });

                db.AgentActionLogs.Remove(log);
                await db.SaveChangesAsync();

                int remainingCount = await db.AgentActionLogs.CountAsync();

                LogAdminAction("delete_single_log", "AgentActionLog", logId, "Success",
                    JsonConvert.SerializeObject(new { logId }),
                    $"Đã xóa bản ghi nhật ký #{logId}. Còn lại: {remainingCount} bản ghi.");

                return Json(new
                {
                    success = true,
                    deletedCount = 1,
                    remainingCount = remainingCount,
                    message = $"Đã xóa thành công bản ghi nhật ký #{logId}!"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi khi xóa bản ghi: " + ex.Message
                });
            }
        }

        // ══════════════════════════════════════════════════════
        // TAB 3: CẤU HÌNH AGENT (CONFIGURATION)
        // ══════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SaveAgentConfig(AgentConfigModel model)
        {
            if (!CheckPermission("ManageContent", out string err))
                return Json(new { success = false, message = err });

            if (model == null) return Json(new { success = false, message = "Dữ liệu cấu hình không hợp lệ." });

            var currentConfig = AgentConfigService.GetConfig();

            currentConfig.AgentName = string.IsNullOrWhiteSpace(model.AgentName) ? "Trợ lý ảo Mr. T" : model.AgentName.Trim();
            string selectedModel = model.PrimaryModel?.Trim();
            if (string.IsNullOrWhiteSpace(selectedModel) || !Array.Exists(AgentConfigModel.SupportedModels, m => string.Equals(m, selectedModel, StringComparison.OrdinalIgnoreCase)))
            {
                selectedModel = "gemini-1.5-flash";
            }
            currentConfig.PrimaryModel = selectedModel;
            currentConfig.Temperature = Math.Max(0.0, Math.Min(1.0, model.Temperature));
            currentConfig.MaxOutputTokens = Math.Max(256, Math.Min(8192, model.MaxOutputTokens));
            currentConfig.TopP = Math.Max(0.0, Math.Min(1.0, model.TopP));
            currentConfig.TopK = Math.Max(1, Math.Min(100, model.TopK));
            currentConfig.AgentStatus = string.IsNullOrWhiteSpace(model.AgentStatus) ? "Active" : model.AgentStatus.Trim();
            currentConfig.StatusReason = model.StatusReason?.Trim() ?? "";
            currentConfig.CustomSystemPrompt = model.CustomSystemPrompt?.Trim() ?? "";
            currentConfig.SchedulerEnabled = model.SchedulerEnabled;
            currentConfig.SchedulerIntervalSeconds = Math.Max(10, Math.Min(300, model.SchedulerIntervalSeconds));

            // Cập nhật hạn mức tác vụ & chống spam
            if (model.MaxActiveTasksPerUser > 0) currentConfig.MaxActiveTasksPerUser = model.MaxActiveTasksPerUser;
            if (model.MaxDailyTaskRunsPerUser > 0) currentConfig.MaxDailyTaskRunsPerUser = model.MaxDailyTaskRunsPerUser;
            if (model.MaxConcurrentRunsPerUser > 0) currentConfig.MaxConcurrentRunsPerUser = model.MaxConcurrentRunsPerUser;
            if (model.MinIntervalBetweenRunsMinutes >= 1) currentConfig.MinIntervalBetweenRunsMinutes = model.MinIntervalBetweenRunsMinutes;
            if (model.MaxMutatingActionsPerRun > 0) currentConfig.MaxMutatingActionsPerRun = model.MaxMutatingActionsPerRun;
            if (model.MaxTaskRetryAttempts >= 0) currentConfig.MaxTaskRetryAttempts = model.MaxTaskRetryAttempts;
            if (model.TaskTimeoutSeconds >= 10) currentConfig.TaskTimeoutSeconds = model.TaskTimeoutSeconds;

            if (model.MaxDailyPostsPerUser > 0) currentConfig.MaxDailyPostsPerUser = model.MaxDailyPostsPerUser;
            if (model.MaxDailyCommentsPerUser > 0) currentConfig.MaxDailyCommentsPerUser = model.MaxDailyCommentsPerUser;
            if (model.MaxDailyReactionsPerUser > 0) currentConfig.MaxDailyReactionsPerUser = model.MaxDailyReactionsPerUser;
            if (model.MaxDailyFollowsPerUser > 0) currentConfig.MaxDailyFollowsPerUser = model.MaxDailyFollowsPerUser;
            if (model.DuplicateCheckWindowHours > 0) currentConfig.DuplicateCheckWindowHours = model.DuplicateCheckWindowHours;
            if (model.DuplicateSimilarityThreshold > 0) currentConfig.DuplicateSimilarityThreshold = model.DuplicateSimilarityThreshold;

            if (model.MaxDailyGeminiRequestsPerUser > 0) currentConfig.MaxDailyGeminiRequestsPerUser = model.MaxDailyGeminiRequestsPerUser;
            if (model.MaxDailyGeminiRequestsGlobal > 0) currentConfig.MaxDailyGeminiRequestsGlobal = model.MaxDailyGeminiRequestsGlobal;
            if (model.MaxConcurrentGeminiRequestsGlobal > 0) currentConfig.MaxConcurrentGeminiRequestsGlobal = model.MaxConcurrentGeminiRequestsGlobal;

            string adminUsername = Session["AdminUsername"] as string ?? "Admin";
            AgentConfigService.SaveConfig(currentConfig, adminUsername);

            LogAdminAction("save_agent_config", "SystemConfig", null, "Success",
                JsonConvert.SerializeObject(new { currentConfig.PrimaryModel, currentConfig.AgentStatus, currentConfig.Temperature }),
                "Cập nhật cấu hình Agent thành công.");

            return Json(new { success = true, message = "Đã lưu và áp dụng cấu hình Agent thành công!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SetAgentStatus(string status, string reason)
        {
            if (!CheckPermission("ManageContent", out string err))
                return Json(new { success = false, message = err });

            var config = AgentConfigService.GetConfig();
            config.AgentStatus = status ?? "Active";
            config.StatusReason = reason?.Trim() ?? "";

            string adminUsername = Session["AdminUsername"] as string ?? "Admin";
            AgentConfigService.SaveConfig(config, adminUsername);

            LogAdminAction("set_agent_status", "SystemConfig", null, "Success",
                JsonConvert.SerializeObject(new { status, reason }),
                $"Thay đổi trạng thái Agent sang {status}.");

            return Json(new { success = true, message = $"Đã cập nhật trạng thái Agent: {status}!" });
        }

        // ══════════════════════════════════════════════════════
        // TAB 4: QUẢN LÝ API & PHÂN QUYỀN
        // ══════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> TestApiConnection(string customKey, string customModel)
        {
            if (!CheckPermission("Interact", out string err))
                return Json(new { success = false, message = err });

            var result = await _aiClient.TestConnectionAsync(customKey, customModel);

            LogAdminAction("test_api_connection", "GeminiApi", null, result.Success ? "Success" : "Failed",
                JsonConvert.SerializeObject(new { model = result.ModelUsed, latency = result.LatencyMs }),
                result.Message);

            return Json(new
            {
                success = result.Success,
                message = result.Message,
                latencyMs = result.LatencyMs,
                modelUsed = result.ModelUsed
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SaveApiKey(string apiKey)
        {
            if (!CheckPermission("SuperAdminOnly", out string err))
                return Json(new { success = false, message = err });

            if (string.IsNullOrWhiteSpace(apiKey))
                return Json(new { success = false, message = "API Key không được để trống." });

            string cleanKey = apiKey.Trim().Trim('"', '\'');

            try
            {
                // Lưu vào file AppSettings.config
                string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AppSettings.config");
                if (System.IO.File.Exists(configPath))
                {
                    string xml = System.IO.File.ReadAllText(configPath);
                    if (xml.Contains("GeminiApiKey"))
                    {
                        xml = System.Text.RegularExpressions.Regex.Replace(
                            xml,
                            @"<add\s+key=""GeminiApiKey""\s+value=""[^""]*""\s*/>",
                            $"<add key=\"GeminiApiKey\" value=\"{cleanKey}\" />"
                        );
                    }
                    else
                    {
                        xml = xml.Replace("</appSettings>", $"    <add key=\"GeminiApiKey\" value=\"{cleanKey}\" />\n</appSettings>");
                    }
                    System.IO.File.WriteAllText(configPath, xml);
                }

                LogAdminAction("save_api_key", "GeminiApi", null, "Success", null, "Admin tối cao cập nhật Gemini API Key mới.");

                return Json(new { success = true, message = "Đã cập nhật Gemini API Key mới thành công!", maskedKey = MaskApiKey(cleanKey) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể ghi file cấu hình: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ToggleTool(string toolName, bool isEnabled, bool requireConfirmation)
        {
            if (!CheckPermission("Interact", out string err))
                return Json(new { success = false, message = err });

            var config = AgentConfigService.GetConfig();
            if (config.Tools.ContainsKey(toolName))
            {
                config.Tools[toolName].IsEnabled = isEnabled;
                config.Tools[toolName].RequireConfirmation = requireConfirmation;

                string adminUsername = Session["AdminUsername"] as string ?? "Admin";
                AgentConfigService.SaveConfig(config, adminUsername);

                LogAdminAction("toggle_tool", "AgentTool", null, "Success",
                    JsonConvert.SerializeObject(new { toolName, isEnabled, requireConfirmation }),
                    $"Cập nhật công cụ {toolName}: Enabled={isEnabled}, RequireConfirm={requireConfirmation}");

                return Json(new { success = true, message = $"Đã cập nhật cấu hình công cụ '{toolName}' thành công!" });
            }

            return Json(new { success = false, message = "Không tìm thấy công cụ này." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UpdateSubAdminPermission(int targetUserId, bool isEnabled, bool canAutomation, bool canManageContent, bool canInteract, bool canReadData)
        {
            if (!CheckPermission("SuperAdminOnly", out string err))
                return Json(new { success = false, message = err });

            var targetUser = await db.Users.FindAsync(targetUserId);
            if (targetUser == null || !targetUser.IsAdmin)
                return Json(new { success = false, message = "Không tìm thấy tài khoản Quản trị viên con này." });

            if (targetUser.AdminRole == "SuperAdmin")
                return Json(new { success = false, message = "Không thể chỉnh sửa quyền của Admin tối cao." });

            var perm = await _permService.UpdatePermissionsAsync(
                db,
                targetUserId,
                canRead: canReadData,
                canManage: canManageContent,
                canInteract: canInteract,
                canAutomation: canAutomation,
                isEnabled: isEnabled
            );

            LogAdminAction("update_subadmin_permission", "UserPermission", targetUserId, "Success",
                JsonConvert.SerializeObject(new { targetUserId, isEnabled, canAutomation, canManageContent, canInteract, canReadData }),
                $"Admin tối cao phân quyền AI Agent cho Admin con: {targetUser.Username} ({targetUser.AdminRole})");

            return Json(new { success = true, message = $"Đã cập nhật quyền hạn AI Agent cho {targetUser.Username} thành công!" });
        }

        // ══════════════════════════════════════════════════════
        // GHI NHẬT KÝ KIỂM TOÁN HỆ THỐNG
        // ══════════════════════════════════════════════════════
        private void LogAdminAction(string actionName, string targetType, long? targetId, string status, string inputSummary, string resultSummary, string error = null)
        {
            try
            {
                using (var logDb = new Models.TDMUEcoSystemEntities())
                {
                    string email = Session["AdminEmail"] as string ?? "";
                    var user = !string.IsNullOrEmpty(email) ? logDb.Users.FirstOrDefault(u => u.Email == email) : null;
                    int? userId = user?.UserID ?? Session["AdminID"] as int? ?? Session["UserID"] as int?;
                    if (!userId.HasValue)
                    {
                        var anyAdmin = logDb.Users.FirstOrDefault(u => u.IsAdmin);
                        userId = anyAdmin?.UserID;
                    }
                    if (!userId.HasValue) return;

                    // Chuẩn hóa status theo CHECK constraint CK_AgentActionLogs_Status:
                    // ('Cancelled', 'Pending', 'Blocked', 'Failed', 'Succeeded')
                    string safeStatus = "Succeeded";
                    if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                    {
                        safeStatus = "Failed";
                    }
                    else if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        safeStatus = "Pending";
                    }
                    else if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    {
                        safeStatus = "Cancelled";
                    }
                    else if (string.Equals(status, "Blocked", StringComparison.OrdinalIgnoreCase))
                    {
                        safeStatus = "Blocked";
                    }

                    var log = new AgentActionLog
                    {
                        UserID = userId.Value,
                        ActionName = "[ADMIN] " + (actionName != null && actionName.Length > 90 ? actionName.Substring(0, 90) : actionName),
                        TargetType = targetType,
                        TargetID = targetId,
                        Status = safeStatus,
                        InputSummary = inputSummary,
                        ResultSummary = resultSummary,
                        ErrorMessage = error,
                        CreatedAt = DateTime.UtcNow
                    };

                    logDb.AgentActionLogs.Add(log);
                    logDb.SaveChanges();
                }
            }
            catch { }
        }
    }
}
