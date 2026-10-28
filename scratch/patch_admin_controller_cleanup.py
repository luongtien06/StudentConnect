with open('Areas/Admin/Controllers/AdminAiAgentController.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. Add using StudentConnect.Helpers; if not present
if 'using StudentConnect.Helpers;' not in code:
    code = 'using StudentConnect.Helpers;\n' + code

# 2. Update FilterLogs
old_filter = """            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(actionName) && actionName != "All")
            {
                query = query.Where(l => l.ActionName == actionName);
            }

            if (!string.IsNullOrWhiteSpace(dateRange))
            {
                DateTime nowUtc = DateTime.UtcNow;
                if (dateRange == "today")
                {
                    DateTime today = nowUtc.Date;
                    query = query.Where(l => l.CreatedAt >= today);
                }
                else if (dateRange == "7days")
                {
                    DateTime sevenDaysAgo = nowUtc.AddDays(-7);
                    query = query.Where(l => l.CreatedAt >= sevenDaysAgo);
                }
                else if (dateRange == "30days")
                {
                    DateTime thirtyDaysAgo = nowUtc.AddDays(-30);
                    query = query.Where(l => l.CreatedAt >= thirtyDaysAgo);
                }
            }"""

new_filter = """            if (!string.IsNullOrWhiteSpace(status) && status != "All")
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
            }"""

# 3. Replace DeleteOldLogs and add CountLogsToDelete and DeleteSelectedLogs
old_delete = """        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteOldLogs(int days = 0, string status = "All")
        {
            if (!CheckPermission("DeleteLogs", out string err))
                return Json(new { success = false, message = err });

            int deleted = 0;
            string criteriaDesc = "";

            try
            {
                if (days <= 0)
                {
                    // Dọn toàn bộ nhật ký (days <= 0)
                    if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(status))
                    {
                        deleted = await db.Database.ExecuteSqlCommandAsync("DELETE FROM dbo.AgentActionLogs");
                        criteriaDesc = "toàn bộ nhật ký hoạt động";
                    }
                    else
                    {
                        deleted = await db.Database.ExecuteSqlCommandAsync(
                            "DELETE FROM dbo.AgentActionLogs WHERE Status = @p0", status);
                        criteriaDesc = $"toàn bộ nhật ký có trạng thái '{status}'";
                    }
                }
                else
                {
                    // Dọn nhật ký cũ hơn X ngày
                    DateTime cutoff = DateTime.UtcNow.AddDays(-days);
                    if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(status))
                    {
                        deleted = await db.Database.ExecuteSqlCommandAsync(
                            "DELETE FROM dbo.AgentActionLogs WHERE CreatedAt < @p0", cutoff);
                        criteriaDesc = $"nhật ký cũ hơn {days} ngày (trước {cutoff.ToLocalTime():dd/MM/yyyy HH:mm})";
                    }
                    else
                    {
                        deleted = await db.Database.ExecuteSqlCommandAsync(
                            "DELETE FROM dbo.AgentActionLogs WHERE CreatedAt < @p0 AND Status = @p1", cutoff, status);
                        criteriaDesc = $"nhật ký trạng thái '{status}' cũ hơn {days} ngày";
                    }
                }

                int remainingCount = await db.AgentActionLogs.CountAsync();

                LogAdminAction("delete_old_logs", "AgentActionLog", null, "Success",
                    JsonConvert.SerializeObject(new { days, status, deletedCount = deleted }),
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
                    message = $"Đã dọn dẹp thành công {deleted} bản ghi ({criteriaDesc})!"
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
        }"""

new_delete = """        /// <summary>
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
        }"""

code = code.replace(old_filter, new_filter)
code = code.replace(old_delete, new_delete)

with open('Areas/Admin/Controllers/AdminAiAgentController.cs', 'w', encoding='utf-8') as f:
    f.write(code)

print("SUCCESS: Updated Areas/Admin/Controllers/AdminAiAgentController.cs")
