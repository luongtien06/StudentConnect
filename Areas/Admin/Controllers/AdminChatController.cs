using StudentConnect.Helpers;
using Microsoft.AspNet.SignalR;

using Newtonsoft.Json;

using StudentConnect.Hubs;

using StudentConnect.Models;

using System;

using System.Collections.Generic;

using System.Data.Entity;

using System.IO;

using System.Linq;

using System.Web.Mvc;

using System.Web.Routing;



namespace StudentConnect.Areas.Admin.Controllers

{

    public class AdminChatController : AdminBaseController

    {

        protected override void OnActionExecuting(ActionExecutingContext filterContext)

        {

            base.OnActionExecuting(filterContext);

            if (filterContext.Result != null) return;



            string currentRole = Session["AdminRole"] as string ?? "";

            bool isSuperAdmin = string.Equals(currentRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            bool isChatAdmin = string.Equals(currentRole, "AdminChat", StringComparison.OrdinalIgnoreCase);



            if (!isSuperAdmin && !isChatAdmin)

            {

                if (filterContext.HttpContext.Request.IsAjaxRequest())

                {

                    filterContext.Result = new JsonResult

                    {

                        Data = new { success = false, message = "Bạn không có quyền truy cập vào chức năng Quản lý Trò chuyện." },

                        JsonRequestBehavior = JsonRequestBehavior.AllowGet

                    };

                }

                else

                {

                    TempData["ErrorMessage"] = "Bạn không có quyền truy cập vào chức năng Quản lý Trò chuyện.";

                    filterContext.Result = new RedirectToRouteResult(

                        new RouteValueDictionary(new { controller = "AdminHome", action = "Index", area = "Admin" }));

                }

                return;

            }



            ViewBag.IsSuperAdmin = isSuperAdmin;

        }



        // ════════════════════════════════════════════════════════════════

        // 0. TRANG CHÍNH: GỘP 5 TAB QUẢN TRỊ TRÒ CHUYỆN

        // ════════════════════════════════════════════════════════════════

        public ActionResult Index()

        {

            // Tác vụ nền dọn dẹp các phòng trống cũ

            System.Web.Hosting.HostingEnvironment.QueueBackgroundWorkItem(_ =>

            {

                try { CleanupInactiveRooms(); }

                catch (Exception ex)

                {

                    System.Diagnostics.Trace.TraceError("CleanupInactiveRooms failed: " + ex.Message);

                }

            });



            var vm = new AdminChatViewModel();

            vm.IsSuperAdmin = ViewBag.IsSuperAdmin ?? false;



            var now = DateTime.Now;

            var today = now.Date;

            var sevenDaysAgo = now.AddDays(-7);

            var thirtyDaysAgo = now.AddDays(-30);



            // Auto-expire out-of-date restrictions (Defensive: an toàn nếu bảng chưa tạo trên DB)
            try
            {
                var expiredRestrictions = db.ChatRestrictions
                    .Where(r => r.IsActive && r.ExpiresAt.HasValue && r.ExpiresAt.Value <= now)
                    .ToList();
                if (expiredRestrictions.Any())
                {
                    foreach (var exp in expiredRestrictions)
                    {
                        exp.IsActive = false;
                        exp.RevokedAt = now;
                        exp.RevokeReason = "Tự động hết hiệu lực theo thời hạn";
                    }
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] Auto-expire ChatRestrictions skipped: " + ex.Message);
            }



            // ── THỐNG KÊ TỔNG QUAN (Tab 0) ──

            var stats = new ChatOverviewStats();

            stats.TotalRooms = db.ChatRooms.Count(r => r.RoomType != "Public");

            stats.ActiveRooms = db.ChatRooms.Count(r => r.RoomType != "Public" && r.IsActive == true);

            stats.ClosedRooms = db.ChatRooms.Count(r => r.RoomType != "Public" && r.IsActive != true);

            stats.Rooms1vs1 = db.ChatRooms.Count(r => r.RoomType == "1vs1");

            stats.RoomsAnonymous = db.ChatRooms.Count(r => r.RoomType == "Anonymous");

            stats.RoomsGroup = db.ChatRooms.Count(r => r.RoomType == "Group" || r.RoomType == "RandomGroup");



            stats.TotalMessages = db.ChatMessages.Count();

            stats.MessagesToday = db.ChatMessages.Count(m => m.SentAt.HasValue && m.SentAt.Value >= today);

            stats.MessagesThisWeek = db.ChatMessages.Count(m => m.SentAt.HasValue && m.SentAt.Value >= sevenDaysAgo);

            stats.MessagesThisMonth = db.ChatMessages.Count(m => m.SentAt.HasValue && m.SentAt.Value >= thirtyDaysAgo);

            stats.TotalActiveUsers = db.ChatMessages.Where(m => m.SenderID.HasValue).Select(m => m.SenderID.Value).Distinct().Count();



            try
            {
                stats.TotalReports = db.ChatReports.Count();
                stats.PendingReports = db.ChatReports.Count(r => r.Status == "Pending");
                stats.ProcessingReports = db.ChatReports.Count(r => r.Status == "Processing");
                stats.ResolvedReports = db.ChatReports.Count(r => r.Status == "Resolved");
                stats.RejectedReports = db.ChatReports.Count(r => r.Status == "Rejected");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] ChatReports stats skipped: " + ex.Message);
            }

            try
            {
                stats.ActiveBannedUsersCount = db.ChatRestrictions
                    .Where(r => r.IsActive && r.RestrictionType == "Ban")
                    .Select(r => r.UserID).Distinct().Count();
                stats.ActiveMutedUsersCount = db.ChatRestrictions
                    .Where(r => r.IsActive && r.RestrictionType == "Mute")
                    .Select(r => r.UserID).Distinct().Count();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] ChatRestrictions stats skipped: " + ex.Message);
            }



            // Biểu đồ xu hướng 7 ngày

            for (int i = 6; i >= 0; i--)

            {

                var dStart = today.AddDays(-i);

                var dEnd = dStart.AddDays(1);

                var mCount = db.ChatMessages.Count(m => m.SentAt.HasValue && m.SentAt.Value >= dStart && m.SentAt.Value < dEnd);

                var uCount = db.ChatMessages.Where(m => m.SentAt.HasValue && m.SentAt.Value >= dStart && m.SentAt.Value < dEnd && m.SenderID.HasValue)

                                           .Select(m => m.SenderID.Value).Distinct().Count();

                stats.DailyTrends.Add(new DailyTrendItem

                {

                    DateLabel = dStart.ToString("dd/MM"),

                    MessageCount = mCount,

                    ActiveUsers = uCount

                });

            }

            vm.Stats = stats;



            // ── TAB 1: DANH SÁCH PHÒNG CHAT HIỆN TẠI (GIỮ NGUYÊN) ──

            vm.Rooms = db.ChatRooms

                         .Where(r => r.RoomType != "Public")

                         .OrderByDescending(r => r.CreatedAt)

                         .ToList();



            // ── TAB 2: TIN NHẮN MỚI NHẤT (Top 60) ──

            var rawMsgs = db.ChatMessages

                .Include(m => m.User)

                .Include(m => m.ChatRoom)

                .OrderByDescending(m => m.SentAt)

                .Take(60)

                .ToList();



            vm.RecentMessages = rawMsgs.Select(m => MapToMessageViewModel(m)).ToList();



            // ── TAB 3: BÁO CÁO VI PHẠM (Top 60) ──
            try
            {
                vm.Reports = GetReportViewModels(db.ChatReports.OrderByDescending(r => r.CreatedAt).Take(60).ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] Reports load skipped: " + ex.Message);
                vm.Reports = new List<ChatReportItemViewModel>();
            }

            // ── TAB 4: THÀNH VIÊN VÀ HẠN CHẾ (Top 50) ──
            try
            {
                vm.Members = GetMemberViewModels(null, null, 1, 50).members;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] Members load skipped: " + ex.Message);
                vm.Members = new List<ChatMemberItemViewModel>();
            }

            try
            {
                vm.ActiveRestrictions = GetActiveRestrictionViewModels();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] ActiveRestrictions load skipped: " + ex.Message);
                vm.ActiveRestrictions = new List<ChatRestrictionItemViewModel>();
            }



            return View(vm);

        }



        // ════════════════════════════════════════════════════════════════

        // 1. QUẢN LÝ PHÒNG CHAT (CÁC CHỨC NĂNG CŨ ĐƯỢC BẢO TOÀN)

        // ════════════════════════════════════════════════════════════════



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult DeleteRoom(int id)

        {

            try

            {

                var room = db.ChatRooms.Find(id);

                if (room == null) return Json(new { success = false, message = "Phòng không tồn tại." });



                var msgs = db.ChatMessages.Where(m => m.RoomID == id);

                db.ChatMessages.RemoveRange(msgs);



                var parts = db.ChatParticipants.Where(p => p.RoomID == id);

                db.ChatParticipants.RemoveRange(parts);



                var reports = db.ChatReports.Where(r => r.RoomID == id);

                foreach (var rep in reports) rep.RoomID = null;



                db.ChatRooms.Remove(room);

                db.SaveChanges();

                ChatHub.EvictRoomConnections(id, "Phòng trò chuyện đã bị xóa bởi Quản trị viên.");

                LogAuditAction("DeleteRoom", "ChatRoom", id, "Xóa phòng chat thành công.");

                return Json(new { success = true, message = "Xóa phòng thành công!" });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi: " + ex.Message });

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult DeleteMultipleRooms(List<int> ids)

        {

            if (ids == null || !ids.Any())

                return Json(new { success = false, message = "Không có phòng nào được chọn." });



            ids = ids.Where(id => id > 0).Distinct().Take(100).ToList();



            try

            {

                var rooms = db.ChatRooms.Where(r => ids.Contains(r.RoomID)).ToList();

                if (!rooms.Any())

                    return Json(new { success = false, message = "Không tìm thấy phòng hợp lệ." });



                var messages = db.ChatMessages.Where(m => ids.Contains(m.RoomID.Value));

                db.ChatMessages.RemoveRange(messages);



                var participants = db.ChatParticipants.Where(p => ids.Contains(p.RoomID.Value));

                db.ChatParticipants.RemoveRange(participants);



                var reports = db.ChatReports.Where(r => r.RoomID.HasValue && ids.Contains(r.RoomID.Value));

                foreach (var rep in reports) rep.RoomID = null;



                db.ChatRooms.RemoveRange(rooms);

                db.SaveChanges();

                foreach (var r in rooms)
                {
                    ChatHub.EvictRoomConnections(r.RoomID, "Phòng trò chuyện đã bị xóa bởi Quản trị viên.");
                }

                LogAuditAction("DeleteMultipleRooms", "ChatRoom", null, $"Đã dọn dẹp {rooms.Count} phòng chat.");

                return Json(new { success = true, message = $"Hệ thống đã dọn dẹp xong {rooms.Count} phòng." });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi: " + (ex.InnerException?.Message ?? ex.Message) });

            }

        }



        [HttpGet]

        public JsonResult GetRoomDetail(int id)

        {

            try

            {

                var room = db.ChatRooms

                    .Include(r => r.ChatParticipants.Select(p => p.User))

                    .Include(r => r.ChatMessages.Select(m => m.User))

                    .FirstOrDefault(r => r.RoomID == id);



                if (room == null)

                    return Json(new { success = false, message = "Phòng không tồn tại." }, JsonRequestBehavior.AllowGet);



                var participants = room.ChatParticipants.Select(p => new

                {

                    userId = p.UserID,

                    username = p.User != null ? p.User.Username : "N/A",

                    displayName = p.User != null ? (p.User.DisplayName ?? p.User.Username) : "Người dùng",

                    avatar = p.User != null && !string.IsNullOrEmpty(p.User.Avatar) ? p.User.Avatar : DefaultImagePlaceholders.AvatarPath,

                    joinedAt = p.JoinedAt.HasValue ? p.JoinedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—",

                    isLeft = p.IsLeft == true

                }).ToList();



                var messages = room.ChatMessages

                    .OrderByDescending(m => m.SentAt)

                    .Take(50)

                    .OrderBy(m => m.SentAt)

                    .Select(m =>

                    {

                        var parsed = ParseMessageContent(m.Content);

                        return new

                        {

                            messageId = m.MessageID,

                            senderId = m.SenderID,

                            senderName = m.User != null ? (m.User.DisplayName ?? m.User.Username) : "Người dùng",

                            senderAvatar = m.User != null && !string.IsNullOrEmpty(m.User.Avatar) ? m.User.Avatar : DefaultImagePlaceholders.AvatarPath,

                            content = parsed.displayText,

                            imageUrl = m.ImageUrl ?? "",

                            isHidden = m.IsHidden == true,

                            hiddenReason = m.HiddenReason,

                            hasAttachments = parsed.hasAttachments,

                            attachments = parsed.attachments,

                            sentAt = m.SentAt.HasValue ? m.SentAt.Value.ToString("HH:mm dd/MM/yyyy") : "—"

                        };

                    }).ToList();



                return Json(new

                {

                    success = true,

                    roomId = room.RoomID,

                    roomName = !string.IsNullOrEmpty(room.RoomName) ? room.RoomName : "Phòng ẩn danh",

                    roomType = room.RoomType,

                    isActive = room.IsActive == true,

                    createdAt = room.CreatedAt.HasValue ? room.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—",

                    maxUsers = room.MaxUsers ?? 2,

                    totalMessages = room.ChatMessages.Count,

                    totalParticipants = room.ChatParticipants.Count,

                    participants = participants,

                    messages = messages

                }, JsonRequestBehavior.AllowGet);

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi: " + ex.Message }, JsonRequestBehavior.AllowGet);

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult ToggleRoomStatus(int id)

        {

            try

            {

                var room = db.ChatRooms.Find(id);

                if (room == null)

                    return Json(new { success = false, message = "Phòng không tồn tại." });



                room.IsActive = !(room.IsActive == true);

                db.SaveChanges();

                if (room.IsActive == false)
                {
                    ChatHub.EvictRoomConnections(id, "Phòng trò chuyện đã bị đóng bởi Quản trị viên.");
                }
                else
                {
                    ChatHub.InvalidateRoomCache(id);
                }

                bool currentStatus = room.IsActive == true;

                LogAuditAction("ToggleRoomStatus", "ChatRoom", id, currentStatus ? "Mở phòng chat" : "Đóng phòng chat");



                return Json(new

                {

                    success = true,

                    isActive = currentStatus,

                    message = currentStatus ? "Đã mở lại hoạt động cho phòng chat." : "Đã đóng phòng chat thành công."

                });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi: " + ex.Message });

            }

        }



        private void CleanupInactiveRooms()

        {

            using (var context = new TDMUEcoSystemEntities())

            {

                var cutoff = DateTime.Now.AddDays(-1);



                var oldRoomIds = context.ChatRooms

                    .Where(r => r.IsActive == false

                             && r.RoomType != "Public"

                             && r.CreatedAt < cutoff)

                    .Select(r => r.RoomID)

                    .ToList();



                if (!oldRoomIds.Any()) return;



                context.ChatMessages.RemoveRange(

                    context.ChatMessages.Where(m => m.RoomID.HasValue && oldRoomIds.Contains(m.RoomID.Value)));



                context.ChatParticipants.RemoveRange(

                    context.ChatParticipants.Where(p => p.RoomID.HasValue && oldRoomIds.Contains(p.RoomID.Value)));



                context.ChatRooms.RemoveRange(

                    context.ChatRooms.Where(r => oldRoomIds.Contains(r.RoomID)));



                context.SaveChanges();

                foreach (var rid in oldRoomIds)
                {
                    ChatHub.EvictRoomConnections(rid, "Phòng trò chuyện đã bị xóa do không hoạt động.");
                }

            }

        }



        // ════════════════════════════════════════════════════════════════

        // 2. QUẢN LÝ TIN NHẮN (MESSAGE MANAGEMENT)

        // ════════════════════════════════════════════════════════════════



        [HttpGet]

        public JsonResult FilterMessages(string keyword, string sender, int? roomId, string roomType, string msgType, string status, string dateRange, int page = 1, int pageSize = 30)

        {

            try

            {

                var query = db.ChatMessages.Include(m => m.User).Include(m => m.ChatRoom).AsQueryable();



                if (!string.IsNullOrWhiteSpace(keyword))

                {

                    string kw = keyword.Trim().ToLower();

                    query = query.Where(m => m.Content != null && m.Content.ToLower().Contains(kw));

                }



                if (!string.IsNullOrWhiteSpace(sender))

                {

                    string snd = sender.Trim().ToLower();

                    query = query.Where(m => m.User != null && (m.User.Username.ToLower().Contains(snd) || m.User.DisplayName.ToLower().Contains(snd)));

                }



                if (roomId.HasValue && roomId.Value > 0)

                {

                    query = query.Where(m => m.RoomID == roomId.Value);

                }



                if (!string.IsNullOrWhiteSpace(roomType) && roomType != "All")

                {

                    query = query.Where(m => m.ChatRoom != null && m.ChatRoom.RoomType == roomType);

                }



                if (!string.IsNullOrWhiteSpace(msgType) && msgType != "All")

                {

                    if (msgType == "Image") query = query.Where(m => !string.IsNullOrEmpty(m.ImageUrl) || m.Content.Contains("[ATTACHMENTS_PAYLOAD]"));

                    else if (msgType == "Text") query = query.Where(m => string.IsNullOrEmpty(m.ImageUrl) && !m.Content.Contains("[ATTACHMENTS_PAYLOAD]"));

                }



                if (!string.IsNullOrWhiteSpace(status) && status != "All")

                {

                    if (status == "Hidden") query = query.Where(m => m.IsHidden == true);

                    else if (status == "Visible") query = query.Where(m => m.IsHidden != true);

                }



                if (!string.IsNullOrWhiteSpace(dateRange) && dateRange != "All")

                {

                    var now = DateTime.Now;

                    if (dateRange == "today")

                    {

                        var t = now.Date;

                        query = query.Where(m => m.SentAt.HasValue && m.SentAt.Value >= t);

                    }

                    else if (dateRange == "7days")

                    {

                        var d = now.AddDays(-7);

                        query = query.Where(m => m.SentAt.HasValue && m.SentAt.Value >= d);

                    }

                    else if (dateRange == "30days")

                    {

                        var d = now.AddDays(-30);

                        query = query.Where(m => m.SentAt.HasValue && m.SentAt.Value >= d);

                    }

                }



                int totalCount = query.Count();

                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                if (totalPages < 1) totalPages = 1;

                if (page < 1) page = 1;

                if (page > totalPages) page = totalPages;



                var list = query.OrderByDescending(m => m.SentAt)

                                .Skip((page - 1) * pageSize)

                                .Take(pageSize)

                                .ToList()

                                .Select(m => MapToMessageViewModel(m))

                                .ToList();



                return Json(new

                {

                    success = true,

                    messages = list,

                    totalCount = totalCount,

                    page = page,

                    totalPages = totalPages

                }, JsonRequestBehavior.AllowGet);

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi tra cứu tin nhắn: " + ex.Message }, JsonRequestBehavior.AllowGet);

            }

        }



        [HttpGet]

        public JsonResult GetMessageDetail(int id)

        {

            try

            {

                var m = db.ChatMessages

                    .Include(x => x.User)

                    .Include(x => x.ChatRoom)

                    .FirstOrDefault(x => x.MessageID == id);



                if (m == null)

                    return Json(new { success = false, message = "Không tìm thấy tin nhắn." }, JsonRequestBehavior.AllowGet);



                var vm = MapToMessageViewModel(m);

                return Json(new { success = true, message = vm }, JsonRequestBehavior.AllowGet);

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi: " + ex.Message }, JsonRequestBehavior.AllowGet);

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult HideMessage(int messageId, string reason)

        {

            try

            {

                var msg = db.ChatMessages.Find(messageId);

                if (msg == null) return Json(new { success = false, message = "Không tìm thấy tin nhắn." });



                string cleanReason = string.IsNullOrWhiteSpace(reason) ? "Nội dung vi phạm tiêu chuẩn cộng đồng" : reason.Trim();

                int adminId = GetCurrentAdminId();



                msg.IsHidden = true;

                msg.HiddenReason = cleanReason;

                msg.HiddenBy = adminId;

                msg.HiddenAt = DateTime.Now;

                db.SaveChanges();



                // SignalR: thông báo cho các thành viên trong phòng cập nhật ngay lập tức

                try

                {

                    var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();

                    hubContext.Clients.Group((msg.RoomID ?? 0).ToString()).onMessageRevoked(messageId);

                }

                catch { }



                LogAuditAction("HideMessage", "ChatMessage", messageId, $"Ẩn tin nhắn #{messageId}: {cleanReason}");

                return Json(new { success = true, message = "Đã ẩn tin nhắn vi phạm thành công!" });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi ẩn tin nhắn: " + ex.Message });

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult UnhideMessage(int messageId)

        {

            try

            {

                var msg = db.ChatMessages.Find(messageId);

                if (msg == null) return Json(new { success = false, message = "Không tìm thấy tin nhắn." });



                msg.IsHidden = false;

                msg.HiddenReason = null;

                msg.HiddenBy = null;

                msg.HiddenAt = null;

                db.SaveChanges();



                LogAuditAction("UnhideMessage", "ChatMessage", messageId, $"Bỏ ẩn tin nhắn #{messageId}");

                return Json(new { success = true, message = "Đã bỏ ẩn và khôi phục hiển thị tin nhắn thành công!" });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi bỏ ẩn tin nhắn: " + ex.Message });

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult AdminDeleteMessage(int messageId, string reason)

        {

            try

            {

                var msg = db.ChatMessages.Find(messageId);

                if (msg == null) return Json(new { success = false, message = "Không tìm thấy tin nhắn." });



                int roomId = msg.RoomID ?? 0;



                // Xóa file ảnh vật lý nếu có

                if (!string.IsNullOrEmpty(msg.ImageUrl))

                {

                    try

                    {

                        string fullPath = Server.MapPath("~" + msg.ImageUrl);

                        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);

                    }

                    catch { }

                }



                // Gỡ liên kết báo cáo nếu báo cáo đang trỏ vào tin nhắn này

                var reports = db.ChatReports.Where(r => r.MessageID == messageId).ToList();

                foreach (var rep in reports)

                {

                    if (string.IsNullOrEmpty(rep.EvidenceSnippet))

                    {

                        rep.EvidenceSnippet = "[Tin nhắn gốc đã bị BQT xóa] " + msg.Content;

                    }

                    rep.MessageID = null;

                }



                db.ChatMessages.Remove(msg);

                db.SaveChanges();



                try

                {

                    var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();

                    hubContext.Clients.Group(roomId.ToString()).onMessageDeleted(messageId);

                }

                catch { }



                LogAuditAction("AdminDeleteMessage", "ChatMessage", messageId, $"Xóa vĩnh viễn tin nhắn #{messageId}. Lý do: {reason}");

                return Json(new { success = true, message = "Đã xóa vĩnh viễn tin nhắn thành công!" });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi xóa tin nhắn: " + ex.Message });

            }

        }



        // ════════════════════════════════════════════════════════════════

        // 3. QUẢN LÝ BÁO CÁO VI PHẠM (VIOLATION REPORTS)

        // ════════════════════════════════════════════════════════════════



        [HttpGet]

        public JsonResult FilterReports(string status, string reason, string keyword, string dateRange, int page = 1, int pageSize = 20)

        {

            try

            {

                var query = db.ChatReports.AsQueryable();



                if (!string.IsNullOrWhiteSpace(status) && status != "All")

                {

                    query = query.Where(r => r.Status == status);

                }



                if (!string.IsNullOrWhiteSpace(reason) && reason != "All")

                {

                    string rs = reason.Trim().ToLower();

                    query = query.Where(r => r.Reason.ToLower().Contains(rs));

                }



                if (!string.IsNullOrWhiteSpace(keyword))

                {

                    string kw = keyword.Trim().ToLower();

                    query = query.Where(r => (r.Details != null && r.Details.ToLower().Contains(kw)) ||

                                             (r.EvidenceSnippet != null && r.EvidenceSnippet.ToLower().Contains(kw)) ||

                                             (r.Reason != null && r.Reason.ToLower().Contains(kw)));

                }



                if (!string.IsNullOrWhiteSpace(dateRange) && dateRange != "All")

                {

                    var now = DateTime.Now;

                    if (dateRange == "today")

                    {

                        var t = now.Date;

                        query = query.Where(r => r.CreatedAt >= t);

                    }

                    else if (dateRange == "7days")

                    {

                        var d = now.AddDays(-7);

                        query = query.Where(r => r.CreatedAt >= d);

                    }

                    else if (dateRange == "30days")

                    {

                        var d = now.AddDays(-30);

                        query = query.Where(r => r.CreatedAt >= d);

                    }

                }



                int totalCount = query.Count();

                int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                if (totalPages < 1) totalPages = 1;

                if (page < 1) page = 1;

                if (page > totalPages) page = totalPages;



                var reports = query.OrderByDescending(r => r.CreatedAt)

                                   .Skip((page - 1) * pageSize)

                                   .Take(pageSize)

                                   .ToList();



                var list = GetReportViewModels(reports);



                return Json(new

                {

                    success = true,

                    reports = list,

                    totalCount = totalCount,

                    page = page,

                    totalPages = totalPages

                }, JsonRequestBehavior.AllowGet);

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi tra cứu báo cáo: " + ex.Message }, JsonRequestBehavior.AllowGet);

            }

        }



        [HttpGet]

        public JsonResult GetReportDetail(int id)

        {

            try

            {

                var report = db.ChatReports.Find(id);

                if (report == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy báo cáo." }, JsonRequestBehavior.AllowGet);
                }

                // Ghi nhận nhật ký Admin truy cập xem chi tiết danh tính vi phạm
                LogAuditAction("ViewReportDetail", "ChatReport", id, $"Admin xem chi tiết và giải mã danh tính báo cáo vi phạm #{id}");



                var vmList = GetReportViewModels(new List<ChatReport> { report });

                var vm = vmList.FirstOrDefault();



                // Kèm theo tin nhắn vi phạm nếu MessageID còn tồn tại

                object attachedMessage = null;

                if (report.MessageID.HasValue)

                {

                    var msg = db.ChatMessages.Include(m => m.User).FirstOrDefault(m => m.MessageID == report.MessageID.Value);

                    if (msg != null)

                    {

                        attachedMessage = MapToMessageViewModel(msg);

                    }

                }



                return Json(new { success = true, report = vm, message = attachedMessage }, JsonRequestBehavior.AllowGet);

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi: " + ex.Message }, JsonRequestBehavior.AllowGet);

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult UpdateReportStatus(int reportId, string status, string handlingNotes)

        {

            try

            {

                var report = db.ChatReports.Find(reportId);

                if (report == null) return Json(new { success = false, message = "Không tìm thấy báo cáo." });



                int adminId = GetCurrentAdminId();

                string cleanStatus = string.IsNullOrWhiteSpace(status) ? "Processing" : status.Trim();



                report.Status = cleanStatus;

                report.HandledBy = adminId;

                report.HandledAt = DateTime.Now;

                if (!string.IsNullOrWhiteSpace(handlingNotes))

                {

                    report.HandlingNotes = handlingNotes.Trim();

                }



                db.SaveChanges();



                LogAuditAction("UpdateReportStatus", "ChatReport", reportId, $"Cập nhật trạng thái báo cáo #{reportId} -> {cleanStatus}");

                return Json(new { success = true, message = $"Đã cập nhật trạng thái báo cáo thành [{cleanStatus}] thành công!" });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi cập nhật: " + ex.Message });

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult ProcessReportWithAction(int reportId, string actionType, string notes, int? durationHours)

        {

            try

            {

                var report = db.ChatReports.Find(reportId);

                if (report == null) return Json(new { success = false, message = "Không tìm thấy báo cáo." });



                int adminId = GetCurrentAdminId();

                string cleanAction = string.IsNullOrWhiteSpace(actionType) ? "None" : actionType.Trim();

                string cleanNotes = string.IsNullOrWhiteSpace(notes) ? "Đã xử lý vi phạm" : notes.Trim();



                // Đối chiếu cứu hộ ReportedUserID nếu báo cáo cũ chưa được liên kết
                if (!report.ReportedUserID.HasValue && report.MessageID.HasValue)
                {
                    var targetMsg = db.ChatMessages.Find(report.MessageID.Value);
                    if (targetMsg != null && targetMsg.SenderID.HasValue && targetMsg.SenderID.Value > 0)
                    {
                        report.ReportedUserID = targetMsg.SenderID.Value;
                        db.SaveChanges();
                    }
                }

                // 1. Thực thi hành động tương ứng

                if (cleanAction == "Warn" && report.ReportedUserID.HasValue)

                {

                    // Gửi thông báo cảnh cáo

                    CreateNotification(report.ReportedUserID.Value, "warning",

                        $"⚠️ Cảnh cáo từ BQT: Tài khoản của bạn bị tố cáo vì '{report.Reason}'. Chi tiết: {cleanNotes}",

                        "/Chat/Index");

                }

                else if (cleanAction == "HideMessage" && report.MessageID.HasValue)

                {

                    var msg = db.ChatMessages.Find(report.MessageID.Value);

                    if (msg != null)

                    {

                        msg.IsHidden = true;

                        msg.HiddenReason = "Bị ẩn do báo cáo vi phạm: " + report.Reason;

                        msg.HiddenBy = adminId;

                        msg.HiddenAt = DateTime.Now;

                    }

                }

                else if (cleanAction == "DeleteMessage" && report.MessageID.HasValue)

                {

                    var msg = db.ChatMessages.Find(report.MessageID.Value);

                    if (msg != null)

                    {

                        db.ChatMessages.Remove(msg);

                        report.MessageID = null;

                    }

                }

                else if ((cleanAction == "MuteUser" || cleanAction == "BanUser") && report.ReportedUserID.HasValue)

                {

                    string resType = cleanAction == "MuteUser" ? "Mute" : "Ban";

                    DateTime? expiresAt = (durationHours.HasValue && durationHours.Value > 0)

                        ? (DateTime?)DateTime.Now.AddHours(durationHours.Value)

                        : null;



                    var rest = new ChatRestriction

                    {

                        UserID = report.ReportedUserID.Value,

                        RestrictionType = resType,

                        Reason = $"Xử lý từ báo cáo #{reportId}: {report.Reason}. {cleanNotes}",

                        CreatedAt = DateTime.Now,

                        ExpiresAt = expiresAt,

                        CreatedByAdminID = adminId,

                        IsActive = true

                    };

                    db.ChatRestrictions.Add(rest);



                    CreateNotification(report.ReportedUserID.Value, "warning",

                        $"⛔ Tài khoản của bạn đã bị { (resType == "Mute" ? "hạn chế gửi tin" : "cấm trò chuyện") } {(expiresAt.HasValue ? $"đến {expiresAt.Value:dd/MM/yyyy HH:mm}" : "vĩnh viễn")}. Lý do: {report.Reason}",

                        "/Chat/Index");

                }



                // 2. Đánh dấu giải quyết báo cáo

                report.Status = "Resolved";

                report.ActionTaken = cleanAction;

                report.HandledBy = adminId;

                report.HandledAt = DateTime.Now;

                report.HandlingNotes = cleanNotes;



                db.SaveChanges();



                LogAuditAction("ProcessReportWithAction", "ChatReport", reportId, $"Xử lý báo cáo #{reportId} với hành động [{cleanAction}]");

                return Json(new { success = true, message = "Đã xử lý và đóng báo cáo vi phạm thành công!" });

            }

            catch (Exception ex)

            {

                string err = ex.Message + (ex.InnerException != null ? " --> " + ex.InnerException.Message + (ex.InnerException.InnerException != null ? " --> " + ex.InnerException.InnerException.Message : "") : "");
                return Json(new { success = false, message = "Lỗi xử lý báo cáo: " + err });

            }

        }



        // ════════════════════════════════════════════════════════════════

        // 4. QUẢN LÝ THÀNH VIÊN VÀ HẠN CHẾ / CẤM CHAT (MEMBER MODERATION)

        // ════════════════════════════════════════════════════════════════



        [HttpGet]

        public JsonResult SearchMembers(string keyword, string filterStatus, int page = 1, int pageSize = 20)

        {

            try

            {

                var result = GetMemberViewModels(keyword, filterStatus, page, pageSize);

                return Json(new

                {

                    success = true,

                    members = result.members,

                    totalCount = result.totalCount,

                    page = page,

                    totalPages = (int)Math.Ceiling((double)result.totalCount / pageSize)

                }, JsonRequestBehavior.AllowGet);

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi tìm kiếm thành viên: " + ex.Message }, JsonRequestBehavior.AllowGet);

            }

        }



        [HttpGet]

        public JsonResult GetMemberDetail(int userId)

        {

            try

            {

                var user = db.Users.Find(userId);

                if (user == null)

                    return Json(new { success = false, message = "Không tìm thấy người dùng." }, JsonRequestBehavior.AllowGet);



                var vm = new ChatMemberItemViewModel

                {

                    UserID = user.UserID,

                    Username = user.Username,

                    DisplayName = user.DisplayName ?? user.Username,

                    Email = user.Email,

                    Avatar = !string.IsNullOrEmpty(user.Avatar) ? user.Avatar : DefaultImagePlaceholders.AvatarPath,

                    PhoneNumber = user.PhoneNumber,

                    IsTDMUStudent = user.IsTDMUStudent == true,

                    TotalRoomsJoined = db.ChatParticipants.Count(p => p.UserID == userId),

                    TotalMessagesSent = db.ChatMessages.Count(m => m.SenderID == userId),

                    TotalReportsReceived = db.ChatReports.Count(r => r.ReportedUserID == userId),

                    JoinedAt = user.CreatedAt,

                    LastActiveAt = user.LastActivityAt

                };



                // Lấy phòng đã tham gia

                var rooms = db.ChatParticipants

                    .Include(p => p.ChatRoom)

                    .Where(p => p.UserID == userId)

                    .OrderByDescending(p => p.JoinedAt)

                    .Take(30)

                    .ToList()

                    .Select(p => new ChatRoomSummaryItem

                    {

                        RoomID = p.RoomID ?? 0,

                        RoomName = p.ChatRoom != null && !string.IsNullOrEmpty(p.ChatRoom.RoomName) ? p.ChatRoom.RoomName : "Phòng #" + p.RoomID,

                        RoomType = p.ChatRoom?.RoomType ?? "1vs1",

                        JoinedAt = p.JoinedAt,

                        IsLeft = p.IsLeft == true

                    }).ToList();

                vm.JoinedRooms = rooms;



                // Lấy lịch sử phạt

                var restrictions = db.ChatRestrictions

                    .Where(r => r.UserID == userId)

                    .OrderByDescending(r => r.CreatedAt)

                    .ToList();



                var adminIds = restrictions.Select(r => r.CreatedByAdminID)

                    .Union(restrictions.Where(r => r.RevokedByAdminID.HasValue).Select(r => r.RevokedByAdminID.Value))

                    .Distinct().ToList();



                var adminsMap = db.Users.Where(u => adminIds.Contains(u.UserID)).ToDictionary(u => u.UserID, u => u.Username);



                vm.RestrictionHistory = restrictions.Select(r =>

                {

                    adminsMap.TryGetValue(r.CreatedByAdminID, out string cName);

                    string rName = null;

                    if (r.RevokedByAdminID.HasValue) adminsMap.TryGetValue(r.RevokedByAdminID.Value, out rName);



                    return new ChatRestrictionItemViewModel

                    {

                        RestrictionID = r.RestrictionID,

                        UserID = r.UserID,

                        RestrictionType = r.RestrictionType,

                        Reason = r.Reason,

                        CreatedAt = r.CreatedAt,

                        CreatedAtFormatted = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),

                        ExpiresAt = r.ExpiresAt,

                        ExpiresAtFormatted = r.ExpiresAt.HasValue ? r.ExpiresAt.Value.ToString("dd/MM/yyyy HH:mm") : "Vĩnh viễn",

                        IsPermanent = !r.ExpiresAt.HasValue,

                        IsActive = r.IsActive && (!r.ExpiresAt.HasValue || r.ExpiresAt.Value > DateTime.Now),

                        CreatedByAdminID = r.CreatedByAdminID,

                        CreatedByAdminName = cName ?? "Admin",

                        RevokedAt = r.RevokedAt,

                        RevokedByAdminName = rName,

                        RevokeReason = r.RevokeReason

                    };

                }).ToList();



                vm.ActiveRestriction = vm.RestrictionHistory.FirstOrDefault(r => r.IsActive);

                vm.IsBanned = vm.ActiveRestriction != null && vm.ActiveRestriction.RestrictionType == "Ban";

                vm.IsMuted = vm.ActiveRestriction != null && vm.ActiveRestriction.RestrictionType == "Mute";



                return Json(new { success = true, member = vm }, JsonRequestBehavior.AllowGet);

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi tải thông tin: " + ex.Message }, JsonRequestBehavior.AllowGet);

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult ApplyRestriction(int userId, string restrictionType, string reason, int? durationHours)

        {

            try

            {

                var user = db.Users.Find(userId);

                if (user == null) return Json(new { success = false, message = "Không tìm thấy người dùng." });



                string cleanType = string.IsNullOrWhiteSpace(restrictionType) ? "Ban" : restrictionType.Trim();

                string cleanReason = string.IsNullOrWhiteSpace(reason) ? "Vi phạm quy tắc trò chuyện" : reason.Trim();

                int adminId = GetCurrentAdminId();



                if (cleanType == "Warn")

                {

                    CreateNotification(userId, "warning",

                        $"⚠️ Cảnh cáo từ BQT: Tài khoản của bạn bị nhắc nhở vì '{cleanReason}'. Hãy tuân thủ quy tắc ứng xử văn minh.",

                        "/Chat/Index");



                    LogAuditAction("WarnUser", "User", userId, $"Gửi cảnh cáo tới @{user.Username}: {cleanReason}");

                    return Json(new { success = true, message = $"Đã gửi cảnh cáo tới người dùng @{user.Username} thành công!" });

                }



                // Nếu có lệnh cấm/mute cũ đang active thì vô hiệu hóa để áp dụng lệnh mới

                var oldActive = db.ChatRestrictions.Where(r => r.UserID == userId && r.IsActive).ToList();

                foreach (var old in oldActive)

                {

                    old.IsActive = false;

                    old.RevokedAt = DateTime.Now;

                    old.RevokedByAdminID = adminId;

                    old.RevokeReason = "Được thay thế bởi quyết định xử phạt mới";

                }



                DateTime? expiresAt = (durationHours.HasValue && durationHours.Value > 0)

                    ? (DateTime?)DateTime.Now.AddHours(durationHours.Value)

                    : null;



                var rest = new ChatRestriction

                {

                    UserID = userId,

                    RestrictionType = cleanType,

                    Reason = cleanReason,

                    CreatedAt = DateTime.Now,

                    ExpiresAt = expiresAt,

                    CreatedByAdminID = adminId,

                    IsActive = true

                };



                db.ChatRestrictions.Add(rest);

                db.SaveChanges();



                string typeLabel = cleanType == "Mute" ? "hạn chế gửi tin nhắn" : "cấm trò chuyện toàn diện";

                string durLabel = expiresAt.HasValue ? $"đến {expiresAt.Value:dd/MM/yyyy HH:mm}" : "vĩnh viễn";



                CreateNotification(userId, "warning",

                    $"⛔ Tài khoản của bạn đã bị {typeLabel} {durLabel}. Lý do: {cleanReason}",

                    "/Chat/Index");



                LogAuditAction("ApplyRestriction", "ChatRestriction", rest.RestrictionID,

                    $"Áp dụng [{cleanType}] cho @{user.Username} {durLabel}. Lý do: {cleanReason}");



                return Json(new

                {

                    success = true,

                    message = $"Đã áp dụng lệnh {typeLabel} ({durLabel}) cho @{user.Username} thành công!"

                });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi áp dụng phạt: " + ex.Message });

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult RevokeRestriction(int restrictionId, string reason)

        {

            try

            {

                var rest = db.ChatRestrictions.Find(restrictionId);

                if (rest == null) return Json(new { success = false, message = "Không tìm thấy bản ghi xử phạt." });



                int adminId = GetCurrentAdminId();

                string cleanReason = string.IsNullOrWhiteSpace(reason) ? "Được BQT xem xét mở cấm trước thời hạn" : reason.Trim();



                rest.IsActive = false;

                rest.RevokedAt = DateTime.Now;

                rest.RevokedByAdminID = adminId;

                rest.RevokeReason = cleanReason;

                db.SaveChanges();



                var user = db.Users.Find(rest.UserID);

                if (user != null)

                {

                    CreateNotification(user.UserID, "system",

                        $"🎉 Bạn đã được mở lại quyền trò chuyện trên hệ thống. Lý do: {cleanReason}",

                        "/Chat/Index");

                }



                LogAuditAction("RevokeRestriction", "ChatRestriction", restrictionId,

                    $"Gỡ bỏ lệnh cấm/hạn chế #{restrictionId} cho User #{rest.UserID}: {cleanReason}");



                return Json(new { success = true, message = "Đã gỡ bỏ hạn chế và mở lại quyền chat thành công!" });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi gỡ cấm: " + ex.Message });

            }

        }



        // ════════════════════════════════════════════════════════════════

        // HELPER FUNCTIONS & MAPPERS

        // ════════════════════════════════════════════════════════════════



        private ChatMessageItemViewModel MapToMessageViewModel(ChatMessage m)

        {

            var parsed = ParseMessageContent(m.Content);

            return new ChatMessageItemViewModel

            {

                MessageID = m.MessageID,

                RoomID = m.RoomID,

                RoomName = m.ChatRoom != null && !string.IsNullOrEmpty(m.ChatRoom.RoomName) ? m.ChatRoom.RoomName : "Phòng #" + m.RoomID,

                RoomType = m.ChatRoom?.RoomType ?? "1vs1",

                SenderID = m.SenderID,

                SenderUsername = m.User?.Username ?? "Ẩn danh",

                SenderDisplayName = m.User?.DisplayName ?? m.User?.Username ?? "Người dùng",

                SenderAvatar = m.User != null && !string.IsNullOrEmpty(m.User.Avatar) ? m.User.Avatar : DefaultImagePlaceholders.AvatarPath,

                RawContent = m.Content,

                DisplayText = parsed.displayText,

                IsAnonymousMsg = parsed.isAnonymous,

                HasAttachments = parsed.hasAttachments,

                AttachmentsCount = parsed.attachments.Count,

                AttachmentItems = parsed.attachments,

                ImageUrl = m.ImageUrl,

                SentAt = m.SentAt,

                SentAtFormatted = m.SentAt.HasValue ? m.SentAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—",

                IsHidden = m.IsHidden == true,

                HiddenReason = m.HiddenReason,

                HiddenBy = m.HiddenBy,

                HiddenAt = m.HiddenAt,

                ReplyToID = m.ReplyToID

            };

        }



        private (string displayText, bool hasAttachments, List<ChatAttachmentItemInfo> attachments, bool isAnonymous) ParseMessageContent(string content)

        {

            if (string.IsNullOrEmpty(content)) return ("", false, new List<ChatAttachmentItemInfo>(), true);



            bool isAnon = true;

            string working = content;



            if (working.StartsWith("[SHOW_ID]:"))

            {

                isAnon = false;

                working = working.Substring("[SHOW_ID]:".Length);

            }



            if (working.StartsWith("[ATTACHMENTS_PAYLOAD]:"))

            {

                try

                {

                    var json = working.Substring("[ATTACHMENTS_PAYLOAD]:".Length);

                    var payload = JsonConvert.DeserializeObject<ChatAttachmentsPayload>(json);

                    if (payload != null && payload.Items != null && payload.Items.Any())

                    {

                        var items = payload.Items.Select(i => new ChatAttachmentItemInfo

                        {

                            FileName = i.FileName,

                            Extension = i.Extension,

                            FileSize = i.FileSize,

                            FormattedSize = i.FormattedSize,

                            IsImage = i.IsImage,

                            Url = i.Url,

                            FileKey = i.FileKey,

                            ContentType = i.ContentType

                        }).ToList();



                        string text = string.IsNullOrWhiteSpace(payload.Text) ? "[Tệp đính kèm]" : payload.Text;

                        return (text, true, items, isAnon);

                    }

                }

                catch { }

            }



            return (working, false, new List<ChatAttachmentItemInfo>(), isAnon);

        }



        private List<ChatReportItemViewModel> GetReportViewModels(List<ChatReport> reports)
        {
            if (reports == null || !reports.Any()) return new List<ChatReportItemViewModel>();

            // 1. Đối chiếu và đồng bộ đáng tin cậy danh tính thật cho các báo cáo cũ (Legacy Data Reconciliation)
            var missingByMsg = reports.Where(r => (!r.ReportedUserID.HasValue || r.ReportedUserID.Value <= 0) && r.MessageID.HasValue).ToList();
            if (missingByMsg.Any())
            {
                var msgIds = missingByMsg.Select(r => r.MessageID.Value).Distinct().ToList();
                var msgAuthors = db.ChatMessages.Where(m => msgIds.Contains(m.MessageID))
                                                .Select(m => new { m.MessageID, m.SenderID })
                                                .ToList();
                var authorMap = msgAuthors.ToDictionary(m => m.MessageID, m => m.SenderID);
                bool hasUpdates = false;
                foreach (var r in missingByMsg)
                {
                    if (authorMap.TryGetValue(r.MessageID.Value, out var realSenderId) && realSenderId.HasValue && realSenderId.Value > 0)
                    {
                        r.ReportedUserID = realSenderId.Value;
                        hasUpdates = true;
                    }
                }
                if (hasUpdates)
                {
                    try { db.SaveChanges(); } catch { }
                }
            }

            var missingByRoom = reports.Where(r => (!r.ReportedUserID.HasValue || r.ReportedUserID.Value <= 0) && r.RoomID.HasValue).ToList();
            if (missingByRoom.Any())
            {
                var roomIds = missingByRoom.Select(r => r.RoomID.Value).Distinct().ToList();
                var targetRooms = db.ChatRooms.Where(rm => roomIds.Contains(rm.RoomID) && (rm.RoomType == "Anonymous" || rm.RoomType == "1vs1")).Select(rm => rm.RoomID).ToList();
                if (targetRooms.Any())
                {
                    var participants = db.ChatParticipants.Where(p => p.RoomID.HasValue && targetRooms.Contains(p.RoomID.Value)).Select(p => new { RoomID = p.RoomID.Value, p.UserID }).ToList();
                    bool hasUpdates = false;
                    foreach (var r in missingByRoom.Where(r => targetRooms.Contains(r.RoomID.Value)))
                    {
                        var otherPart = participants.FirstOrDefault(p => p.RoomID == r.RoomID.Value && p.UserID != r.ReporterID);
                        if (otherPart != null && otherPart.UserID > 0)
                        {
                            r.ReportedUserID = otherPart.UserID;
                            hasUpdates = true;
                        }
                    }
                    if (hasUpdates)
                    {
                        try { db.SaveChanges(); } catch { }
                    }
                }
            }

            // 2. Tải thông tin người dùng và phòng chat
            var userIds = reports.Select(r => r.ReporterID)
                .Union(reports.Where(r => r.ReportedUserID.HasValue).Select(r => r.ReportedUserID.Value))
                .Union(reports.Where(r => r.HandledBy.HasValue).Select(r => r.HandledBy.Value))
                .Distinct().ToList();

            var usersMap = db.Users.Where(u => userIds.Contains(u.UserID))
                                   .ToDictionary(u => u.UserID, u => u);

            var repRoomIds = reports.Where(r => r.RoomID.HasValue).Select(r => r.RoomID.Value).Distinct().ToList();
            var roomsMap = db.ChatRooms.Where(r => repRoomIds.Contains(r.RoomID))
                                       .ToDictionary(r => r.RoomID, r => r);

            var repMsgIds = reports.Where(r => r.MessageID.HasValue).Select(r => r.MessageID.Value).Distinct().ToList();
            var messagesMap = db.ChatMessages.Where(m => repMsgIds.Contains(m.MessageID))
                                             .ToDictionary(m => m.MessageID, m => m);

            return reports.Select(r =>
            {
                usersMap.TryGetValue(r.ReporterID, out var reporter);

                User reportedUser = null;
                if (r.ReportedUserID.HasValue) usersMap.TryGetValue(r.ReportedUserID.Value, out reportedUser);

                User handler = null;
                if (r.HandledBy.HasValue) usersMap.TryGetValue(r.HandledBy.Value, out handler);

                ChatRoom room = null;
                if (r.RoomID.HasValue) roomsMap.TryGetValue(r.RoomID.Value, out room);

                ChatMessage targetMsg = null;
                if (r.MessageID.HasValue) messagesMap.TryGetValue(r.MessageID.Value, out targetMsg);

                bool isAnonymousMsg = false;
                string cleanMessageText = r.EvidenceSnippet ?? "";
                if (targetMsg != null)
                {
                    var parsed = ParseMessageContent(targetMsg.Content);
                    isAnonymousMsg = parsed.isAnonymous;
                    cleanMessageText = parsed.displayText;
                }
                else if (room != null && (room.RoomType == "Anonymous" || (r.EvidenceSnippet != null && !r.EvidenceSnippet.StartsWith("[SHOW_ID]:"))))
                {
                    isAnonymousMsg = true;
                }

                string anonAlias = isAnonymousMsg ? "Ẩn danh" : null;
                string identityStatus = (reportedUser != null)
                    ? "Đã xác định"
                    : (r.ReportedUserID.HasValue ? ("Đã xác định (#" + r.ReportedUserID + ")") : "Chưa xác định danh tính");

                return new ChatReportItemViewModel
                {
                    ReportID = r.ReportID,
                    ReporterID = r.ReporterID,
                    ReporterName = reporter?.Username ?? ("User #" + r.ReporterID),
                    ReporterDisplayName = reporter?.DisplayName ?? reporter?.Username ?? ("User #" + r.ReporterID),
                    ReporterAvatar = reporter?.Avatar ?? DefaultImagePlaceholders.AvatarPath,

                    // ── THÔNG TIN DANH TÍNH THẬT CHO ADMIN ──
                    ReportedUserID = r.ReportedUserID,
                    ReportedUserName = reportedUser?.Username ?? (r.ReportedUserID.HasValue ? "User #" + r.ReportedUserID : "Chưa xác định danh tính"),
                    ReportedUserDisplayName = reportedUser?.DisplayName ?? reportedUser?.Username ?? (r.ReportedUserID.HasValue ? "User #" + r.ReportedUserID : "Chưa xác định danh tính"),
                    ReportedUserAvatar = reportedUser?.Avatar ?? DefaultImagePlaceholders.AvatarPath,
                    ReportedUserEmail = reportedUser?.Email ?? (r.ReportedUserID.HasValue ? "Chưa cập nhật" : "—"),
                    ReportedUserRealUsername = reportedUser?.Username,
                    ReportedUserRealDisplayName = reportedUser?.DisplayName,
                    IsReportedUserAnonymous = isAnonymousMsg,
                    AnonymousAlias = anonAlias ?? (isAnonymousMsg ? "Người lạ ẩn danh" : null),
                    IdentityStatus = identityStatus,
                    MessageContent = cleanMessageText,
                    MessageSentAtFormatted = targetMsg?.SentAt.HasValue == true ? targetMsg.SentAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : null,

                    RoomID = r.RoomID,
                    RoomName = room != null && !string.IsNullOrEmpty(room.RoomName) ? room.RoomName : (r.RoomID.HasValue ? "Phòng #" + r.RoomID : "—"),
                    RoomType = room?.RoomType ?? "1vs1",
                    MessageID = r.MessageID,
                    Reason = r.Reason,
                    Details = r.Details,
                    EvidenceSnippet = cleanMessageText,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    CreatedAtFormatted = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    HandledBy = r.HandledBy,
                    HandledByName = handler?.Username ?? (r.HandledBy.HasValue ? "Admin #" + r.HandledBy : null),
                    HandledAt = r.HandledAt,
                    HandlingNotes = r.HandlingNotes,
                    ActionTaken = r.ActionTaken
                };
            }).ToList();
        }
        private (List<ChatMemberItemViewModel> members, int totalCount) GetMemberViewModels(string keyword, string filterStatus, int page, int pageSize)

        {
            try
            {

            var now = DateTime.Now;



            // Truy vấn người dùng có tham gia chat hoặc có lệnh phạt

            var chatUserIdsQuery = db.ChatParticipants.Where(p => p.UserID.HasValue).Select(p => p.UserID.Value)

                .Union(db.ChatMessages.Where(m => m.SenderID.HasValue).Select(m => m.SenderID.Value))

                .Union(db.ChatRestrictions.Select(r => r.UserID));



            var usersQuery = db.Users.Where(u => chatUserIdsQuery.Contains(u.UserID));



            if (!string.IsNullOrWhiteSpace(keyword))

            {

                string kw = keyword.Trim().ToLower();

                usersQuery = usersQuery.Where(u => u.Username.ToLower().Contains(kw) ||

                                                   (u.DisplayName != null && u.DisplayName.ToLower().Contains(kw)) ||

                                                   (u.Email != null && u.Email.ToLower().Contains(kw)));

            }



            if (!string.IsNullOrWhiteSpace(filterStatus) && filterStatus != "All")

            {

                if (filterStatus == "Banned")

                {

                    var bannedIds = db.ChatRestrictions.Where(r => r.IsActive && r.RestrictionType == "Ban" && (!r.ExpiresAt.HasValue || r.ExpiresAt > now)).Select(r => r.UserID);

                    usersQuery = usersQuery.Where(u => bannedIds.Contains(u.UserID));

                }

                else if (filterStatus == "Muted")

                {

                    var mutedIds = db.ChatRestrictions.Where(r => r.IsActive && r.RestrictionType == "Mute" && (!r.ExpiresAt.HasValue || r.ExpiresAt > now)).Select(r => r.UserID);

                    usersQuery = usersQuery.Where(u => mutedIds.Contains(u.UserID));

                }

                else if (filterStatus == "Normal")

                {

                    var restrictedIds = db.ChatRestrictions.Where(r => r.IsActive && (!r.ExpiresAt.HasValue || r.ExpiresAt > now)).Select(r => r.UserID);

                    usersQuery = usersQuery.Where(u => !restrictedIds.Contains(u.UserID));

                }

            }



            int totalCount = usersQuery.Count();

            if (page < 1) page = 1;



            var users = usersQuery.OrderByDescending(u => u.LastActivityAt ?? u.CreatedAt)

                                  .Skip((page - 1) * pageSize)

                                  .Take(pageSize)

                                  .ToList();



            var uIds = users.Select(u => u.UserID).ToList();



            // Lấy thông tin restriction đang active

            var activeRests = db.ChatRestrictions

                .Where(r => uIds.Contains(r.UserID) && r.IsActive && (!r.ExpiresAt.HasValue || r.ExpiresAt > now))

                .ToList()

                .GroupBy(r => r.UserID)

                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First());



            // Thống kê tin nhắn, phòng, tố cáo

            var msgCounts = db.ChatMessages

                .Where(m => m.SenderID.HasValue && uIds.Contains(m.SenderID.Value))

                .GroupBy(m => m.SenderID.Value)

                .ToDictionary(g => g.Key, g => g.Count());



            var roomCounts = db.ChatParticipants

                .Where(p => p.UserID.HasValue && uIds.Contains(p.UserID.Value))

                .GroupBy(p => p.UserID.Value)

                .ToDictionary(g => g.Key, g => g.Count());



            var repCounts = db.ChatReports

                .Where(r => r.ReportedUserID.HasValue && uIds.Contains(r.ReportedUserID.Value))

                .GroupBy(r => r.ReportedUserID.Value)

                .ToDictionary(g => g.Key, g => g.Count());



            var list = users.Select(u =>

            {

                activeRests.TryGetValue(u.UserID, out var activeR);

                msgCounts.TryGetValue(u.UserID, out int totalMsgs);

                roomCounts.TryGetValue(u.UserID, out int totalRooms);

                repCounts.TryGetValue(u.UserID, out int totalReps);



                return new ChatMemberItemViewModel

                {

                    UserID = u.UserID,

                    Username = u.Username,

                    DisplayName = u.DisplayName ?? u.Username,

                    Email = u.Email,

                    Avatar = !string.IsNullOrEmpty(u.Avatar) ? u.Avatar : DefaultImagePlaceholders.AvatarPath,

                    PhoneNumber = u.PhoneNumber,

                    IsTDMUStudent = u.IsTDMUStudent == true,

                    TotalRoomsJoined = totalRooms,

                    TotalMessagesSent = totalMsgs,

                    TotalReportsReceived = totalReps,

                    IsBanned = activeR != null && activeR.RestrictionType == "Ban",

                    IsMuted = activeR != null && activeR.RestrictionType == "Mute",

                    ActiveRestriction = activeR != null ? new ChatRestrictionItemViewModel

                    {

                        RestrictionID = activeR.RestrictionID,

                        RestrictionType = activeR.RestrictionType,

                        Reason = activeR.Reason,

                        ExpiresAt = activeR.ExpiresAt,

                        ExpiresAtFormatted = activeR.ExpiresAt.HasValue ? activeR.ExpiresAt.Value.ToString("dd/MM/yyyy HH:mm") : "Vĩnh viễn",

                        IsPermanent = !activeR.ExpiresAt.HasValue

                    } : null,

                    JoinedAt = u.CreatedAt,

                    LastActiveAt = u.LastActivityAt

                };

            }).ToList();



            return (list, totalCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] GetMemberViewModels error: " + ex.Message);
                return (new List<ChatMemberItemViewModel>(), 0);
            }

        }



        private List<ChatRestrictionItemViewModel> GetActiveRestrictionViewModels()

        {
            try
            {

            var now = DateTime.Now;

            var list = db.ChatRestrictions

                .Where(r => r.IsActive && (!r.ExpiresAt.HasValue || r.ExpiresAt > now))

                .OrderByDescending(r => r.CreatedAt)

                .Take(50)

                .ToList();



            if (!list.Any()) return new List<ChatRestrictionItemViewModel>();



            var uIds = list.Select(r => r.UserID)

                .Union(list.Select(r => r.CreatedByAdminID))

                .Distinct().ToList();



            var uMap = db.Users.Where(u => uIds.Contains(u.UserID)).ToDictionary(u => u.UserID, u => u);



            return list.Select(r =>

            {

                uMap.TryGetValue(r.UserID, out var targetUser);

                uMap.TryGetValue(r.CreatedByAdminID, out var admin);



                return new ChatRestrictionItemViewModel

                {

                    RestrictionID = r.RestrictionID,

                    UserID = r.UserID,

                    Username = targetUser?.Username ?? ("User #" + r.UserID),

                    DisplayName = targetUser?.DisplayName ?? targetUser?.Username ?? ("User #" + r.UserID),

                    Avatar = targetUser?.Avatar ?? DefaultImagePlaceholders.AvatarPath,

                    RestrictionType = r.RestrictionType,

                    Reason = r.Reason,

                    CreatedAt = r.CreatedAt,

                    CreatedAtFormatted = r.CreatedAt.ToString("dd/MM/yyyy HH:mm"),

                    ExpiresAt = r.ExpiresAt,

                    ExpiresAtFormatted = r.ExpiresAt.HasValue ? r.ExpiresAt.Value.ToString("dd/MM/yyyy HH:mm") : "Vĩnh viễn",

                    IsPermanent = !r.ExpiresAt.HasValue,

                    CreatedByAdminID = r.CreatedByAdminID,

                    CreatedByAdminName = admin?.Username ?? "Admin",

                    IsActive = true

                };

            }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] GetActiveRestrictionViewModels error: " + ex.Message);
                return new List<ChatRestrictionItemViewModel>();
            }

        }



        private int GetCurrentAdminId()

        {

            string email = Session["AdminEmail"] as string ?? "";

            var adminUser = db.Users.FirstOrDefault(u => u.Email == email);

            return adminUser?.UserID ?? 1002;

        }



        private void LogAuditAction(string actionName, string targetType, int? targetId, string details)

        {

            try

            {

                int adminId = GetCurrentAdminId();

                var log = new AgentActionLog

                {

                    UserID = adminId,

                    ActionName = "[ADMIN_CHAT] " + actionName,

                    TargetType = targetType,

                    TargetID = targetId,

                    Status = "Succeeded",

                    InputSummary = details,

                    ResultSummary = "Thực hiện bởi Admin",

                    CreatedAt = DateTime.UtcNow

                };

                db.AgentActionLogs.Add(log);

                db.SaveChanges();

            }

            catch { }

        }



        protected override void Dispose(bool disposing)

        {

            if (disposing) db.Dispose();

            base.Dispose(disposing);

        }

    }

}

