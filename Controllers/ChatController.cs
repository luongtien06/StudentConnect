using Microsoft.AspNet.SignalR;

using StudentConnect.Helpers;

using StudentConnect.Hubs;

using StudentConnect.Models;

using System;

using System.Collections.Generic;

using System.Data.Entity;

using System.Linq;

using System.Web;

using System.Web.Mvc;

using System.Web.Routing;

using System.Threading.Tasks;

using System.IO;



namespace StudentConnect.Controllers

{

    public class ChatController : BaseController

    {

        private static readonly object _anonymousLock = new object();

        private static DateTime _lastCleanupTime = DateTime.MinValue;

        private static readonly object _cleanupLock = new object();



        // Giới hạn tốc độ gửi tin nhắn (Sliding Window Rate Limiter)

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, List<DateTime>> _userMessageTimestamps

            = new System.Collections.Concurrent.ConcurrentDictionary<int, List<DateTime>>();

        private static readonly object _rateLimitLock = new object();



        /// <summary>

        /// Kiểm tra tốc độ gửi tin nhắn của người dùng để chống spam, chống flood và giảm tải server.

        /// Giới hạn: Tối đa 10 tin nhắn trong 5 giây.

        /// </summary>

        public static bool CheckUserRateLimit(int userId, out int retryAfterSeconds)

        {

            retryAfterSeconds = 0;

            if (userId <= 0) return true;



            var now = DateTime.UtcNow;

            var windowStart = now.AddSeconds(-5);



            var timestamps = _userMessageTimestamps.GetOrAdd(userId, _ => new List<DateTime>());

            lock (timestamps)

            {

                // Loại bỏ các mốc thời gian ngoài cửa sổ trượt 5 giây

                timestamps.RemoveAll(t => t < windowStart);



                if (timestamps.Count >= 10)

                {

                    var oldestInWindow = timestamps.First();

                    var waitTime = oldestInWindow.AddSeconds(5) - now;

                    retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(waitTime.TotalSeconds));

                    return false;

                }



                timestamps.Add(now);

                return true;

            }

        }



        /// <summary>

        /// Bộ lọc xác thực trung tâm cho toàn bộ Controller Trò chuyện.

        /// Bắt buộc người dùng phải đăng nhập hợp lệ mới được truy cập bất kỳ tài nguyên nào trong Chat.

        /// </summary>

        protected override void OnActionExecuting(ActionExecutingContext filterContext)

        {

            base.OnActionExecuting(filterContext);

            if (filterContext.Result != null) return;



            // Xác thực danh tính người dùng từ Session hoặc Cookie ký điện tử MachineKey và đối chiếu DB

            if (!UserAuthHelper.TryGetAuthenticatedUser(Session, Request, db, out User currentUser))

            {

                HandleUnauthenticated(filterContext);

                return;

            }



            // Đồng bộ Session đảm bảo an toàn nếu chưa được thiết lập

            if (Session["UserID"] == null || (int)Session["UserID"] != currentUser.UserID)

            {

                Session["UserID"] = currentUser.UserID;

            }

            if (Session["Username"] == null) Session["Username"] = currentUser.Username;

            if (Session["Avatar"] == null) Session["Avatar"] = currentUser.SafeAvatar;

            // Đảm bảo Cookie xác thực luôn đồng bộ để SignalR và ChatApi hoạt động trơn tru
            if (UserAuthHelper.GetUserIdFromCookie(Request) != currentUser.UserID)
            {
                UserAuthHelper.SetAuthCookie(Response, currentUser.UserID);
            }



            // Cập nhật LastActivityAt định kỳ nếu cần

            if (currentUser.LastActivityAt == null || (DateTime.Now - currentUser.LastActivityAt.Value).TotalMinutes > 5)

            {

                currentUser.LastActivityAt = DateTime.Now;

                db.SaveChanges();

            }

        }



        /// <summary>

        /// Xử lý từ chối truy cập khi chưa đăng nhập hoặc phiên làm việc đã hết hạn.

        /// - Với AJAX/API request: trả về mã trạng thái HTTP 401 Unauthorized kèm JSON (không trả về HTML).

        /// - Với Page request thông thường: chuyển hướng sang trang đăng nhập /User/Login kèm returnUrl.

        /// </summary>

        private void HandleUnauthenticated(ActionExecutingContext filterContext)

        {

            bool isAjax = filterContext.HttpContext.Request.IsAjaxRequest()

                || string.Equals(filterContext.HttpContext.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)

                || (filterContext.HttpContext.Request.AcceptTypes != null && filterContext.HttpContext.Request.AcceptTypes.Any(t => t.Contains("application/json")));



            string rawUrl = filterContext.HttpContext.Request.RawUrl ?? "/Chat";



            if (isAjax)

            {

                filterContext.HttpContext.Response.StatusCode = 401;

                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;

                filterContext.Result = new JsonResult

                {

                    Data = new

                    {

                        success = false,

                        requireLogin = true,

                        message = "Phiên làm việc đã hết hạn hoặc bạn chưa đăng nhập. Vui lòng đăng nhập lại!",

                        redirectUrl = "/User/Login?returnUrl=" + HttpUtility.UrlEncode(rawUrl)

                    },

                    JsonRequestBehavior = JsonRequestBehavior.AllowGet

                };

            }

            else

            {

                filterContext.Result = new RedirectToRouteResult(

                    new RouteValueDictionary(new

                    {

                        controller = "User",

                        action = "Login",

                        area = "",

                        returnUrl = rawUrl

                    }));

            }

        }



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



        public static string FormatMessagePreview(string content)

        {

            if (string.IsNullOrEmpty(content)) return "";

            if (content.StartsWith("[SHOW_ID]:")) content = content.Substring(10);

            if (content.StartsWith("[ATTACHMENTS_PAYLOAD]:"))

            {

                try

                {

                    var json = content.Substring("[ATTACHMENTS_PAYLOAD]:".Length);

                    var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatAttachmentsPayload>(json);

                    if (payload != null)

                    {

                        if (!string.IsNullOrEmpty(payload.Text)) return payload.Text;

                        if (payload.Items != null && payload.Items.Count > 0)

                        {

                            int imgCount = payload.Items.Count(i => i.IsImage);

                            int docCount = payload.Items.Count(i => !i.IsImage);

                            if (imgCount > 0 && docCount > 0) return $"[Hình ảnh & Tệp đính kèm ({payload.Items.Count})]";

                            if (imgCount > 0) return imgCount == 1 ? "[Hình ảnh]" : $"[{imgCount} Hình ảnh]";

                            return docCount == 1 ? $"[Tệp: {payload.Items[0].FileName}]" : $"[{docCount} Tệp đính kèm]";

                        }

                    }

                }

                catch { }

                return "[Tệp đính kèm]";

            }

            return content;

        }



        public static string FormatFileSize(long bytes)

        {

            if (bytes < 1024) return bytes + " B";

            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";

            return (bytes / (1024.0 * 1024.0)).ToString("0.00") + " MB";

        }



        // =========================================================================

        // 1. TRANG NHẮN TIN CHÍNH (Sảnh chung)

        // =========================================================================

        [HttpGet]

        public ActionResult Index()

        {

            int currentUserId = (int)Session["UserID"];



            TryScheduleCleanup();



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

                 .AsNoTracking()

                 .Include("User")

                 .Where(m => m.RoomID == room.RoomID)

                 .OrderByDescending(m => m.MessageID)

                 .Take(30)

                 .OrderBy(m => m.MessageID)

                 .ToList();



            var replyIds = messages.Where(m => m.ReplyToID.HasValue)

                       .Select(m => m.ReplyToID.Value)

                       .Distinct()

                       .ToList();



            Dictionary<int, string> replyMessages = new Dictionary<int, string>();

            if (replyIds.Any())

            {

                var rawReplies = db.ChatMessages

                                      .AsNoTracking()

                                      .Where(m => replyIds.Contains(m.MessageID))

                                      .Select(m => new { m.MessageID, m.Content })

                                      .ToList();



                replyMessages = rawReplies

                                      .ToDictionary(m => m.MessageID, m => FormatMessagePreview(m.Content));

            }



            ViewBag.ReplyMessages = replyMessages;

            return View(messages);

        }



        // Dọn dẹp phòng không hoạt động

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



                if (oldRoomIds.Any())

                {

                    var msgs = context.ChatMessages.Where(m => oldRoomIds.Contains(m.ChatRoom.RoomID));

                    context.ChatMessages.RemoveRange(msgs);



                    var parts = context.ChatParticipants.Where(p => oldRoomIds.Contains(p.ChatRoom.RoomID));

                    context.ChatParticipants.RemoveRange(parts);



                    var rooms = context.ChatRooms.Where(r => oldRoomIds.Contains(r.RoomID));

                    context.ChatRooms.RemoveRange(rooms);



                    context.SaveChanges();

                }

            }

        }



        /// <summary>

        /// Kiểm tra xem người dùng có đang bị cấm hoặc hạn chế chat hay không.

        /// </summary>

        private bool IsUserChatRestricted(int userId, out string blockMessage, out string restrictionType)
        {
            blockMessage = null;
            restrictionType = null;
            if (userId <= 0) return false;

            try
            {
                var now = DateTime.Now;
                var activeRestriction = db.ChatRestrictions
                    .Where(r => r.UserID == userId && r.IsActive)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();

                if (activeRestriction == null) return false;

                if (activeRestriction.ExpiresAt.HasValue && activeRestriction.ExpiresAt.Value <= now)
                {
                    activeRestriction.IsActive = false;
                    activeRestriction.RevokedAt = now;
                    activeRestriction.RevokeReason = "Tự động mở cấm do đã hết thời hạn phạt";
                    db.SaveChanges();
                    return false;
                }

                restrictionType = activeRestriction.RestrictionType;
                string timeInfo = activeRestriction.ExpiresAt.HasValue
                    ? $"đến {activeRestriction.ExpiresAt.Value:HH:mm dd/MM/yyyy}"
                    : "vĩnh viễn";

                string typeName = restrictionType == "Mute" ? "tạm khóa tính năng gửi tin nhắn" : "cấm tham gia trò chuyện";
                string reasonInfo = string.IsNullOrWhiteSpace(activeRestriction.Reason) ? "Vi phạm quy tắc cộng đồng" : activeRestriction.Reason;

                blockMessage = $"Tài khoản của bạn đang bị {typeName} ({timeInfo}). Lý do: {reasonInfo}.";
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("[IsUserChatRestricted] Error checking restriction: " + ex.Message);
                return false;
            }
        }



        // =========================================================================

        // 2. GỬI TIN NHẮN VĂN BẢN

        // =========================================================================

                // =========================================================================
        // PHƯƠNG THỨC TIỆN ÍCH BROADCAST TIN NHẮN REALTIME (XỬ LÝ CHUẨN ẨN DANH)
        // =========================================================================
        private void BroadcastChatMessage(int roomId, int senderId, int messageId, string content, string imageUrl, int? replyToId, string replyContent, bool isAnonymous, string displaySenderName, string displaySenderAvatar)
        {
            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
            if (isAnonymous)
            {
                // 1. Gửi riêng cho chính người gửi (để hiển thị bên phải với senderId thực tế của họ)
                hubContext.Clients.Group("user_" + senderId)
                          .addNewMessageToPage(senderId, content, imageUrl, messageId, replyToId, replyContent, true, displaySenderName, displaySenderAvatar);

                // 2. Gửi cho những người khác trong phòng (LOẠI TRỪ tất cả ConnectionId của người gửi) với senderId = 0 để bảo mật danh tính tuyệt đối
                var senderConns = ChatHub.GetUserConnections(senderId, roomId);
                if (senderConns != null && senderConns.Length > 0)
                {
                    hubContext.Clients.Group(roomId.ToString(), senderConns)
                              .addNewMessageToPage(0, content, imageUrl, messageId, replyToId, replyContent, true, displaySenderName, displaySenderAvatar);
                }
                else
                {
                    hubContext.Clients.Group(roomId.ToString())
                              .addNewMessageToPage(0, content, imageUrl, messageId, replyToId, replyContent, true, displaySenderName, displaySenderAvatar);
                }
            }
            else
            {
                hubContext.Clients.Group(roomId.ToString())
                          .addNewMessageToPage(senderId, content, imageUrl, messageId, replyToId, replyContent, false, displaySenderName, displaySenderAvatar);
            }
            hubContext.Clients.Group(roomId.ToString()).triggerReloadChatList(roomId);
        }

        [HttpPost]

        public JsonResult SendMessage(int roomId, string content, int? replyToId = null, bool isAnonymous = true)

        {

            int myId = (int)Session["UserID"];



            if (!CheckUserRateLimit(myId, out int retryAfter))

            {

                return Json(new { success = false, isRateLimited = true, message = $"Bạn đang gửi tin nhắn quá nhanh. Vui lòng chờ {retryAfter} giây." });

            }



            if (string.IsNullOrWhiteSpace(content))

                return Json(new { success = false, message = "Nội dung không được để trống." });



            if (content.Length > 2000)

                return Json(new { success = false, message = "Tin nhắn quá dài. Tối đa 2000 ký tự." });



            if (IsUserChatRestricted(myId, out string blockMsg, out string resType))

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, isRestricted = true, message = blockMsg });

            }



            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

            {

                return Json(new { success = false, message = "Phòng trò chuyện không tồn tại hoặc đã đóng." });

            }



            bool isMember = room.RoomType == "Public" ||

                            db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

            if (!isMember)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Bạn không có quyền gửi tin vào cuộc trò chuyện này." });

            }



            // Kiểm duyệt từ ngữ nhạy cảm / xúc phạm qua WordModerationHelper

            var modResult = WordModerationHelper.Moderate(content);

            string cleanContent = modResult.CleanText;



            // Xử lý chế độ ẩn danh

            var me = db.Users.Find(myId);

            string myName = me?.DisplayName ?? me?.Username ?? "Sinh viên";

            string myAvatar = me?.Avatar;



            string displaySenderName = isAnonymous ? "Ẩn danh" : myName;

            string displaySenderAvatar = isAnonymous ? null : myAvatar;

            string storedContent = isAnonymous ? cleanContent : "[SHOW_ID]:" + cleanContent;



            string replyContent = null;

            if (replyToId.HasValue)

            {

                var replyMsg = db.ChatMessages.Find(replyToId.Value);

                if (replyMsg != null)

                {

                    replyContent = replyMsg.Content != null && replyMsg.Content.StartsWith("[SHOW_ID]:")

                        ? replyMsg.Content.Substring(10)

                        : replyMsg.Content;

                }

            }



            var msg = new ChatMessage

            {

                RoomID = roomId,

                SenderID = myId,

                Content = storedContent,

                SentAt = DateTime.Now,

                ReplyToID = replyToId

            };

            db.ChatMessages.Add(msg);

            db.SaveChanges();



            BroadcastChatMessage(roomId, myId, msg.MessageID, cleanContent, null, replyToId, replyContent, isAnonymous, displaySenderName, displaySenderAvatar);



            return Json(new

            {

                success = true,

                messageId = msg.MessageID,

                censored = modResult.HasViolation,

                violations = modResult.Violations,

                message = modResult.HasViolation ? "Tin nhắn đã được tự động kiểm duyệt các từ ngữ nhạy cảm." : null

            });

        }



        // =========================================================================

        // 3. GỬI ẢNH ĐÍNH KÈM

        // =========================================================================

        [HttpPost]

        public async Task<JsonResult> SendImage(int roomId, HttpPostedFileBase file, bool isAnonymous = true)

        {

            int myId = (int)Session["UserID"];



            if (!CheckUserRateLimit(myId, out int retryAfterImg))

            {

                return Json(new { success = false, isRateLimited = true, message = $"Bạn đang gửi thao tác quá nhanh. Vui lòng chờ {retryAfterImg} giây." });

            }



            if (file == null || file.ContentLength <= 0)

                return Json(new { success = false, message = "File ảnh không hợp lệ hoặc rỗng." });



            if (IsUserChatRestricted(myId, out string blockMsgImg, out string resTypeImg))

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, isRestricted = true, message = blockMsgImg });

            }



            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

                return Json(new { success = false, message = "Phòng không tồn tại." });



            bool isMember = room.RoomType == "Public" ||

                            db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

            if (!isMember)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Không có quyền gửi tin vào phòng này." });

            }



            var me = db.Users.Find(myId);

            string myName = me?.DisplayName ?? me?.Username ?? "Sinh viên";

            string myAvatar = me?.Avatar;



            string tempInspectionDir = Server.MapPath("~/App_Data/TempFileInspection");

            if (!Directory.Exists(tempInspectionDir))

                Directory.CreateDirectory(tempInspectionDir);

            ChatFileModerationHelper.CleanOrphanedTempFiles(tempInspectionDir);



            string safeBaseName = ChatFileModerationHelper.SanitizeFileName(file.FileName);

            string tempFileName = $"{Guid.NewGuid():N}_{safeBaseName}";

            string tempFilePath = Path.Combine(tempInspectionDir, tempFileName);



            try

            {

                // Bước 1: Lưu tạm vào thư mục cách ly (Staging / Quarantine)

                file.SaveAs(tempFilePath);



                // Bước 2: Thực hiện chu trình kiểm duyệt toàn diện bằng các lớp bảo mật cục bộ (Chữ ký Magic Bytes, Antivirus, GDI+, Archive Inspection)

                var modResult = await ChatFileModerationHelper.ValidateAndModerateFileAsync(

                    tempFilePath,

                    file.FileName,

                    file.ContentType,

                    true,

                    myId,

                    myName,

                    roomId

                );



                if (!modResult.IsApproved)

                {

                    return Json(new { success = false, message = modResult.UserFriendlyMessage });

                }



                // Bước 3: Đã duyệt an toàn -> Chuyển vào thư mục lưu trữ vĩnh viễn

                string chatUploadsDir = Server.MapPath("~/Content/Uploads/Chat");

                if (!Directory.Exists(chatUploadsDir))

                    Directory.CreateDirectory(chatUploadsDir);



                string ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";

                string permanentKey = $"{Guid.NewGuid():N}{ext}";

                string permanentPath = Path.Combine(chatUploadsDir, permanentKey);



                System.IO.File.Move(tempFilePath, permanentPath);



                string relativePath = "/Content/Uploads/Chat/" + permanentKey;

                string displaySenderName = isAnonymous ? "Ẩn danh" : myName;

                string displaySenderAvatar = isAnonymous ? null : myAvatar;

                string storedContent = isAnonymous ? "[Hình ảnh]" : "[SHOW_ID]:[Hình ảnh]";



                var msg = new ChatMessage

                {

                    RoomID = roomId,

                    SenderID = myId,

                    Content = storedContent,

                    ImageUrl = relativePath,

                    SentAt = DateTime.Now

                };

                db.ChatMessages.Add(msg);

                db.SaveChanges();



                BroadcastChatMessage(roomId, myId, msg.MessageID, "[Hình ảnh]", relativePath, null, null, isAnonymous, displaySenderName, displaySenderAvatar);



                return Json(new { success = true, imgUrl = relativePath, messageId = msg.MessageID });

            }

            catch (Exception ex)

            {

                try { if (System.IO.File.Exists(tempFilePath)) System.IO.File.Delete(tempFilePath); } catch { }

                return Json(new { success = false, message = "Lỗi khi xử lý ảnh: " + ex.Message });

            }

        }



        // =========================================================================

        // 4. GỬI TIN NHẮN KÈM NHIỀU FILE / ẢNH (KIỂM DUYỆT TẤT CẢ TỆP ĐÍNH KÈM)

        // =========================================================================

        [HttpPost]

        [ValidateAntiForgeryToken]

        public async Task<JsonResult> SendMessageWithFiles(int roomId, string content, List<HttpPostedFileBase> files, int? replyToId = null, bool isAnonymous = true)

        {

            int myId = (int)Session["UserID"];



            if (!CheckUserRateLimit(myId, out int retryAfterFiles))

            {

                return Json(new { success = false, isRateLimited = true, message = $"Bạn đang gửi thao tác quá nhanh. Vui lòng chờ {retryAfterFiles} giây." });

            }



            if (IsUserChatRestricted(myId, out string blockMsgFiles, out string resTypeFiles))

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, isRestricted = true, message = blockMsgFiles });

            }



            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

                return Json(new { success = false, message = "Phòng không tồn tại." });



            bool isMember = room.RoomType == "Public" ||

                            db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

            if (!isMember)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Không có quyền gửi tin vào phòng này." });

            }



            var validFiles = (files ?? new List<HttpPostedFileBase>()).Where(f => f != null && f.ContentLength > 0).ToList();

            string trimmedContent = (content ?? "").Trim();



            if (string.IsNullOrEmpty(trimmedContent) && validFiles.Count == 0)

                return Json(new { success = false, message = "Vui lòng nhập tin nhắn hoặc chọn tệp đính kèm." });



            if (validFiles.Count > ChatFileModerationHelper.MaxBatchFileCount)

                return Json(new { success = false, message = $"Mỗi tin nhắn chỉ được gửi tối đa {ChatFileModerationHelper.MaxBatchFileCount} tệp." });



            long totalSize = validFiles.Sum(f => (long)f.ContentLength);

            if (totalSize > ChatFileModerationHelper.MaxBatchUploadSizeBytes)

                return Json(new { success = false, message = "Tổng dung lượng các tệp không được vượt quá 25MB." });



            var me = db.Users.Find(myId);

            string myName = me?.DisplayName ?? me?.Username ?? "Sinh viên";

            string myAvatar = me?.Avatar;



            string tempInspectionDir = Server.MapPath("~/App_Data/TempFileInspection");

            if (!Directory.Exists(tempInspectionDir))

                Directory.CreateDirectory(tempInspectionDir);

            ChatFileModerationHelper.CleanOrphanedTempFiles(tempInspectionDir);



            string chatUploadsDir = Server.MapPath("~/Content/Uploads/Chat");

            if (!Directory.Exists(chatUploadsDir))

                Directory.CreateDirectory(chatUploadsDir);



            string secureAttachmentsDir = Server.MapPath("~/App_Data/ChatAttachments");

            if (!Directory.Exists(secureAttachmentsDir))

                Directory.CreateDirectory(secureAttachmentsDir);



            ChatFileModerationHelper.CleanOrphanedTempFiles(tempInspectionDir);



            var stagedPaths = new List<string>();

            var stagedItems = new List<Tuple<HttpPostedFileBase, string, string, bool, string>>();

            var attachmentItems = new List<ChatAttachmentItem>();



            try

            {

                // Bước 1: Lưu tạm và kiểm duyệt nghiêm ngặt từng tệp

                // Nguyên tắc nguyên khối (Atomic Transaction): Nếu BẤT KỲ tệp nào vi phạm, hủy bỏ toàn bộ và xóa sạch

                foreach (var file in validFiles)

                {

                    string safeBaseName = ChatFileModerationHelper.SanitizeFileName(file.FileName);

                    string tempFileName = $"{Guid.NewGuid():N}_{safeBaseName}";

                    string tempFilePath = Path.Combine(tempInspectionDir, tempFileName);



                    file.SaveAs(tempFilePath);

                    stagedPaths.Add(tempFilePath);



                    string ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";

                    bool isImage = ChatFileModerationHelper.AllowedImageExtensions.Contains(ext);



                    var modResult = await ChatFileModerationHelper.ValidateAndModerateFileAsync(

                        tempFilePath,

                        file.FileName,

                        file.ContentType,

                        isImage,

                        myId,

                        myName,

                        roomId

                    );



                    if (!modResult.IsApproved)

                    {

                        // Hủy bỏ và dọn dẹp sạch toàn bộ các tệp đã upload trong lượt này

                        foreach (var path in stagedPaths)

                        {

                            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }

                        }

                        return Json(new { success = false, message = modResult.UserFriendlyMessage });

                    }



                    stagedItems.Add(Tuple.Create(file, tempFilePath, ext, isImage, safeBaseName));

                }



                // Bước 2: TẤT CẢ các tệp trong lượt đều được duyệt an toàn -> chuyển sang thư mục đích vĩnh viễn

                foreach (var item in stagedItems)

                {

                    var file = item.Item1;

                    var tempFilePath = item.Item2;

                    var ext = item.Item3;

                    var isImage = item.Item4;

                    var safeBaseName = item.Item5;



                    string uniqueKey = $"{Guid.NewGuid():N}_{safeBaseName}";

                    if (isImage)

                    {

                        string permPath = Path.Combine(chatUploadsDir, uniqueKey);

                        System.IO.File.Move(tempFilePath, permPath);



                        attachmentItems.Add(new ChatAttachmentItem

                        {

                            FileName = file.FileName,

                            Extension = ext,

                            FileSize = file.ContentLength,

                            FormattedSize = FormatFileSize(file.ContentLength),

                            IsImage = true,

                            Url = "/Content/Uploads/Chat/" + uniqueKey,

                            FileKey = uniqueKey,

                            ContentType = file.ContentType

                        });

                    }

                    else

                    {

                        string permPath = Path.Combine(secureAttachmentsDir, uniqueKey);

                        System.IO.File.Move(tempFilePath, permPath);



                        attachmentItems.Add(new ChatAttachmentItem

                        {

                            FileName = file.FileName,

                            Extension = ext,

                            FileSize = file.ContentLength,

                            FormattedSize = FormatFileSize(file.ContentLength),

                            IsImage = false,

                            Url = null,

                            FileKey = uniqueKey,

                            ContentType = file.ContentType

                        });

                    }

                }



                // Bước 2: Kiểm duyệt ngôn từ nội dung chữ nếu có

                string cleanContent = "";

                bool hasViolation = false;

                List<string> violations = new List<string>();



                if (!string.IsNullOrEmpty(trimmedContent))

                {

                    var modResult = WordModerationHelper.Moderate(trimmedContent);

                    cleanContent = modResult.CleanText;

                    hasViolation = modResult.HasViolation;

                    violations = modResult.Violations;

                }



                string replySnippet = null;

                if (replyToId.HasValue)

                {

                    var replyMsg = db.ChatMessages.Find(replyToId.Value);

                    if (replyMsg != null)

                    {

                        replySnippet = FormatMessagePreview(replyMsg.Content);

                        if (replySnippet.Length > 80)

                            replySnippet = replySnippet.Substring(0, 80) + "...";

                    }

                }



                var payload = new ChatAttachmentsPayload

                {

                    Text = cleanContent,

                    Items = attachmentItems

                };



                string payloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

                string storedContent = (isAnonymous ? "" : "[SHOW_ID]:") + "[ATTACHMENTS_PAYLOAD]:" + payloadJson;

                string firstImgUrl = attachmentItems.FirstOrDefault(i => i.IsImage)?.Url;



                var msg = new ChatMessage

                {

                    RoomID = roomId,

                    SenderID = myId,

                    Content = storedContent,

                    ImageUrl = firstImgUrl,

                    SentAt = DateTime.Now,

                    ReplyToID = replyToId

                };

                db.ChatMessages.Add(msg);

                db.SaveChanges();



                string displaySenderName = isAnonymous ? "Ẩn danh" : myName;

                string displaySenderAvatar = isAnonymous ? null : myAvatar;



                BroadcastChatMessage(roomId, myId, msg.MessageID, "[ATTACHMENTS_PAYLOAD]:" + payloadJson, firstImgUrl, replyToId, replySnippet, isAnonymous, displaySenderName, displaySenderAvatar);



                return Json(new

                {

                    success = true,

                    messageId = msg.MessageID,

                    censored = hasViolation,

                    violations = violations,

                    message = hasViolation ? "Tin nhắn đã được tự động kiểm duyệt các từ ngữ nhạy cảm." : null

                });

            }

            catch (Exception ex)

            {

                foreach (var path in stagedPaths)

                {

                    try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }

                }

                return Json(new { success = false, message = "Lỗi khi xử lý file: " + ex.Message });

            }

        }



        // =========================================================================

        // 5. TẢI TỆP ĐÍNH KÈM AN TOÀN (BẮT BUỘC ĐĂNG NHẬP, QUYỀN THÀNH VIÊN, CHẶN FILE THU HỒI/ẨN)

        // =========================================================================

        [HttpGet]

        public ActionResult DownloadAttachment(int messageId, string fileKey)

        {

            if (Session["UserID"] == null)

            {

                return new HttpStatusCodeResult(401, "Yêu cầu đăng nhập để tải tệp tin.");

            }

            int myId = (int)Session["UserID"];



            var msg = db.ChatMessages.Find(messageId);

            if (msg == null)

            {

                return HttpNotFound("Không tìm thấy tin nhắn chứa tệp này.");

            }



            // Chặn tải xuống nếu tin nhắn đã bị thu hồi hoặc đã bị Admin ẩn kiểm duyệt

            if (msg.Content == "Tin nhắn đã được thu hồi")

            {

                return new HttpStatusCodeResult(403, "Tin nhắn đã được thu hồi. Tệp đính kèm không còn khả dụng.");

            }

            if (msg.IsHidden == true)

            {

                return new HttpStatusCodeResult(403, "Tin nhắn chứa tệp này đã bị ban quản trị ẩn do vi phạm quy chuẩn cộng đồng.");

            }



            int roomId = msg.RoomID ?? 0;

            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

            {

                return HttpNotFound("Không tìm thấy cuộc trò chuyện.");

            }



            bool isMember = room.RoomType == "Public" ||

                            db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

            if (!isMember)

            {

                return new HttpStatusCodeResult(403, "Bạn không có quyền tải tệp từ cuộc trò chuyện này.");

            }



            if (string.IsNullOrWhiteSpace(fileKey) || fileKey.Contains("..") || fileKey.Contains("/") || fileKey.Contains("\\"))

            {

                return new HttpStatusCodeResult(400, "Tên file không hợp lệ (Phát hiện đường dẫn bất thường).");

            }



            string secureDir = Server.MapPath("~/App_Data/ChatAttachments");

            string fullPath = System.IO.Path.Combine(secureDir, fileKey);



            if (!System.IO.File.Exists(fullPath))

            {

                string imgDir = Server.MapPath("~/Content/Uploads/Chat");

                string imgPath = System.IO.Path.Combine(imgDir, fileKey);

                if (System.IO.File.Exists(imgPath))

                {

                    fullPath = imgPath;

                }

                else

                {

                    return HttpNotFound("Tệp đính kèm không tồn tại trên hệ thống hoặc đã bị xóa.");

                }

            }



            // Xác thực nghiêm ngặt quyền sở hữu tệp đối với tin nhắn cụ thể (Chống tấn công IDOR / Cross-Message Parameter Tampering)

            bool fileBelongsToMessage = false;

            string downloadFileName = System.IO.Path.GetFileName(fullPath);



            if (!string.IsNullOrEmpty(msg.ImageUrl))

            {

                string imgFileName = System.IO.Path.GetFileName(msg.ImageUrl);

                if (string.Equals(imgFileName, fileKey, StringComparison.OrdinalIgnoreCase) ||

                    msg.ImageUrl.EndsWith("/" + fileKey, StringComparison.OrdinalIgnoreCase))

                {

                    fileBelongsToMessage = true;

                }

            }



            try

            {

                string rawContent = msg.Content ?? "";

                if (rawContent.StartsWith("[SHOW_ID]:")) rawContent = rawContent.Substring(10);

                if (rawContent.StartsWith("[ATTACHMENTS_PAYLOAD]:"))

                {

                    var json = rawContent.Substring("[ATTACHMENTS_PAYLOAD]:".Length);

                    var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatAttachmentsPayload>(json);

                    var item = payload?.Items?.FirstOrDefault(i => string.Equals(i.FileKey, fileKey, StringComparison.OrdinalIgnoreCase));

                    if (item != null)

                    {

                        fileBelongsToMessage = true;

                        if (!string.IsNullOrEmpty(item.FileName))

                        {

                            downloadFileName = item.FileName;

                        }

                    }

                }

            }

            catch { }



            if (!fileBelongsToMessage)

            {

                return new HttpStatusCodeResult(403, "Tệp đính kèm không thuộc về tin nhắn này.");

            }



            string contentType = MimeMapping.GetMimeMapping(downloadFileName);



            // Gắn bảo mật HTTP response headers chống MIME-sniffing và sandbox script execution

            Response.Headers["X-Content-Type-Options"] = "nosniff";

            Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";



            return File(fullPath, contentType, downloadFileName);

        }



        // =========================================================================

        [HttpPost]

        public JsonResult RevokeMessage(int messageId)

        {

            int myId = (int)Session["UserID"];

            var msg = db.ChatMessages.Find(messageId);



            if (msg == null || msg.SenderID != myId)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Bạn không có quyền thu hồi tin nhắn này." });

            }



            if (!msg.SentAt.HasValue)

            {

                return Json(new { success = false, message = "Không xác định thời gian gửi tin nhắn, không thể thu hồi." });

            }



            if ((DateTime.Now - msg.SentAt.Value).TotalMinutes > 10)

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



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult DeleteMessage(int messageId)

        {

            int myId = (int)Session["UserID"];

            var msg = db.ChatMessages.Find(messageId);



            if (msg == null || msg.SenderID != myId)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Bạn không có quyền xóa tin nhắn này." });

            }



            if (!msg.SentAt.HasValue)

            {

                return Json(new { success = false, message = "Không xác định thời gian gửi tin nhắn, không thể xóa." });

            }



            if ((DateTime.Now - msg.SentAt.Value).TotalMinutes > 5)

            {

                return Json(new { success = false, message = "Chỉ có thể xóa tin nhắn trong vòng 5 phút kể từ khi gửi." });

            }



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



        public string GetRoomDisplayName(ChatRoom room, int myId, Dictionary<int, string> customRoomNames = null)

        {

            if (room == null) return "Trò chuyện";



            if (!string.IsNullOrWhiteSpace(room.RoomName))

            {

                return room.RoomName;

            }



            if (customRoomNames != null && customRoomNames.TryGetValue(room.RoomID, out var preloadedName))

            {

                return preloadedName;

            }



            if (customRoomNames == null)

            {

                var renameMsg = db.ChatMessages.AsNoTracking()

                                  .Where(m => m.RoomID == room.RoomID && m.Content.StartsWith("[SYSTEM_RENAME]"))

                                  .OrderByDescending(m => m.MessageID)

                                  .FirstOrDefault();



                if (renameMsg != null)

                {

                    string customName = renameMsg.Content.Substring("[SYSTEM_RENAME]".Length).Trim();

                    if (!string.IsNullOrEmpty(customName)) return customName;

                }

            }



            if (room.RoomType == "1vs1" || room.RoomType == "Anonymous")

            {

                var otherPart = room.ChatParticipants != null

                    ? room.ChatParticipants.FirstOrDefault(p => p.UserID != myId)

                    : db.ChatParticipants.Include("User").FirstOrDefault(p => p.RoomID == room.RoomID && p.UserID != myId);



                return otherPart?.User?.Username ?? (room.RoomType == "Anonymous" ? "Người lạ Ẩn danh" : "Bạn học");

            }

            else

            {

                return room.RoomName ?? (room.RoomType == "RandomGroup" ? "Nhóm 5 Ngẫu nhiên" : "Nhóm trò chuyện #" + room.RoomID);

            }

        }



        // =========================================================================

        // 7. TẢI DANH SÁCH PHÒNG CHAT (Tối ưu truy vấn gom cụm không N+1)

        // =========================================================================

        [HttpGet]

        public ActionResult GetChatList(int? currentRoomId = null)
        {
            int myId = (int)Session["UserID"];

            // 1. Gom nhóm trong 1 truy vấn duy nhất (AsNoTracking): Lấy phòng đang active mà user tham gia
            var rooms = db.ChatRooms.AsNoTracking()
                          .Include("ChatParticipants")
                          .Include("ChatParticipants.User")
                          .Where(r => r.IsActive == true && r.ChatParticipants.Any(p => p.UserID == myId))
                          .ToList();

            if (!rooms.Any())
            {
                return PartialView("_ChatListPartial", new List<ChatListItemViewModel>());
            }

            var activeRoomIds = rooms.Select(r => r.RoomID).ToList();

            // 2. Truy vấn gom cụm batch: Lấy ID tin nhắn mới nhất của từng phòng theo index
            var latestMessageIds = db.ChatMessages.AsNoTracking()
                .Where(m => m.RoomID.HasValue && activeRoomIds.Contains(m.RoomID.Value)
                    && !m.Content.StartsWith("[SYSTEM_RENAME]")
                    && !m.Content.StartsWith("[SYSTEM_NOTICE_RENAME]"))
                .GroupBy(m => m.RoomID.Value)
                .Select(g => g.Max(m => m.MessageID))
                .ToList();

            var latestMessages = new Dictionary<int, ChatMessage>();
            if (latestMessageIds.Any())
            {
                latestMessages = db.ChatMessages.AsNoTracking()
                    .Where(m => latestMessageIds.Contains(m.MessageID))
                    .ToDictionary(m => m.RoomID.Value);
            }

            // 3. Chỉ tìm tên đổi cho phòng NHÓM (không phải 1vs1, không phải Anonymous) có RoomName trống
            var roomsNeedingName = rooms
                .Where(r => r.RoomType != "1vs1" && r.RoomType != "Anonymous" && string.IsNullOrWhiteSpace(r.RoomName))
                .Select(r => r.RoomID)
                .ToList();

            var customRoomNames = new Dictionary<int, string>();
            if (roomsNeedingName.Any())
            {
                var renameMsgIds = db.ChatMessages.AsNoTracking()
                    .Where(m => m.RoomID.HasValue && roomsNeedingName.Contains(m.RoomID.Value) && m.Content.StartsWith("[SYSTEM_RENAME]"))
                    .GroupBy(m => m.RoomID.Value)
                    .Select(g => g.Max(m => m.MessageID))
                    .ToList();

                if (renameMsgIds.Any())
                {
                    var renameList = db.ChatMessages.AsNoTracking()
                        .Where(m => renameMsgIds.Contains(m.MessageID))
                        .Select(m => new { RoomID = m.RoomID.Value, m.Content })
                        .ToList();

                    foreach (var rm in renameList)
                    {
                        string name = rm.Content.Substring("[SYSTEM_RENAME]".Length).Trim();
                        if (!string.IsNullOrEmpty(name))
                            customRoomNames[rm.RoomID] = name;
                    }
                }
            }

            var result = new List<ChatListItemViewModel>();

            foreach (var room in rooms)
            {
                ChatMessage lastMsg = null;
                latestMessages.TryGetValue(room.RoomID, out lastMsg);

                string resolvedName = GetRoomDisplayName(room, myId, customRoomNames);

                string lastMsgText = "Bắt đầu cuộc trò chuyện...";
                if (lastMsg != null)
                {
                    if (!string.IsNullOrEmpty(lastMsg.ImageUrl))
                    {
                        lastMsgText = "[Hình ảnh]";
                    }
                    else if (!string.IsNullOrEmpty(lastMsg.Content))
                    {
                        lastMsgText = FormatMessagePreview(lastMsg.Content);
                    }
                }

                var item = new ChatListItemViewModel
                {
                    RoomID = room.RoomID,
                    RoomType = room.RoomType,
                    RoomName = resolvedName,
                    LastMsgAt = lastMsg?.SentAt ?? room.CreatedAt,
                    LastMessage = lastMsgText,
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



        // =========================================================================

        // 8. ĐỔI TÊN PHÒNG CHAT

        // =========================================================================

        [HttpPost]

        public JsonResult RenameRoom(int roomId, string newName)

        {

            if (string.IsNullOrWhiteSpace(newName))

                return Json(new { success = false, message = "Tên cuộc trò chuyện không được để trống." });



            int myId = (int)Session["UserID"];

            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

                return Json(new { success = false, message = "Phòng chat không tồn tại." });



            bool isMember = db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

            if (!isMember)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Bạn không thuộc cuộc trò chuyện này." });

            }



            string cleanName = newName.Trim();

            if (cleanName.Length > 60) cleanName = cleanName.Substring(0, 60);



            room.RoomName = cleanName;



            var renameMsg = new ChatMessage

            {

                RoomID = roomId,

                SenderID = myId,

                Content = "[SYSTEM_RENAME]" + cleanName,

                SentAt = DateTime.Now

            };

            db.ChatMessages.Add(renameMsg);



            string renameSystemText = $"Cuộc trò chuyện được đổi tên thành: \"{cleanName}\"";

            var notifyMsg = new ChatMessage

            {

                RoomID = roomId,

                SenderID = null,

                Content = "[SYSTEM_NOTICE_RENAME]" + renameSystemText,

                SentAt = DateTime.Now.AddMilliseconds(5)

            };

            db.ChatMessages.Add(notifyMsg);

            db.SaveChanges();

            ChatHub.InvalidateRoomCache(roomId);

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();

            hubContext.Clients.Group(roomId.ToString()).addNewMessageToPage(0, renameSystemText, null, notifyMsg.MessageID);

            hubContext.Clients.Group(roomId.ToString()).onRoomRenamed(roomId, cleanName);

            hubContext.Clients.All.triggerReloadChatList(roomId);



            return Json(new { success = true, newName = cleanName });

        }



        // =========================================================================

        // =========================================================================

        // 9. TỐ CÁO VI PHẠM

        // =========================================================================

        [HttpPost]

        public JsonResult ReportViolation(int roomId, int? reportedUserId, int? messageId, string reason, string details)

        {

            int reporterId = (int)Session["UserID"];

            string reporterName = Session["Username"]?.ToString() ?? "Sinh viên";



            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

                return Json(new { success = false, message = "Phòng chat không tồn tại." });



            bool isMember = room.RoomType == "Public" || db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == reporterId);

            if (!isMember)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Bạn không thuộc cuộc trò chuyện này." });

            }



            int? verifiedReportedUserId = null;

            string violatingContent = null;

            string messageSnippet = "";

            ChatMessage targetMsg = null;



            // 1. Xác thực nguồn danh tính: Nếu tố cáo theo MessageID, lấy UserID thật từ ChatMessages của Server

            // Tuyệt đối không nhận danh tính do client tự gửi làm nguồn xác thực

            if (messageId.HasValue && messageId.Value > 0)

            {

                targetMsg = db.ChatMessages.Find(messageId.Value);

                if (targetMsg != null && targetMsg.RoomID == roomId)

                {

                    violatingContent = targetMsg.Content ?? "";

                    string displayContent = violatingContent.StartsWith("[SHOW_ID]:")

                        ? violatingContent.Substring(10)

                        : violatingContent;



                    messageSnippet = $" | Nội dung: \"{(displayContent.Length > 80 ? displayContent.Substring(0, 80) + "..." : displayContent)}\"";



                    // UserID thật của người gửi luôn được lưu trong trường SenderID phía server

                    if (targetMsg.SenderID.HasValue && targetMsg.SenderID.Value > 0)

                    {

                        verifiedReportedUserId = targetMsg.SenderID.Value;

                    }

                }

            }



            // 2. Nếu tố cáo không kèm MessageID (tố cáo từ menu phòng chat hoặc phòng 1vs1/Anonymous)

            if (!verifiedReportedUserId.HasValue)

            {

                if (room.RoomType == "1vs1" || room.RoomType == "Anonymous")

                {

                    // Lấy thành viên đối diện trong phòng chat

                    var otherParticipant = db.ChatParticipants.FirstOrDefault(p => p.RoomID == roomId && p.UserID != reporterId);

                    if (otherParticipant != null)

                    {

                        verifiedReportedUserId = otherParticipant.UserID;

                    }

                }

                else if (reportedUserId.HasValue && reportedUserId.Value > 0)

                {

                    // Xác thực người dùng này thực sự có tham gia trong phòng

                    bool isReportedInRoom = db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == reportedUserId.Value);

                    if (isReportedInRoom)

                    {

                        verifiedReportedUserId = reportedUserId.Value;

                    }

                }

            }



            // Ngăn chặn tự tố cáo chính mình

            if (verifiedReportedUserId.HasValue && verifiedReportedUserId.Value == reporterId)

            {

                return Json(new { success = false, message = "Bạn không thể tự tố cáo chính mình." });

            }



            string alertMessage = $"🚨 BÁO CÁO VI PHẠM: [{reporterName}] đã tố cáo vi phạm trong phòng #{roomId}. Lý do: {reason}. Chi tiết: {details ?? "Không có mô tả"}{messageSnippet}";

            string roomUrl = (room.RoomType == "Public" ? "/Chat/Index" : $"/Chat/Private/{roomId}")

                + $"?reportedUserId={verifiedReportedUserId}&reporterId={reporterId}&messageId={messageId}&roomId={roomId}";



            DateTime threshold = DateTime.Now.AddSeconds(-30);

            bool isDuplicate = db.Notifications.Any(n => n.Type == "report" && n.Url == roomUrl && n.CreatedAt > threshold);

            if (isDuplicate)

            {

                return Json(new { success = true, message = "Báo cáo của bạn đã được tiếp nhận, vui lòng không gửi lặp lại." });

            }



            NotifyAdmins("report", alertMessage, roomUrl);



            try

            {

                var rep = new ChatReport

                {

                    ReporterID = reporterId,

                    ReportedUserID = verifiedReportedUserId,

                    RoomID = roomId > 0 ? (int?)roomId : null,

                    MessageID = (targetMsg != null) ? (int?)targetMsg.MessageID : null,

                    Reason = string.IsNullOrWhiteSpace(reason) ? "Vi phạm quy tắc trò chuyện" : reason.Trim(),

                    Details = details?.Trim(),

                    EvidenceSnippet = string.IsNullOrWhiteSpace(violatingContent) ? null : violatingContent,

                    Status = "Pending",

                    CreatedAt = DateTime.Now

                };

                db.ChatReports.Add(rep);

                db.SaveChanges();

            }

            catch (Exception ex)

            {

                System.Diagnostics.Trace.TraceError("Failed to save ChatReport: " + ex.Message);

            }



            return Json(new { success = true, message = "Báo cáo của bạn đã được gửi đến ban quản trị để xem xét xử lý." });

        }



        // =========================================================================

        // 10. XÓA CUỘC TRÒ CHUYỆN

        // =========================================================================

        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult DeleteConversation(int roomId)

        {

            int myId = (int)Session["UserID"];

            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

                return Json(new { success = false, message = "Phòng không tồn tại." });



            if (room.RoomType == "Public")

                return Json(new { success = false, message = "Không thể xóa sảnh chung." });



            var participant = db.ChatParticipants.FirstOrDefault(p => p.RoomID == roomId && p.UserID == myId);

            if (participant == null)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Bạn không thuộc cuộc trò chuyện này." });

            }



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

            if (remaining == 0)
            {
                ChatHub.EvictRoomConnections(roomId, "Cuộc trò chuyện đã bị xóa.");
            }
            else
            {
                ChatHub.EvictUserFromRoom(myId, roomId, "Bạn đã xóa cuộc trò chuyện này.");
            }

            var hubContextAll = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();

            hubContextAll.Clients.All.triggerReloadChatList(roomId);



            return Json(new { success = true });

        }



        [HttpPost]

        public ActionResult LeaveRoomBeacon(int roomId)

        {

            return new HttpStatusCodeResult(200);

        }



        // =========================================================================

        // 11. TẠO HOẶC TRUY CẬP PHÒNG CHAT RIÊNG 1VS1

        // =========================================================================

        [HttpPost]

        public JsonResult CreatePrivateRoom(int targetId)

        {

            if (Session["UserID"] == null || Session["Username"] == null)

                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để gửi tin nhắn!" });

            int myId = (int)Session["UserID"];



            if (myId == targetId)

                return Json(new { success = false, message = "Không thể tự chat với chính mình." });



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



        // =========================================================================

        // 12. PHÒNG CHAT RIÊNG / NHÓM

        // =========================================================================

        [HttpGet]

        public ActionResult Private(int id)

        {

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

                TempData["Error"] = "Bạn không có quyền truy cập phòng trò chuyện này.";

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

                             .AsNoTracking()

                             .Include("User")

                             .Where(m => m.RoomID == room.RoomID

                                 && !m.Content.StartsWith("[SYSTEM_RENAME]"))

                             .OrderByDescending(m => m.MessageID)

                             .Take(30)

                             .OrderBy(m => m.MessageID)

                             .ToList();



            var replyIds = messages.Where(m => m.ReplyToID.HasValue)

                                   .Select(m => m.ReplyToID.Value)

                                   .Distinct()

                                   .ToList();



            var replyMessages = new Dictionary<int, string>();

            if (replyIds.Any())

            {

                var rawReplies = db.ChatMessages

                                      .AsNoTracking()

                                      .Where(m => replyIds.Contains(m.MessageID))

                                      .Select(m => new { m.MessageID, m.Content })

                                      .ToList();



                replyMessages = rawReplies

                                      .ToDictionary(m => m.MessageID, m => FormatMessagePreview(m.Content));

            }



            ViewBag.ReplyMessages = replyMessages;

            return View("Index", messages);

        }



        // =========================================================================

        // 12B. PHÂN TRANG LỊCH SỬ TIN NHẮN THEO CURSOR (MessageID)

        // =========================================================================

        [HttpGet]

        public JsonResult GetMessageHistory(int roomId, int beforeMessageId, int take = 30)

        {

            if (take <= 0 || take > 50) take = 30;

            int myId = (int)Session["UserID"];



            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

                return Json(new { success = false, message = "Phòng không tồn tại." }, JsonRequestBehavior.AllowGet);



            bool isMember = room.RoomType == "Public" || db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

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

                bool isMe = (m.SenderID == myId);

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

                    replyToId = m.ReplyToID,

                    replyContent = replySnippet

                };

            }).ToList();



            return Json(new { success = true, messages = result, hasMore = hasMore }, JsonRequestBehavior.AllowGet);

        }



        // =========================================================================

        // 13. KIỂM TRA TRẠNG THÁI PHÒNG

        // =========================================================================

        [HttpGet]

        public JsonResult CheckRoomReady(int roomId)

        {

            int myId = (int)Session["UserID"];

            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

            {

                return Json(new { ready = false, message = "Phòng không tồn tại." }, JsonRequestBehavior.AllowGet);

            }



            bool isMember = room.RoomType == "Public" || db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

            if (!isMember)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { ready = false, message = "Bạn không có quyền truy cập phòng này." }, JsonRequestBehavior.AllowGet);

            }



            int count = db.ChatParticipants.Count(p => p.RoomID == roomId);

            return Json(new { ready = count >= 2 }, JsonRequestBehavior.AllowGet);

        }



        // =========================================================================

        // 14. THAM GIA NHÓM 5 NGẪU NHIÊN

        // =========================================================================

        [HttpPost]

        public JsonResult JoinRandomGroup()

        {

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



        // =========================================================================

        // 15. GHÉP CẶP 1VS1 ẨN DANH NGẪU NHIÊN

        // =========================================================================

        [HttpPost]

        public JsonResult JoinRandomAnonymous()

        {

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



        // =========================================================================

        // 16. RỜI PHÒNG

        // =========================================================================

        [HttpPost]

        public JsonResult LeaveRoom(int roomId)

        {

            int myId = (int)Session["UserID"];



            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false) return Json(new { success = false, message = "Phòng không tồn tại." });

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

                if (remainCount == 0)
                {
                    ChatHub.EvictRoomConnections(roomId, "Cuộc trò chuyện đã kết thúc vì mọi thành viên đã rời phòng.");
                }
                else
                {
                    ChatHub.EvictUserFromRoom(myId, roomId, "Bạn đã rời khỏi cuộc trò chuyện này.");
                }

                if (remainCount > 0)

                {

                    var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();

                    hubContext.Clients.Group(roomId.ToString())

                              .addNewMessageToPage(0, "Người lạ đã rời khỏi phòng.", null);

                }

            }



            return Json(new { success = true });

        }



        // =========================================================================

        // 17. TẠO NHÓM VỚI DANH SÁCH EMAIL

        // =========================================================================

        [HttpPost]

        public JsonResult CreateGroupWithEmails(string groupName, List<string> emails)

        {

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



                    var renameMsg = new ChatMessage

                    {

                        RoomID = newRoom.RoomID,

                        SenderID = myId,

                        Content = "[SYSTEM_RENAME]" + cleanGroupName,

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



        // =========================================================================

        // 18. MỜI THÀNH VIÊN VÀO NHÓM

        // =========================================================================

        [HttpPost]

        public JsonResult InviteToGroup(int roomId, string email)

        {

            int myId = (int)Session["UserID"];



            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

                return Json(new { success = false, message = "Phòng không tồn tại." });



            bool isMember = db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

            if (!isMember)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Bạn không có quyền mời vào phòng này." });

            }



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

            ChatHub.InvalidateRoomCache(roomId);

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();

            hubContext.Clients.Group(roomId.ToString())

                      .addNewMessageToPage(0, notifyMsg.Content, null, notifyMsg.MessageID);



            hubContext.Clients.Group("user_" + user.UserID)

                      .receiveChatInvitation(myId, myUsername, roomId, user.UserID);



            hubContext.Clients.All.triggerReloadChatList(roomId);



            return Json(new { success = true, message = "Đã thêm " + user.Username + " vào nhóm thành công!" });

        }



        // =========================================================================

        // 19. ĐẾM SỐ THÀNH VIÊN TRỰC TUYẾN

        // =========================================================================

        [HttpGet]

        public JsonResult GetOnlineCount(int roomId)

        {

            try

            {

                int myId = (int)Session["UserID"];

                var room = db.ChatRooms.Find(roomId);

                if (room == null || room.IsActive == false)

                    return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);



                bool isMember = room.RoomType == "Public" || db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

                if (!isMember)

                {

                    Response.StatusCode = 403;

                    Response.TrySkipIisCustomErrors = true;

                    return Json(new { count = 0, message = "Không có quyền truy cập." }, JsonRequestBehavior.AllowGet);

                }



                int count;

                if (room.RoomType == "Public")

                {

                    var cutoff = DateTime.Now.AddMinutes(-5);

                    count = db.Users.Count(u => u.LastActivityAt != null && u.LastActivityAt > cutoff);

                }

                else

                {

                    count = db.ChatParticipants.Count(p => p.RoomID == roomId);

                }



                return Json(new { count = count }, JsonRequestBehavior.AllowGet);

            }

            catch

            {

                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);

            }

        }



        // =========================================================================

        // 20. XEM DANH SÁCH THÀNH VIÊN TRONG PHÒNG

        // =========================================================================

        [HttpGet]

        public JsonResult GetRoomMembers(int roomId)

        {

            if (roomId <= 0)

                return Json(new { success = false, message = "Mã phòng không hợp lệ." }, JsonRequestBehavior.AllowGet);



            int myId = (int)Session["UserID"];

            var room = db.ChatRooms.Find(roomId);

            if (room == null || room.IsActive == false)

                return Json(new { success = false, message = "Phòng không tồn tại hoặc đã bị xóa." }, JsonRequestBehavior.AllowGet);



            bool isMember = room.RoomType == "Public" || db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == myId);

            if (!isMember)

            {

                Response.StatusCode = 403;

                Response.TrySkipIisCustomErrors = true;

                return Json(new { success = false, message = "Bạn không thuộc cuộc trò chuyện này." }, JsonRequestBehavior.AllowGet);

            }



            var memberList = new List<object>();



            if (room.RoomType == "Public")

            {

                var cutoff = DateTime.Now.AddMinutes(-15);

                var activeUsers = db.Users.Where(u => u.LastActivityAt != null && u.LastActivityAt > cutoff).Take(50).ToList();

                foreach (var u in activeUsers)

                {

                    memberList.Add(new

                    {

                        userId = u.UserID,

                        username = u.Username,

                        displayName = u.DisplayName ?? u.Username,

                        avatar = u.SafeAvatar,

                        isOnline = (DateTime.Now - (u.LastActivityAt ?? DateTime.MinValue)).TotalMinutes < 5,

                        isMe = (u.UserID == myId),

                        isAdmin = (u.IsAdmin == true)

                    });

                }

            }

            else

            {

                var participants = db.ChatParticipants.Include("User").Where(p => p.RoomID == roomId).ToList();

                foreach (var p in participants)

                {

                    var u = p.User;

                    if (u == null) continue;

                    memberList.Add(new

                    {

                        userId = u.UserID,

                        username = u.Username,

                        displayName = u.DisplayName ?? u.Username,

                        avatar = u.SafeAvatar,

                        isOnline = u.LastActivityAt.HasValue && (DateTime.Now - u.LastActivityAt.Value).TotalMinutes < 5,

                        isMe = (u.UserID == myId),

                        isAdmin = (u.IsAdmin == true)

                    });

                }

            }



            return Json(new { success = true, members = memberList }, JsonRequestBehavior.AllowGet);

        }



        protected override void Dispose(bool disposing)

        {

            if (disposing) db.Dispose();

            base.Dispose(disposing);

        }

    }

}

