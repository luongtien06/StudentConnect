using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace StudentConnect.Models.Assistant
{
    public class AgentConfirmationService
    {
        /// <summary>
        /// Tạo một yêu cầu xác nhận hành động nhạy cảm chờ người dùng duyệt
        /// </summary>
        public async Task<AgentPendingConfirmation> CreatePendingConfirmationAsync(
            TDMUEcoSystemEntities db,
            int userId,
            string actionName,
            object actionPayload,
            long? taskId = null,
            int expireMinutes = 30)
        {
            if (userId <= 0) return null;

            string payloadJson = actionPayload is string str ? str : JsonConvert.SerializeObject(actionPayload);

            var conf = new AgentPendingConfirmation
            {
                UserID = userId,
                TaskID = taskId,
                ActionName = actionName ?? "UnknownAction",
                ActionPayload = payloadJson,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expireMinutes)
            };

            db.AgentPendingConfirmations.Add(conf);
            await db.SaveChangesAsync();

            return conf;
        }

        /// <summary>
        /// Xử lý duyệt hoặc từ chối hành động nguyên tử, chống tấn công phát lại (Replay Attack) và Race Condition
        /// </summary>
        public async Task<(bool success, AgentPendingConfirmation conf, string message)> TryResolveConfirmationAsync(
            TDMUEcoSystemEntities db,
            long confirmationId,
            int userId,
            bool approve)
        {
            if (userId <= 0 || confirmationId <= 0)
                return (false, null, "Thông tin xác nhận không hợp lệ.");

            string newStatus = approve ? "Approved" : "Rejected";
            DateTime nowUtc = DateTime.UtcNow;

            // Khóa nguyên tử trên SQL Server: Chỉ DUY NHẤT 1 luồng chuyển được trạng thái từ 'Pending'
            int affected = await db.Database.ExecuteSqlCommandAsync(@"
                UPDATE dbo.AgentPendingConfirmations
                SET Status = @p0, ResolvedAt = @p1
                WHERE ConfirmationID = @p2 AND UserID = @p3 AND Status = 'Pending' AND (ExpiresAt IS NULL OR ExpiresAt > @p1)
            ", newStatus, nowUtc, confirmationId, userId);

            if (affected == 0)
            {
                var currentConf = await db.AgentPendingConfirmations
                    .FirstOrDefaultAsync(c => c.ConfirmationID == confirmationId && c.UserID == userId);

                if (currentConf == null)
                    return (false, null, "Không tìm thấy yêu cầu xác nhận.");

                if (currentConf.ExpiresAt.HasValue && currentConf.ExpiresAt.Value < nowUtc)
                {
                    if (currentConf.Status == "Pending")
                    {
                        currentConf.Status = "Expired";
                        currentConf.ResolvedAt = nowUtc;
                        await db.SaveChangesAsync();
                    }
                    return (false, currentConf, "Yêu cầu xác nhận này đã hết hạn (quá 15 phút).");
                }

                return (false, currentConf, "Yêu cầu xác nhận này đã được xử lý trước đó (" + currentConf.Status + ") và không thể sử dụng lại.");
            }

            var conf = await db.AgentPendingConfirmations.FindAsync(confirmationId);
            if (conf != null)
            {
                conf.Status = newStatus;
                conf.ResolvedAt = nowUtc;
            }
            return (true, conf, null);
        }

        /// <summary>
        /// Xử lý duyệt hoặc từ chối hành động (tương thích ngược)
        /// </summary>
        public async Task<AgentPendingConfirmation> ResolveConfirmationAsync(
            TDMUEcoSystemEntities db,
            long confirmationId,
            int userId,
            bool approve)
        {
            var result = await TryResolveConfirmationAsync(db, confirmationId, userId, approve);
            return result.conf;
        }

        /// <summary>
        /// Lấy danh sách các hành động đang chờ xác nhận của người dùng
        /// </summary>
        public async Task<List<AgentPendingConfirmation>> GetPendingAsync(TDMUEcoSystemEntities db, int userId)
        {
            var now = DateTime.UtcNow;
            return await db.AgentPendingConfirmations
                .Where(c => c.UserID == userId && c.Status == "Pending" && (!c.ExpiresAt.HasValue || c.ExpiresAt > now))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
    }
}
