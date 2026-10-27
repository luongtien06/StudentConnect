using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace StudentConnect.Models.Assistant
{
    public class AgentAuditService
    {
        /// <summary>
        /// Ghi nhật ký hành động của AI Agent vào bảng AgentActionLogs
        /// </summary>
        public async Task<AgentActionLog> LogActionAsync(
            TDMUEcoSystemEntities db,
            int userId,
            string actionName,
            string targetType,
            long? targetId,
            string status,
            string inputSummary,
            string resultSummary,
            string errorMessage = null,
            long? runId = null,
            long? conversationId = null)
        {
            if (userId <= 0)
                return null;

            try
            {
                var log = new AgentActionLog
                {
                    UserID = userId,
                    RunID = runId,
                    ConversationID = conversationId,
                    ActionName = (actionName ?? "Unknown").Length > 100 ? (actionName ?? "Unknown").Substring(0, 100) : (actionName ?? "Unknown"),
                    TargetType = (targetType ?? "").Length > 50 ? targetType.Substring(0, 50) : targetType,
                    TargetID = targetId,
                    Status = (status ?? "Succeeded").Length > 20 ? status.Substring(0, 20) : status,
                    InputSummary = inputSummary,
                    ResultSummary = resultSummary,
                    ErrorMessage = errorMessage,
                    CreatedAt = DateTime.UtcNow
                };

                db.AgentActionLogs.Add(log);
                await db.SaveChangesAsync();
                return log;
            }
            catch (Exception)
            {
                // Tránh để lỗi ghi log làm sập luồng chính của Agent
                return null;
            }
        }

        /// <summary>
        /// Lấy danh sách lịch sử hành động của người dùng có hỗ trợ lọc và tìm kiếm
        /// </summary>
        public async Task<List<AgentActionLog>> GetRecentLogsAsync(TDMUEcoSystemEntities db, int userId, int take = 50, string keyword = null, string status = null)
        {
            if (userId <= 0)
                return new List<AgentActionLog>();

            var query = db.AgentActionLogs.AsNoTracking().Where(l => l.UserID == userId);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim().ToLower();
                query = query.Where(l => l.ActionName.ToLower().Contains(kw) ||
                                         (l.TargetType != null && l.TargetType.ToLower().Contains(kw)) ||
                                         (l.ResultSummary != null && l.ResultSummary.ToLower().Contains(kw)) ||
                                         (l.InputSummary != null && l.InputSummary.ToLower().Contains(kw)) ||
                                         (l.ErrorMessage != null && l.ErrorMessage.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(l => l.Status == status);
            }

            return await query
                .OrderByDescending(l => l.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        /// <summary>
        /// Tìm bản ghi nhật ký theo ID và UserID
        /// </summary>
        public async Task<AgentActionLog> GetLogByIdAsync(TDMUEcoSystemEntities db, long logId, int userId)
        {
            if (userId <= 0 || logId <= 0) return null;
            return await db.AgentActionLogs.FirstOrDefaultAsync(l => l.LogID == logId && l.UserID == userId);
        }

        /// <summary>
        /// Cập nhật ghi chú / đính chính cho bản ghi nhật ký (bảo toàn các trường kiểm toán kỹ thuật)
        /// </summary>
        public async Task<bool> UpdateLogNoteAsync(TDMUEcoSystemEntities db, long logId, int userId, string newResultSummary)
        {
            var log = await GetLogByIdAsync(db, logId, userId);
            if (log == null) return false;

            log.ResultSummary = (newResultSummary ?? "").Trim();
            await db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Xóa bản ghi nhật ký của người dùng
        /// </summary>
        public async Task<bool> DeleteLogAsync(TDMUEcoSystemEntities db, long logId, int userId)
        {
            var log = await GetLogByIdAsync(db, logId, userId);
            if (log == null) return false;

            db.AgentActionLogs.Remove(log);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
