using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.SessionState;

namespace StudentConnect.Controllers
{
    /// <summary>
    /// API Controller phục vụ đọc dữ liệu trò chuyện với SessionStateBehavior.Disabled.
    /// Loại bỏ hoàn toàn ReaderWriterLock của SessionState, cho phép client mở nhiều tab
    /// hoặc gửi nhiều request đọc đồng thời mà không bị tuần tự hóa (serialized).
    /// Xác thực 100% qua Cookie MachineKey ký điện tử từ UserAuthHelper.
    /// </summary>
    [SessionState(SessionStateBehavior.Disabled)]
    public class ChatApiController : Controller
    {
        private TDMUEcoSystemEntities db = new TDMUEcoSystemEntities();

        /// <summary>
        /// Xác thực danh tính người dùng an toàn từ Cookie ký điện tử MachineKey và đối chiếu DB.
        /// Chặn người dùng nếu cookie hết hạn, chữ ký sai, tài khoản không tồn tại hoặc chưa xác thực email.
        /// Tuyệt đối KHÔNG tin tưởng bất kỳ UserID/SenderID nào do Client gửi lên.
        /// </summary>
        private int? GetAuthenticatedUserId()
        {
            int? userId = UserAuthHelper.GetUserIdFromCookie(Request);
            if (!userId.HasValue || userId.Value <= 0)
            {
                if (Session != null && Session["UserID"] != null)
                {
                    try
                    {
                        int sId = Convert.ToInt32(Session["UserID"]);
                        if (sId > 0) userId = sId;
                    }
                    catch { }
                }
            }
            if (!userId.HasValue || userId.Value <= 0) return null;

            // Kiểm tra trạng thái tài khoản trong DB (chống trường hợp tài khoản đã bị xóa hoặc chưa kích hoạt OTP)
            var user = db.Users.AsNoTracking().FirstOrDefault(u => u.UserID == userId.Value);
            if (user == null || user.IsEmailVerified == false)
            {
                return null;
            }

            return userId.Value;
        }

        // =========================================================================
        // PHÂN TRANG LỊCH SỬ TIN NHẮN (MessageID Cursor) - KHÔNG LOCK SESSION
        // =========================================================================
        [HttpGet]
        public JsonResult GetMessageHistory(int? roomId = null, int beforeMessageId = 999999, int take = 30)
        {
            if (!roomId.HasValue || roomId.Value <= 0)
            {
                return Json(new { success = false, messages = new object[0], message = "Mã phòng không hợp lệ." }, JsonRequestBehavior.AllowGet);
            }

            if (take <= 0 || take > 50) take = 30;

            int? myId = GetAuthenticatedUserId();
            if (!myId.HasValue || myId.Value <= 0)
            {
                Response.StatusCode = 401;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);
            }

            var room = db.ChatRooms.Find(roomId);
            if (room == null || room.IsActive == false)
            {
                Response.StatusCode = 404;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = "Phòng không tồn tại." }, JsonRequestBehavior.AllowGet);
            }

            bool isMember = room.RoomType == "Public" || db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId.Value);
            if (!isMember)
            {
                Response.StatusCode = 403;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = "Bạn không thuộc cuộc trò chuyện này." }, JsonRequestBehavior.AllowGet);
            }

            var rawMessages = db.ChatMessages.AsNoTracking()
                .Include("User")
                .Where(m => m.RoomID == roomId && m.MessageID < beforeMessageId && !m.Content.StartsWith("[SYSTEM_RENAME]"))
                .OrderByDescending(m => m.MessageID)
                .Take(take)
                .ToList();

            bool hasMore = rawMessages.Count == take;
            var orderedMessages = rawMessages.OrderBy(m => m.MessageID).ToList();

            var replyIds = orderedMessages.Where(m => m.ReplyToID.HasValue)
                                          .Select(m => m.ReplyToID.Value)
                                          .Distinct()
                                          .ToList();

            var replyMessages = new Dictionary<int, string>();
            if (replyIds.Any())
            {
                var rawReplies = db.ChatMessages.AsNoTracking()
                    .Where(m => replyIds.Contains(m.MessageID))
                    .Select(m => new { m.MessageID, m.Content })
                    .ToList();

                replyMessages = rawReplies.ToDictionary(m => m.MessageID, m => FormatMessagePreview(m.Content));
            }

            var result = orderedMessages.Select(m =>
            {
                bool isMe = (m.SenderID == myId.Value);
                bool isRevoked = m.Content == "Tin nhắn đã được thu hồi";
                bool isMsgAnon = string.IsNullOrEmpty(m.Content) || !m.Content.StartsWith("[SHOW_ID]:");
                string cleanText = isMsgAnon ? (m.Content ?? "") : (m.Content != null && m.Content.Length >= 10 ? m.Content.Substring(10) : "");
                string senderDisplayName = isMsgAnon ? "Ẩn danh" : (m.User?.DisplayName ?? m.User?.Username ?? "Sinh viên");
                string senderAvatarUrl = isMsgAnon ? null : (m.User?.SafeAvatar ?? "");

                string replySnippet = null;
                if (m.ReplyToID.HasValue && replyMessages.TryGetValue(m.ReplyToID.Value, out var rs))
                {
                    replySnippet = rs;
                }

                return new
                {
                    messageId = m.MessageID,
                    senderId = (isMsgAnon && !isMe) ? 0 : (m.SenderID ?? 0),
                    isMe = isMe,
                    content = cleanText,
                    imageUrl = m.ImageUrl,
                    isRevoked = isRevoked,
                    isHidden = (m.IsHidden == true),
                    isAnonymous = isMsgAnon,
                    senderName = senderDisplayName,
                    senderAvatar = senderAvatarUrl,
                    sentAt = m.SentAt.HasValue ? m.SentAt.Value.ToString("HH:mm") : "",
                    sentAtFull = m.SentAt.HasValue ? m.SentAt.Value.ToString("o") : "",
                    replyToId = m.ReplyToID,
                    replyContent = replySnippet,
                    replySnippet = replySnippet
                };
            }).ToList();

            return Json(new
            {
                success = true,
                messages = result,
                hasMore = hasMore,
                oldestMessageId = orderedMessages.Any() ? orderedMessages.First().MessageID : 0
            }, JsonRequestBehavior.AllowGet);
        }

        // =========================================================================
        // KIỂM TRA SỐ LƯỢNG NGƯỜI TRỰC TUYẾN TRONG PHÒNG - KHÔNG LOCK SESSION
        // =========================================================================
        [HttpGet]
        public JsonResult GetOnlineCount(int? roomId = null)
        {
            if (!roomId.HasValue || roomId.Value <= 0)
            {
                return Json(new { success = false, count = 0, message = "Mã phòng không hợp lệ." }, JsonRequestBehavior.AllowGet);
            }

            int rId = roomId.Value;

            int? myId = GetAuthenticatedUserId();
            if (!myId.HasValue || myId.Value <= 0)
            {
                Response.StatusCode = 401;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = "Vui lòng đăng nhập." }, JsonRequestBehavior.AllowGet);
            }

            var room = db.ChatRooms.Find(rId);
            if (room == null || room.IsActive == false)
            {
                return Json(new { success = false, count = 0 }, JsonRequestBehavior.AllowGet);
            }

            bool isMember = room.RoomType == "Public" || db.ChatParticipants.Any(p => p.RoomID == rId && p.UserID == myId.Value);
            if (!isMember)
            {
                Response.StatusCode = 403;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = "Bạn không thuộc cuộc trò chuyện này." }, JsonRequestBehavior.AllowGet);
            }

            int count = 0;
            if (room.RoomType == "Public")
            {
                // Đếm chính xác số user duy nhất đang kết nối thực tế trong sảnh chính qua SignalR
                count = Hubs.ChatHub.GetRoomOnlineCount(rId);
            }
            else
            {
                // Phòng riêng hoặc nhóm: Đếm số user đang có mặt trực tiếp trong phòng
                int roomUsers = Hubs.ChatHub.GetRoomOnlineCount(rId);
                if (roomUsers > 0)
                {
                    count = roomUsers;
                }
                else
                {
                    // Dự phòng: Đếm số thành viên của phòng này đang trực tuyến trên hệ thống
                    var participantIds = room.ChatParticipants
                        .Where(p => p.UserID.HasValue)
                        .Select(p => p.UserID.Value)
                        .ToList();
                    count = participantIds.Count(uid => Hubs.ChatHub.IsUserOnline(uid));
                }
            }

            return Json(new { success = true, count = count }, JsonRequestBehavior.AllowGet);
        }

        // =========================================================================
        // TIỆN ÍCH TRÍCH XUẤT NỘI DUNG XEM TRƯỚC
        // =========================================================================
        private string FormatMessagePreview(string content)
        {
            if (string.IsNullOrEmpty(content)) return "Đã trả lời tin nhắn";
            if (content.StartsWith("[SHOW_ID]:"))
            {
                content = content.Length > 10 ? content.Substring(10) : "";
            }
            if (content.StartsWith("[ATTACHMENTS_PAYLOAD]:"))
            {
                return "[Tệp đính kèm]";
            }
            if (content.Length > 60) return content.Substring(0, 57) + "...";
            return content;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
