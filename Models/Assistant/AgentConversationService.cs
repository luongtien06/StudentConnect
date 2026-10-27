using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace StudentConnect.Models.Assistant
{
    /// <summary>
    /// Service quản lý các phiên hội thoại Agent (AgentConversations) và tin nhắn (AgentMessages).
    /// Hỗ trợ tạo phiên, lưu tin nhắn, nạp lịch sử, tiếp tục phiên cũ và xóa phiên.
    /// </summary>
    public class AgentConversationService
    {
        /// <summary>
        /// Tạo phiên hội thoại mới cho người dùng
        /// </summary>
        public async Task<AgentConversation> CreateConversationAsync(TDMUEcoSystemEntities db, int userId, string title, long? taskId = null)
        {
            var conv = new AgentConversation
            {
                UserID = userId,
                Title = string.IsNullOrWhiteSpace(title) ? "Cuộc trò chuyện mới" : title.Trim(),
                TaskID = taskId,
                Status = "Active",
                MessageCount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow
            };
            db.AgentConversations.Add(conv);
            await db.SaveChangesAsync();
            return conv;
        }

        /// <summary>
        /// Lưu một tin nhắn vào phiên hội thoại
        /// </summary>
        public async Task<AgentMessage> SaveMessageAsync(
            TDMUEcoSystemEntities db,
            long conversationId,
            string senderType,
            string content,
            string toolCallDetails = null,
            string toolResultDetails = null,
            string errorMessage = null)
        {
            var msg = new AgentMessage
            {
                ConversationID = conversationId,
                SenderType = senderType,
                Content = content ?? "",
                ToolCallDetails = toolCallDetails,
                ToolResultDetails = toolResultDetails,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow
            };
            db.AgentMessages.Add(msg);

            // Cập nhật metadata của phiên
            var conv = await db.AgentConversations.FindAsync(conversationId);
            if (conv != null)
            {
                conv.MessageCount++;
                conv.LastMessageAt = DateTime.UtcNow;
                conv.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return msg;
        }

        /// <summary>
        /// Lấy danh sách phiên hội thoại theo userId (phân trang, tìm kiếm, lọc)
        /// </summary>
        public async Task<List<AgentConversation>> GetConversationsAsync(
            TDMUEcoSystemEntities db,
            int userId,
            int page = 1,
            int pageSize = 15,
            string keyword = null,
            string status = null)
        {
            var query = db.AgentConversations
                .AsNoTracking()
                .Where(c => c.UserID == userId);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim().ToLower();
                query = query.Where(c =>
                    c.Title.ToLower().Contains(kw) ||
                    (c.Summary != null && c.Summary.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(c => c.Status == status);
            }

            return await query
                .OrderByDescending(c => c.LastMessageAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <summary>
        /// Đếm tổng số phiên để phân trang
        /// </summary>
        public async Task<int> CountConversationsAsync(
            TDMUEcoSystemEntities db,
            int userId,
            string keyword = null,
            string status = null)
        {
            var query = db.AgentConversations
                .AsNoTracking()
                .Where(c => c.UserID == userId);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string kw = keyword.Trim().ToLower();
                query = query.Where(c =>
                    c.Title.ToLower().Contains(kw) ||
                    (c.Summary != null && c.Summary.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(c => c.Status == status);
            }

            return await query.CountAsync();
        }

        /// <summary>
        /// Lấy thông tin chi tiết phiên, kiểm tra quyền sở hữu
        /// </summary>
        public async Task<AgentConversation> GetConversationByIdAsync(TDMUEcoSystemEntities db, long conversationId, int userId)
        {
            return await db.AgentConversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ConversationID == conversationId && c.UserID == userId);
        }

        /// <summary>
        /// Lấy các tin nhắn của phiên, theo thứ tự thời gian
        /// </summary>
        public async Task<List<AgentMessage>> GetMessagesAsync(
            TDMUEcoSystemEntities db,
            long conversationId,
            int userId,
            int skip = 0,
            int take = 100)
        {
            // Xác minh quyền sở hữu trước
            bool owns = await db.AgentConversations
                .AsNoTracking()
                .AnyAsync(c => c.ConversationID == conversationId && c.UserID == userId);
            if (!owns) return new List<AgentMessage>();

            return await db.AgentMessages
                .AsNoTracking()
                .Where(m => m.ConversationID == conversationId)
                .OrderBy(m => m.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        /// <summary>
        /// Xây dựng lịch sử hội thoại Gemini API (JArray contents) từ các tin nhắn đã lưu.
        /// Giới hạn N tin nhắn gần nhất để tránh vượt context window.
        /// </summary>
        public async Task<JArray> BuildGeminiHistoryAsync(
            TDMUEcoSystemEntities db,
            long conversationId,
            int userId,
            int maxMessages = 20)
        {
            var messages = await db.AgentMessages
                .AsNoTracking()
                .Where(m => m.ConversationID == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(maxMessages)
                .ToListAsync();

            // Đảo lại thứ tự đúng (cũ trước, mới sau)
            messages.Reverse();

            var normalizedTurns = new List<Tuple<string, string>>();

            foreach (var msg in messages)
            {
                string text = (msg.Content ?? "").Trim();
                if (string.IsNullOrEmpty(text)) continue;

                string sender = (msg.SenderType ?? "").Trim().ToLowerInvariant();
                string role = (sender == "assistant" || sender == "agent" || sender == "model") ? "model" : "user";

                if (normalizedTurns.Count == 0)
                {
                    // Lịch sử gửi tới Gemini bắt buộc phải bắt đầu bằng turn 'user'
                    if (role == "user")
                    {
                        normalizedTurns.Add(Tuple.Create(role, text));
                    }
                }
                else
                {
                    var lastTurn = normalizedTurns[normalizedTurns.Count - 1];
                    if (lastTurn.Item1 == role)
                    {
                        // Cùng role liên tiếp -> gộp nội dung để đảm bảo luân phiên user/model đúng chuẩn Gemini API
                        normalizedTurns[normalizedTurns.Count - 1] = Tuple.Create(role, lastTurn.Item2 + "\n\n" + text);
                    }
                    else
                    {
                        normalizedTurns.Add(Tuple.Create(role, text));
                    }
                }
            }

            var contents = new JArray();
            foreach (var turn in normalizedTurns)
            {
                contents.Add(new JObject
                {
                    ["role"] = turn.Item1,
                    ["parts"] = new JArray { new JObject { ["text"] = turn.Item2 } }
                });
            }

            return contents;
        }

        /// <summary>
        /// Cập nhật tiêu đề và summary phiên
        /// </summary>
        public async Task<bool> UpdateConversationMetadataAsync(
            TDMUEcoSystemEntities db,
            long conversationId,
            int userId,
            string title = null,
            string summary = null,
            string status = null)
        {
            var conv = await db.AgentConversations
                .FirstOrDefaultAsync(c => c.ConversationID == conversationId && c.UserID == userId);
            if (conv == null) return false;

            if (title != null) conv.Title = title.Trim();
            if (summary != null) conv.Summary = summary.Trim();
            if (status != null) conv.Status = status;
            conv.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Xóa phiên hội thoại và toàn bộ tin nhắn liên quan (giữ lại nhật ký kiểm toán nhưng gỡ liên kết)
        /// </summary>
        public async Task<bool> DeleteConversationAsync(TDMUEcoSystemEntities db, long conversationId, int userId)
        {
            var conv = await db.AgentConversations
                .FirstOrDefaultAsync(c => c.ConversationID == conversationId && c.UserID == userId);
            if (conv == null) return false;

            // Gỡ liên kết ConversationID trong AgentActionLogs (không xóa nhật ký kiểm toán)
            var linkedLogs = await db.AgentActionLogs.Where(l => l.ConversationID == conversationId).ToListAsync();
            foreach (var log in linkedLogs)
            {
                log.ConversationID = null;
            }

            // Xóa tất cả tin nhắn trong phiên
            var messages = await db.AgentMessages.Where(m => m.ConversationID == conversationId).ToListAsync();
            db.AgentMessages.RemoveRange(messages);

            db.AgentConversations.Remove(conv);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
