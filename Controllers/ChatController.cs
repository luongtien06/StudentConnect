using Microsoft.AspNet.SignalR;
using StudentConnect.Hubs;
using StudentConnect.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class ChatController : BaseController
    {
        private static readonly object _anonymousLock = new object();
        private static DateTime _lastCleanupTime = DateTime.MinValue;
        private static readonly object _cleanupLock = new object();

        private void TryScheduleCleanup()
        {
            if ((DateTime.Now - _lastCleanupTime).TotalHours < 1) return;
            lock (_cleanupLock)
            {
                if ((DateTime.Now - _lastCleanupTime).TotalHours < 1) return;
                _lastCleanupTime = DateTime.Now;

                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        CleanupInactiveRooms();
                    }
                    catch { }
                });
            }
        }

        // TRANG NHẮN TIN CHÍNH (Sảnh chung)
        public ActionResult Index()
        {
            if (Session["UserID"] == null)
                return RedirectToAction("Login", "User");

            TryScheduleCleanup();

            int currentUserId = (int)Session["UserID"];
            var me = db.Users.Find(currentUserId);

            if (me != null && (me.LastActivityAt == null ||
            (DateTime.Now - me.LastActivityAt.Value).TotalMinutes > 5))
            {
                me.LastActivityAt = DateTime.Now;
                db.SaveChanges();
            }

            var room = db.ChatRooms.FirstOrDefault(r => r.RoomType == "Public" && r.IsActive == true);
            if (room == null)
            {
                room = new ChatRoom
                {
                    RoomType = "Public",
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };
                db.ChatRooms.Add(room);
                db.SaveChanges();
            }

            ViewBag.RoomID = room.RoomID;
            ViewBag.myId = currentUserId;

            var messages = db.ChatMessages
                 .Where(m => m.RoomID == room.RoomID)
                 .OrderByDescending(m => m.SentAt)
                 .Take(100)
                 .OrderBy(m => m.SentAt)
                 .ToList();
            var replyIds = messages.Where(m => m.ReplyToID.HasValue)
                       .Select(m => m.ReplyToID.Value)
                       .Distinct()
                       .ToList();

            var replyMessages = db.ChatMessages
                                  .Where(m => replyIds.Contains(m.MessageID))
                                  .ToDictionary(m => m.MessageID, m => m.Content);

            ViewBag.ReplyMessages = replyMessages;
            return View(messages);
        }

        //dọn 
        private void CleanupInactiveRooms()
        {
            using (var context = new TDMUEcoSystemEntities())
            {
                var cutoff = DateTime.Now.AddDays(-1); // Tìm phòng không hoạt động hơn 1 ngày

                // Lấy danh sách ID phòng cần xóa
                var oldRoomIds = context.ChatRooms
                               .Where(r => r.IsActive == false
                                        && r.RoomType != "Public"
                                        && r.CreatedAt < cutoff)
                               .Select(r => r.RoomID)
                               .ToList();

                if (oldRoomIds.Any())
                {
                    // Xóa tất cả tin nhắn
                    var msgs = context.ChatMessages.Where(m => oldRoomIds.Contains(m.ChatRoom.RoomID));
                    context.ChatMessages.RemoveRange(msgs);

                    // Xóa tất cả người tham gia trong các phòng
                    var parts = context.ChatParticipants.Where(p => oldRoomIds.Contains(p.ChatRoom.RoomID));
                    context.ChatParticipants.RemoveRange(parts);

                    // Xóa các phòng
                    var rooms = context.ChatRooms.Where(r => oldRoomIds.Contains(r.RoomID));
                    context.ChatRooms.RemoveRange(rooms);

                    // Lưu thay đổi xuống Database
                    context.SaveChanges();
                }
            }
        }

        [HttpPost]
        public JsonResult SendMessage(int roomId, string content, int? replyToId = null)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Hết phiên làm việc." });
            if (string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Nội dung không được để trống." });
            if (content.Length > 2000)
                return Json(new { success = false, message = "Tin nhắn quá dài. Tối đa 2000 ký tự." });

            int myId = (int)Session["UserID"];
            var room = db.ChatRooms.Find(roomId);
            if (room == null) return Json(new { success = false, message = "Phòng không tồn tại." });

            bool isMember = room.RoomType == "Public" ||
                            db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);
            if (!isMember)
                return Json(new { success = false, message = "Bạn không có quyền tham gia phòng này." });

            string replyContent = null;
            if (replyToId.HasValue)
            {
                var replyMsg = db.ChatMessages.Find(replyToId.Value);
                replyContent = replyMsg?.Content;
            }

            var msg = new ChatMessage
            {
                RoomID = roomId,
                SenderID = myId,
                Content = content,
                SentAt = DateTime.Now,
                ReplyToID = replyToId
            };
            db.ChatMessages.Add(msg);
            db.SaveChanges();

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
            hubContext.Clients.Group(roomId.ToString())
                      .addNewMessageToPage(myId, content, null, msg.MessageID, replyToId, replyContent);
            hubContext.Clients.Group(roomId.ToString()).triggerReloadChatList(roomId);

            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult SendImage(int roomId, HttpPostedFileBase file)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Hết phiên làm việc." });

            if (file == null || file.ContentLength <= 0)
                return Json(new { success = false, message = "File không hợp lệ." });

            int myId = (int)Session["UserID"];

            var room = db.ChatRooms.Find(roomId);
            if (room == null)
                return Json(new { success = false, message = "Phòng không tồn tại." });

            bool isMember = room.RoomType == "Public" ||
                            db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);
            if (!isMember)
                return Json(new { success = false, message = "Không có quyền." });

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return Json(new { success = false, message = "Chỉ chấp nhận file ảnh." });

            if (file.ContentLength > 5 * 1024 * 1024)
                return Json(new { success = false, message = "File quá lớn. Tối đa 5MB." });

            try
            {
                string ext = System.IO.Path.GetExtension(file.FileName).ToLower();
                string fileName = Guid.NewGuid().ToString() + ext;
                string relativePath = "/Content/Uploads/Chat/" + fileName;
                string fullPath = Server.MapPath("~" + relativePath);

                string directory = System.IO.Path.GetDirectoryName(fullPath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                file.SaveAs(fullPath);

                var msg = new ChatMessage
                {
                    RoomID = roomId,
                    SenderID = myId,
                    Content = "[Hình ảnh]",
                    ImageUrl = relativePath,
                    SentAt = DateTime.Now
                };
                db.ChatMessages.Add(msg);
                db.SaveChanges();

                // Gửi SignalR real-time cho mọi người trong phòng
                var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                hubContext.Clients.Group(roomId.ToString()).addNewMessageToPage(myId, "[Hình ảnh]", relativePath, msg.MessageID);

                return Json(new { success = true, imgUrl = relativePath });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi server: " + ex.Message });
            }
        }

        // THU HỒI TIN NHẮN
        [HttpPost]
        public JsonResult RevokeMessage(int messageId)
        {
            if (Session["UserID"] == null) return Json(new { success = false });

            int myId = (int)Session["UserID"];
            var msg = db.ChatMessages.Find(messageId);

            if (msg != null && msg.SenderID == myId)
            {
                if (msg.SentAt.HasValue && (DateTime.Now - msg.SentAt.Value).TotalMinutes > 10)
                {
                    return Json(new { success = false, message = "Quá thời gian thu hồi (tối đa 10 phút)." });
                }
                msg.Content = "Tin nhắn đã được thu hồi";
                msg.ImageUrl = null;
                db.SaveChanges();

                var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                hubContext.Clients.Group((msg.RoomID ?? 0).ToString()).onMessageRevoked(messageId);

                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Bạn không có quyền thu hồi tin nhắn này." });
        }

        [HttpPost]
        public JsonResult DeleteMessage(int messageId)
        {
            if (Session["UserID"] == null) return Json(new { success = false });

            int myId = (int)Session["UserID"];
            var msg = db.ChatMessages.Find(messageId);

            if (msg == null || msg.SenderID != myId)
                return Json(new { success = false, message = "Không thể xóa tin nhắn." });

            int roomId = msg.RoomID ?? 0;

            if (!string.IsNullOrEmpty(msg.ImageUrl))
            {
                string fullPath = Server.MapPath("~" + msg.ImageUrl);
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }

            db.ChatMessages.Remove(msg);
            db.SaveChanges();

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
            hubContext.Clients.Group(roomId.ToString()).onMessageDeleted(messageId);

            return Json(new { success = true });
        }

        // Helper lấy tên hiển thị của phòng
        public string GetRoomDisplayName(ChatRoom room, int myId)
        {
            if (room == null) return "Trò chuyện";

            // 1. Ưu tiên RoomName đã lưu trong database
            if (!string.IsNullOrWhiteSpace(room.RoomName))
            {
                return room.RoomName;
            }

            // 2. Kiểm tra xem phòng có tin nhắn đổi tên mới nhất không
            var renameMsg = db.ChatMessages
                              .Where(m => m.RoomID == room.RoomID && m.Content.StartsWith("[SYSTEM_RENAME]:"))
                              .OrderByDescending(m => m.SentAt)
                              .FirstOrDefault();

            if (renameMsg != null)
            {
                string customName = renameMsg.Content.Substring(15).Trim();
                if (!string.IsNullOrEmpty(customName)) return customName;
            }

            // 3. Tên theo loại phòng
            if (room.RoomType == "1vs1" || room.RoomType == "Anonymous")
            {
                var otherPart = db.ChatParticipants
                                  .Include("User")
                                  .FirstOrDefault(p => p.RoomID == room.RoomID && p.UserID != myId);
                return otherPart?.User?.Username ?? (room.RoomType == "Anonymous" ? "Người lạ Ẩn danh" : "Bạn học");
            }
            else
            {
                return room.RoomName ?? (room.RoomType == "RandomGroup" ? "Nhóm 5 Ngẫu nhiên" : "Nhóm trò chuyện #" + room.RoomID);
            }
        }

        public ActionResult GetChatList(int? currentRoomId = null)
        {
            if (Session["UserID"] == null) return Content("");
            int myId = (int)Session["UserID"];

            // Lấy tất cả phòng mà user tham gia (trừ Public)
            var myRoomIds = db.ChatParticipants
                              .Where(p => p.UserID == myId)
                              .Select(p => p.RoomID)
                              .ToList();

            var rooms = db.ChatRooms
                          .Include("ChatParticipants")
                          .Include("ChatParticipants.User")
                          .Where(r => myRoomIds.Contains(r.RoomID) && r.IsActive == true)
                          .ToList();

            var result = new List<ChatListItemViewModel>();

            foreach (var room in rooms)
            {
                var lastMsg = db.ChatMessages
                                .Where(m => m.RoomID == room.RoomID && !m.Content.StartsWith("[SYSTEM_RENAME]:"))
                                .OrderByDescending(m => m.SentAt)
                                .FirstOrDefault();

                string resolvedName = GetRoomDisplayName(room, myId);

                var item = new ChatListItemViewModel
                {
                    RoomID = room.RoomID,
                    RoomType = room.RoomType,
                    RoomName = resolvedName,
                    LastMsgAt = lastMsg?.SentAt ?? room.CreatedAt,
                    LastMessage = lastMsg != null ? (string.IsNullOrEmpty(lastMsg.ImageUrl) ? lastMsg.Content : "[Hình ảnh]") : "Bắt đầu cuộc trò chuyện...",
                    MemberCount = room.ChatParticipants.Count,
                    IsCurrent = (currentRoomId.HasValue && currentRoomId.Value == room.RoomID)
                };

                if (room.RoomType == "1vs1" || room.RoomType == "Anonymous")
                {
                    var otherParticipant = room.ChatParticipants.FirstOrDefault(p => p.UserID != myId);
                    item.OtherUser = otherParticipant?.User;
                }

                result.Add(item);
            }

            result = result.OrderByDescending(x => x.LastMsgAt).ToList();

            return PartialView("_ChatListPartial", result);
        }

        [HttpPost]
        public JsonResult RenameRoom(int roomId, string newName)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Hết phiên làm việc." });

            if (string.IsNullOrWhiteSpace(newName))
                return Json(new { success = false, message = "Tên cuộc trò chuyện không được để trống." });

            int myId = (int)Session["UserID"];
            var room = db.ChatRooms.Find(roomId);
            if (room == null || room.IsActive == false)
                return Json(new { success = false, message = "Phòng chat không tồn tại." });

            bool isMember = db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);
            if (!isMember)
                return Json(new { success = false, message = "Bạn không thuộc cuộc trò chuyện này." });

            string cleanName = newName.Trim();
            if (cleanName.Length > 60) cleanName = cleanName.Substring(0, 60);

            // Cập nhật trực tiếp RoomName trong bảng ChatRooms để lưu vĩnh viễn
            room.RoomName = cleanName;

            // Lưu bản ghi đổi tên dạng system message
            var renameMsg = new ChatMessage
            {
                RoomID = roomId,
                SenderID = myId,
                Content = "[SYSTEM_RENAME]:" + cleanName,
                SentAt = DateTime.Now
            };
            db.ChatMessages.Add(renameMsg);

            string myName = Session["Username"]?.ToString() ?? "Thành viên";
            var notifyMsg = new ChatMessage
            {
                RoomID = roomId,
                SenderID = null,
                Content = $"{myName} đã đổi tên cuộc trò chuyện thành: \"{cleanName}\"",
                SentAt = DateTime.Now.AddMilliseconds(5)
            };
            db.ChatMessages.Add(notifyMsg);
            db.SaveChanges();

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
            hubContext.Clients.Group(roomId.ToString()).addNewMessageToPage(0, notifyMsg.Content, null, notifyMsg.MessageID);
            hubContext.Clients.Group(roomId.ToString()).onRoomRenamed(roomId, cleanName);
            hubContext.Clients.All.triggerReloadChatList(roomId);

            return Json(new { success = true, newName = cleanName });
        }

        // TỐ CÁO VI PHẠM (GỬI ĐẾN ADMIN)
        [HttpPost]
        public JsonResult ReportViolation(int roomId, int? reportedUserId, int? messageId, string reason, string details)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập để thực hiện báo cáo." });

            int reporterId = (int)Session["UserID"];
            string reporterName = Session["Username"]?.ToString() ?? "Sinh viên";

            var room = db.ChatRooms.Find(roomId);
            if (room == null)
                return Json(new { success = false, message = "Phòng chat không tồn tại." });

            string reportedName = "Người dùng ẩn danh";
            if (reportedUserId.HasValue && reportedUserId.Value > 0)
            {
                var targetUser = db.Users.Find(reportedUserId.Value);
                if (targetUser != null) reportedName = targetUser.Username;
            }

            string messageSnippet = "";
            string violatingContent = "";
            if (messageId.HasValue && messageId.Value > 0)
            {
                var targetMsg = db.ChatMessages.Find(messageId.Value);
                if (targetMsg != null)
                {
                    violatingContent = targetMsg.Content ?? "";
                    messageSnippet = $" | Nội dung: \"{(violatingContent.Length > 80 ? violatingContent.Substring(0, 80) + "..." : violatingContent)}\"";
                    if ((!reportedUserId.HasValue || reportedUserId.Value <= 0) && targetMsg.SenderID.HasValue)
                    {
                        reportedUserId = targetMsg.SenderID.Value;
                        var sender = db.Users.Find(targetMsg.SenderID.Value);
                        if (sender != null) reportedName = sender.Username;
                    }
                }
            }

            string alertMessage = $"🚨 BÁO CÁO VI PHẠM: [{reporterName}] đã tố cáo [{reportedName}] trong phòng #{roomId}. Lý do: {reason}. Chi tiết: {details ?? "Không có mô tả"}{messageSnippet}";
            string roomUrl = (room.RoomType == "Public" ? "/Chat/Index" : $"/Chat/Private/{roomId}")
                + $"?reportedUserId={reportedUserId}&reporterId={reporterId}&messageId={messageId}&roomId={roomId}";

            // Chống spam: Không tạo lặp lại báo cáo trùng nội dung trong 30 giây
            DateTime threshold = DateTime.Now.AddSeconds(-30);
            bool isDuplicate = db.Notifications.Any(n => n.Type == "report" && n.Url == roomUrl && n.CreatedAt > threshold);
            if (isDuplicate)
            {
                return Json(new { success = true, message = "Báo cáo của bạn đã được tiếp nhận, vui lòng không gửi lặp lại." });
            }

            NotifyAdmins("report", alertMessage, roomUrl);

            return Json(new { success = true, message = "Báo cáo của bạn đã được gửi đến ban quản trị để xem xét xử lý." });
        }

        [HttpPost]
        public JsonResult DeleteConversation(int roomId)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Hết phiên làm việc." });

            int myId = (int)Session["UserID"];
            var room = db.ChatRooms.Find(roomId);
            if (room == null)
                return Json(new { success = false, message = "Phòng không tồn tại." });

            if (room.RoomType == "Public")
                return Json(new { success = false, message = "Không thể xóa sảnh chung." });

            var participant = db.ChatParticipants.FirstOrDefault(p => p.RoomID == roomId && p.UserID == myId);
            if (participant != null)
            {
                int remaining = db.ChatParticipants.Count(p => p.RoomID == roomId && p.UserID != myId);
                db.ChatParticipants.Remove(participant);

                if (remaining == 0)
                {
                    room.IsActive = false;
                    var msgs = db.ChatMessages.Where(m => m.RoomID == roomId).ToList();
                    db.ChatMessages.RemoveRange(msgs);
                }
                else
                {
                    string myName = Session["Username"]?.ToString() ?? "Một bạn";
                    var notifyMsg = new ChatMessage
                    {
                        RoomID = roomId,
                        SenderID = null,
                        Content = $"{myName} đã rời khỏi cuộc trò chuyện.",
                        SentAt = DateTime.Now
                    };
                    db.ChatMessages.Add(notifyMsg);

                    var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                    hubContext.Clients.Group(roomId.ToString()).addNewMessageToPage(0, notifyMsg.Content, null);
                }

                db.SaveChanges();

                var hubContextAll = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                hubContextAll.Clients.All.triggerReloadChatList(roomId);
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public ActionResult LeaveRoomBeacon(int roomId)
        {
            // Không tự động rời phòng hoặc xóa nhóm khi người dùng thoát/lùi trình duyệt
            // Giữ nguyên cuộc trò chuyện để người dùng có thể quay lại tiếp tục trò chuyện
            // Cuộc trò chuyện chỉ bị xóa khi người dùng chủ động nhấn Xóa thủ công.
            return new HttpStatusCodeResult(200);
        }

        [HttpPost]
        public JsonResult CreatePrivateRoom(int targetId)
        {
            if (Session["UserID"] == null) return Json(new { success = false });
            int myId = (int)Session["UserID"];

            if (myId == targetId)
                return Json(new { success = false, message = "Không thể tự chat." });

            if (db.Users.Find(targetId) == null)
                return Json(new { success = false, message = "Người dùng không tồn tại." });

            var existingRoomId = db.ChatParticipants
                .Where(p => p.UserID == myId && p.ChatRoom.RoomType == "1vs1" && p.ChatRoom.IsActive == true)
                .Select(p => p.RoomID)
                .FirstOrDefault(rid => db.ChatParticipants.Any(p => p.RoomID == rid && p.UserID == targetId));

            if (existingRoomId != null && existingRoomId > 0)
                return Json(new { success = true, roomId = existingRoomId });

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var newRoom = new ChatRoom { RoomType = "1vs1", CreatedAt = DateTime.Now, IsActive = true };
                    db.ChatRooms.Add(newRoom);
                    db.SaveChanges();

                    db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = myId, JoinedAt = DateTime.Now });
                    db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = targetId, JoinedAt = DateTime.Now });
                    db.SaveChanges();

                    transaction.Commit();

                    var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                    hubContext.Clients.All.triggerReloadChatList(newRoom.RoomID);

                    return Json(new { success = true, roomId = newRoom.RoomID });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = ex.Message });
                }
            }
        }

        public ActionResult Private(int id)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int myId = (int)Session["UserID"];
            var room = db.ChatRooms.Find(id);
            if (room == null || room.IsActive == false)
            {
                TempData["Error"] = "Phòng chat không còn tồn tại.";
                return RedirectToAction("Index");
            }
            var isMember = db.ChatParticipants.Any(p => p.RoomID == id && p.UserID == myId);
            if (!isMember)
            {
                TempData["Error"] = "Bạn không có quyền truy cập phòng này.";
                return RedirectToAction("Index");
            }
            ViewBag.RoomID = id;
            ViewBag.myId = myId;
            ViewBag.IsPrivate = true;
            ViewBag.IsChatPage = true;
            ViewBag.RoomType = room.RoomType;

            string roomTitle = GetRoomDisplayName(room, myId);
            ViewBag.RoomTitle = roomTitle;

            if (room.RoomType == "Anonymous")
            {
                int memberCount = db.ChatParticipants.Count(p => p.RoomID == id);
                ViewBag.WaitingForPair = memberCount < 2;
            }
            var messages = db.ChatMessages
                             .Where(m => m.RoomID == room.RoomID && !m.Content.StartsWith("[SYSTEM_RENAME]:"))
                             .OrderByDescending(m => m.SentAt)
                             .Take(100)
                             .OrderBy(m => m.SentAt)
                             .ToList();
            var replyIds = messages.Where(m => m.ReplyToID.HasValue)
                                   .Select(m => m.ReplyToID.Value)
                                   .Distinct()
                                   .ToList();
            var replyMessages = db.ChatMessages
                                  .Where(m => replyIds.Contains(m.MessageID))
                                  .ToDictionary(m => m.MessageID, m => m.Content);
            ViewBag.ReplyMessages = replyMessages;
            return View("Index", messages);
        }

        // check room
        public JsonResult CheckRoomReady(int roomId)
        {
            int count = db.ChatParticipants.Count(p => p.RoomID == roomId);
            return Json(new { ready = count >= 2 }, JsonRequestBehavior.AllowGet);
        }

        // THAM GIA NHÓM NGẪU NHIÊN
        [HttpPost]
        public JsonResult JoinRandomGroup()
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập." });
            int myId = (int)Session["UserID"];

            var targetRoom = db.ChatRooms
                .Where(r => r.RoomType == "RandomGroup" && r.IsActive == true)
                .Where(r => db.ChatParticipants.Count(p => p.RoomID == r.RoomID) < 5)
                .Where(r => !db.ChatParticipants.Any(p => p.RoomID == r.RoomID && p.UserID == myId))
                .FirstOrDefault();

            if (targetRoom == null)
            {
                targetRoom = new ChatRoom { RoomType = "RandomGroup", CreatedAt = DateTime.Now, IsActive = true };
                db.ChatRooms.Add(targetRoom);
                db.SaveChanges();
            }

            db.ChatParticipants.Add(new ChatParticipant { RoomID = targetRoom.RoomID, UserID = myId });
            db.SaveChanges();

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
            hubContext.Clients.Group(targetRoom.RoomID.ToString())
                      .addNewMessageToPage(0, "Một người dùng vừa tham gia nhóm.", null);

            return Json(new { success = true, roomId = targetRoom.RoomID });
        }

        [HttpPost]
        public JsonResult JoinRandomAnonymous()
        {
            if (Session["UserID"] == null) return Json(new { success = false });
            int myId = (int)Session["UserID"];

            int roomId = 0;

            lock (_anonymousLock)
            {
                var waitingRoomId = db.ChatParticipants
                    .Where(p => p.ChatRoom.RoomType == "Anonymous"
                             && p.ChatRoom.IsActive == true
                             && p.UserID != myId)
                    .GroupBy(p => p.RoomID)
                    .Where(g => g.Count() == 1)
                    .Select(g => g.Key)
                    .FirstOrDefault();

                if (waitingRoomId != null && waitingRoomId > 0)
                {
                    roomId = (int)waitingRoomId;
                    db.ChatParticipants.Add(new ChatParticipant { RoomID = roomId, UserID = myId });
                    db.SaveChanges();

                    var hub = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                    hub.Clients.Group(roomId.ToString())
                       .addNewMessageToPage(0, "Người lạ đã tham gia. Hãy gửi lời chào! 👋", null);
                }
                else
                {
                    var newRoom = new ChatRoom { RoomType = "Anonymous", CreatedAt = DateTime.Now, IsActive = true };
                    db.ChatRooms.Add(newRoom);
                    db.SaveChanges();

                    roomId = newRoom.RoomID;
                    db.ChatParticipants.Add(new ChatParticipant { RoomID = roomId, UserID = myId });
                    db.SaveChanges();

                    var hub = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                    hub.Clients.Group(roomId.ToString())
                       .addNewMessageToPage(0, "Đang tìm người lạ... Vui lòng chờ 🔍", null);
                }
            }

            return Json(new { success = true, roomId = roomId });
        }

        [HttpPost]
        public JsonResult LeaveRoom(int roomId)
        {
            if (Session["UserID"] == null) return Json(new { success = false });
            int myId = (int)Session["UserID"];

            var room = db.ChatRooms.Find(roomId);
            if (room == null) return Json(new { success = false, message = "Phòng không tồn tại." });
            if (room.RoomType == "Public") return Json(new { success = false, message = "Không thể rời sảnh chung." });

            var participant = db.ChatParticipants
                                .FirstOrDefault(p => p.RoomID == roomId && p.UserID == myId);

            if (participant != null)
            {
                int remainCount = db.ChatParticipants.Count(p => p.RoomID == roomId && p.UserID != myId);

                db.ChatParticipants.Remove(participant);

                if (remainCount == 0)
                {
                    room.IsActive = false;
                    if (room.RoomType != "Public")
                    {
                        var msgs = db.ChatMessages.Where(m => m.RoomID == room.RoomID).ToList();
                        db.ChatMessages.RemoveRange(msgs);
                    }
                }
                db.SaveChanges();

                if (remainCount > 0)
                {
                    var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                    hubContext.Clients.Group(roomId.ToString())
                              .addNewMessageToPage(0, "Người lạ đã rời khỏi phòng.", null);
                }
            }

            return Json(new { success = true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        [HttpPost]
        public JsonResult CreateGroupWithEmails(string groupName, List<string> emails)
        {
            if (Session["UserID"] == null) return Json(new { success = false, message = "Hết phiên làm việc." });
            int myId = (int)Session["UserID"];

            if (emails == null || !emails.Any())
                return Json(new { success = false, message = "Vui lòng nhập ít nhất một email thành viên." });

            if (emails.Count > 10)
                return Json(new { success = false, message = "Tối đa 10 thành viên trong nhóm." });

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    string cleanGroupName = string.IsNullOrWhiteSpace(groupName) ? "Nhóm học tập mới" : groupName.Trim();
                    if (cleanGroupName.Length > 60) cleanGroupName = cleanGroupName.Substring(0, 60);

                    var newRoom = new ChatRoom
                    {
                        RoomType = "Group",
                        CreatedAt = DateTime.Now,
                        IsActive = true,
                        MaxUsers = 10
                    };
                    db.ChatRooms.Add(newRoom);
                    db.SaveChanges();

                    db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = myId, JoinedAt = DateTime.Now });

                    // Ghi nhận tên nhóm
                    var renameMsg = new ChatMessage
                    {
                        RoomID = newRoom.RoomID,
                        SenderID = myId,
                        Content = "[SYSTEM_RENAME]:" + cleanGroupName,
                        SentAt = DateTime.Now
                    };
                    db.ChatMessages.Add(renameMsg);

                    var invitedUserIds = new List<int>();
                    var notFoundEmails = new List<string>();

                    foreach (var email in emails.Select(e => e.Trim()).Distinct())
                    {
                        if (string.IsNullOrEmpty(email)) continue;

                        if (!email.EndsWith("@student.tdmu.edu.vn"))
                        {
                            notFoundEmails.Add(email + " (không phải email sinh viên TDMU)");
                            continue;
                        }
                        var user = db.Users.FirstOrDefault(u => u.Email == email);
                        if (user != null && user.UserID != myId)
                        {
                            db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = user.UserID, JoinedAt = DateTime.Now });
                            invitedUserIds.Add(user.UserID);
                        }
                        else if (user == null)
                        {
                            notFoundEmails.Add(email);
                        }
                    }

                    if (!invitedUserIds.Any())
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Không tìm thấy tài khoản sinh viên nào với các email đã nhập." });
                    }

                    string myUsername = Session["Username"]?.ToString() ?? "Trưởng nhóm";
                    var welcomeMsg = new ChatMessage
                    {
                        RoomID = newRoom.RoomID,
                        SenderID = null,
                        Content = $"{myUsername} đã tạo nhóm \"{cleanGroupName}\" với {invitedUserIds.Count + 1} thành viên.",
                        SentAt = DateTime.Now.AddMilliseconds(10)
                    };
                    db.ChatMessages.Add(welcomeMsg);

                    db.SaveChanges();
                    transaction.Commit();

                    var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                    foreach (var targetId in invitedUserIds)
                    {
                        hubContext.Clients.Group("user_" + targetId)
                                  .receiveChatInvitation(myId, myUsername, newRoom.RoomID, targetId);
                    }
                    hubContext.Clients.All.triggerReloadChatList(newRoom.RoomID);

                    return Json(new
                    {
                        success = true,
                        roomId = newRoom.RoomID,
                        notFound = notFoundEmails
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Lỗi server: " + ex.Message });
                }
            }
        }

        [HttpPost]
        public JsonResult InviteToGroup(int roomId, string email)
        {
            if (Session["UserID"] == null) return Json(new { success = false, message = "Hết phiên làm việc." });
            int myId = (int)Session["UserID"];

            var room = db.ChatRooms.Find(roomId);
            if (room == null || room.IsActive == false)
                return Json(new { success = false, message = "Phòng không tồn tại." });

            bool isMember = db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);
            if (!isMember) return Json(new { success = false, message = "Bạn không có quyền mời vào phòng này." });

            int memberCount = db.ChatParticipants.Count(p => p.RoomID == roomId);
            int maxLimit = room.MaxUsers ?? 10;
            if (maxLimit < 10) maxLimit = 10;
            if (memberCount >= maxLimit)
                return Json(new { success = false, message = $"Nhóm đã đạt giới hạn tối đa ({maxLimit} thành viên)." });

            if (string.IsNullOrWhiteSpace(email))
                return Json(new { success = false, message = "Vui lòng nhập email." });

            string cleanEmail = email.Trim();
            if (!cleanEmail.EndsWith("@student.tdmu.edu.vn"))
                return Json(new { success = false, message = "Chỉ chấp nhận email sinh viên TDMU (@student.tdmu.edu.vn)." });

            var user = db.Users.FirstOrDefault(u => u.Email == cleanEmail);
            if (user == null)
                return Json(new { success = false, message = "Email này chưa đăng ký tài khoản sinh viên." });

            if (user.UserID == myId)
                return Json(new { success = false, message = "Bạn đã ở trong nhóm này rồi." });

            bool alreadyIn = db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == user.UserID);
            if (alreadyIn)
                return Json(new { success = false, message = "Thành viên này đã ở trong nhóm rồi." });

            // Nếu là phòng 1vs1 hoặc Anonymous thì nâng cấp thành Group
            if (room.RoomType == "1vs1" || room.RoomType == "Anonymous")
            {
                room.RoomType = "Group";
                room.MaxUsers = 10;
            }

            db.ChatParticipants.Add(new ChatParticipant { RoomID = roomId, UserID = user.UserID, JoinedAt = DateTime.Now });

            string myUsername = Session["Username"]?.ToString() ?? "Một bạn";
            var notifyMsg = new ChatMessage
            {
                RoomID = roomId,
                SenderID = null,
                Content = $"{myUsername} đã thêm {user.Username} vào nhóm.",
                SentAt = DateTime.Now
            };
            db.ChatMessages.Add(notifyMsg);
            db.SaveChanges();

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
            hubContext.Clients.Group(roomId.ToString())
                      .addNewMessageToPage(0, notifyMsg.Content, null, notifyMsg.MessageID);

            hubContext.Clients.Group("user_" + user.UserID)
                      .receiveChatInvitation(myId, myUsername, roomId, user.UserID);

            hubContext.Clients.All.triggerReloadChatList(roomId);

            return Json(new { success = true, message = "Đã thêm " + user.Username + " vào nhóm thành công!" });
        }

        // count online
        [HttpGet]
        public JsonResult GetOnlineCount(int roomId)
        {
            try
            {
                var room = db.ChatRooms.Find(roomId);
                int count;

                if (room != null && room.RoomType == "Public")
                {
                    var cutoff = DateTime.Now.AddMinutes(-5);
                    count = db.Users.Count(u => u.LastActivityAt != null && u.LastActivityAt > cutoff);
                }
                else
                {
                    // Phòng private — đếm participants
                    count = db.ChatParticipants.Count(p => p.RoomID == roomId);
                }

                return Json(new { count = count }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}