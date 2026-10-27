using StudentConnect.Helpers;
﻿using StudentConnect.Models;

using System;

using System.Linq;

using System.Web.Mvc;



namespace StudentConnect.Areas.Admin.Controllers

{

    // Kế thừa AdminBaseController để bảo mật

    public class AdminHomeController : AdminBaseController

    {

        protected override void OnActionExecuting(ActionExecutingContext filterContext)

        {

            base.OnActionExecuting(filterContext);

            if (filterContext.Result != null) return;



            if ((Session["AdminRole"] as string) == "adminFeedStory" &&

                filterContext.ActionDescriptor.ActionName != "Index")

            {

                filterContext.Result = RedirectToAction("Index", "AdminFeedStory", new { area = "Admin" });

            }

        }



        // GET: Admin/AdminHome

        public ActionResult Index()

        {

            // Truy vấn đếm số lượng để đẩy ra View bằng ViewBag

            ViewBag.TotalUsers = db.Users.AsNoTracking().Count();

            ViewBag.TotalMarketPosts = db.MarketPosts.AsNoTracking().Count();

            ViewBag.TotalConnectPosts = db.ConnectPosts.AsNoTracking().Count();

            ViewBag.TotalFeedPosts = db.Posts.AsNoTracking().Count();

            ViewBag.TotalStories = db.Stories.AsNoTracking().Count();
            ViewBag.TotalAgentTasks = db.AgentTasks.AsNoTracking().Count();
            ViewBag.TotalAgentLogs = db.AgentActionLogs.AsNoTracking().Count();



            string adminEmail = Session["AdminEmail"] as string;

            var currentAdmin = db.Users.FirstOrDefault(u => u.Email == adminEmail && u.IsAdmin == true);



            DateTime? prevLogin = Session["AdminPreviousLogin"] as DateTime?;

            if (!prevLogin.HasValue && currentAdmin != null)

            {

                prevLogin = currentAdmin.LastActivityAt;

            }



            DateTime loginTime = Session["AdminLoginTime"] as DateTime? ?? DateTime.Now;

            if (Session["AdminLoginTime"] == null)

            {

                Session["AdminLoginTime"] = loginTime;

            }



            ViewBag.PreviousLoginTime = prevLogin;

            ViewBag.CurrentLoginTime = loginTime;



            return View();

        }



        // Admin debug: return recent notifications (all types) for inspection

        [HttpGet]

        public JsonResult DebugNotifications(int take = 50)

        {

            try

            {

                var items = db.Notifications

                    .AsNoTracking()

                    .OrderByDescending(n => n.CreatedAt)

                    .Take(take)

                    .Select(n => new {

                        n.NotificationID,

                        n.UserID,

                        UserEmail = n.User != null ? n.User.Email : null,

                        n.Type,

                        n.Message,

                        n.Url,

                        n.IsRead,

                        CreatedAt = n.CreatedAt

                    })

                    .ToList();



                return Json(new { success = true, items = items }, JsonRequestBehavior.AllowGet);

            }

            catch (System.Exception ex)

            {

                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);

            }

        }



        // Debug helper: create a test report notification to verify admin inbox

        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult TestCreateReport()

        {

            try

            {

                string alertMessage = "[TEST] Test report created from admin panel";

                string roomUrl = "/Chat/Index";

                NotifyAdmins("report", alertMessage, roomUrl);

                return Json(new { success = true, message = "Test report created." });

            }

            catch (System.Exception ex)

            {

                return Json(new { success = false, message = ex.Message });

            }

        }



        private User GetCurrentAdmin()

        {

            int? adminId = Session["AdminID"] as int? ?? Session["UserID"] as int?;

            if (adminId.HasValue && adminId.Value > 0)

            {

                var adminById = db.Users.FirstOrDefault(u => u.UserID == adminId.Value && u.IsAdmin);

                if (adminById != null) return adminById;

            }



            string adminEmail = (Session["AdminEmail"] as string ?? "").Trim().ToLower();

            if (!string.IsNullOrEmpty(adminEmail))

            {

                var adminByEmail = db.Users.FirstOrDefault(u => u.Email.ToLower() == adminEmail && u.IsAdmin);

                if (adminByEmail != null) return adminByEmail;

            }



            string adminUsername = (Session["AdminUsername"] as string ?? "").Trim().ToLower();

            if (!string.IsNullOrEmpty(adminUsername))

            {

                var adminByUsername = db.Users.FirstOrDefault(u => u.Username.ToLower() == adminUsername && u.IsAdmin);

                if (adminByUsername != null) return adminByUsername;

            }



            return null;

        }



        private bool CanManageReports(User admin)

        {

            if (admin == null || !admin.IsAdmin) return false;

            string role = Session["AdminRole"] as string ?? admin.AdminRole ?? "";

            return role == "SuperAdmin" || role == "AdminUser" || string.IsNullOrEmpty(role);

        }



        [HttpGet]

        public JsonResult GetReports()

        {

            var currentAdmin = GetCurrentAdmin();

            if (currentAdmin == null || !CanManageReports(currentAdmin))

            {

                return Json(new { success = true, unreadCount = 0, reports = new object[0] }, JsonRequestBehavior.AllowGet);

            }



            var reports = db.Notifications

                .AsNoTracking()

                .Where(n => n.Type == "report" && n.UserID == currentAdmin.UserID)

                .OrderByDescending(n => n.CreatedAt)

                .Take(25)

                .Select(n => new

                {

                    n.NotificationID,

                    n.Message,

                    n.Type,

                    n.Url,

                    n.IsRead,

                    n.CreatedAt

                })

                .ToList()

                .Select(n => new

                {

                    n.NotificationID,

                    n.Message,

                    n.Type,

                    n.Url,

                    n.IsRead,

                    CreatedAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm")

                })

                .ToList();



            int unreadCount = db.Notifications.Count(n => n.Type == "report" && n.UserID == currentAdmin.UserID && !n.IsRead);



            return Json(new { success = true, unreadCount = unreadCount, reports = reports }, JsonRequestBehavior.AllowGet);

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult MarkReportRead(int id)

        {

            try

            {

                var currentAdmin = GetCurrentAdmin();

                if (currentAdmin == null || !CanManageReports(currentAdmin))

                {

                    return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

                }



                var notif = db.Notifications.FirstOrDefault(n => n.NotificationID == id && n.UserID == currentAdmin.UserID);

                if (notif != null)

                {

                    if (!notif.IsRead)

                    {

                        notif.IsRead = true;

                        db.SaveChanges();

                    }

                    int unreadCount = db.Notifications.Count(n => n.Type == "report" && n.UserID == currentAdmin.UserID && !n.IsRead);

                    return Json(new { success = true, unreadCount = unreadCount, message = "Đã đánh dấu đã đọc thông báo." });

                }

                return Json(new { success = false, message = "Không tìm thấy thông báo hoặc bạn không có quyền truy cập." });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi cập nhật trạng thái thông báo: " + ex.Message });

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult MarkAllReportsRead()

        {

            try

            {

                var currentAdmin = GetCurrentAdmin();

                if (currentAdmin == null || !CanManageReports(currentAdmin))

                {

                    return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

                }



                // CHỈ ĐÁNH DẤU ĐÃ ĐỌC CÁC THÔNG BÁO THUỘC PHẠM VI ADMIN HIỆN TẠI

                var unreadReports = db.Notifications

                    .Where(n => n.Type == "report" && n.UserID == currentAdmin.UserID && !n.IsRead)

                    .ToList();



                int updatedCount = unreadReports.Count;

                if (updatedCount > 0)

                {

                    foreach (var r in unreadReports)

                    {

                        r.IsRead = true;

                    }

                    db.SaveChanges();

                }



                return Json(new

                {

                    success = true,

                    updatedCount = updatedCount,

                    unreadCount = 0,

                    message = updatedCount > 0

                        ? $"Đã đánh dấu đã đọc {updatedCount} thông báo."

                        : "Tất cả thông báo đã ở trạng thái đã đọc."

                });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi đánh dấu đã đọc thông báo: " + ex.Message });

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult DeleteAllReports()

        {

            try

            {

                var currentAdmin = GetCurrentAdmin();

                if (currentAdmin == null || !CanManageReports(currentAdmin))

                {

                    return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

                }



                // CHỈ XÓA THÔNG BÁO TỐ CÁO THUỘC VỀ ADMIN HIỆN TẠI (KHÔNG XÓA ADMIN KHÁC)

                var reports = db.Notifications

                    .Where(n => n.Type == "report" && n.UserID == currentAdmin.UserID)

                    .ToList();



                int count = reports.Count;

                if (count > 0)

                {

                    db.Notifications.RemoveRange(reports);

                    db.SaveChanges();

                }



                return Json(new

                {

                    success = true,

                    deletedCount = count,

                    unreadCount = 0,

                    message = count > 0

                        ? $"Đã xóa thành công {count} thông báo."

                        : "Không có thông báo nào để xóa."

                });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi xóa thông báo: " + ex.Message });

            }

        }



        [HttpPost]

        [ValidateAntiForgeryToken]

        public JsonResult DeleteReport(int id)

        {

            try

            {

                var currentAdmin = GetCurrentAdmin();

                if (currentAdmin == null || !CanManageReports(currentAdmin))

                {

                    return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });

                }



                var notif = db.Notifications.FirstOrDefault(n => n.NotificationID == id && n.UserID == currentAdmin.UserID);

                if (notif != null)

                {

                    db.Notifications.Remove(notif);

                    db.SaveChanges();



                    int unreadCount = db.Notifications.Count(n => n.Type == "report" && n.UserID == currentAdmin.UserID && !n.IsRead);

                    return Json(new { success = true, unreadCount = unreadCount, message = "Đã xóa thông báo thành công." });

                }

                return Json(new { success = false, message = "Không tìm thấy thông báo cần xóa hoặc bạn không có quyền." });

            }

            catch (Exception ex)

            {

                return Json(new { success = false, message = "Lỗi khi xóa thông báo: " + ex.Message });

            }

        }



        // Lấy chi tiết báo cáo vi phạm kèm thông tin người bị tố cáo và tin nhắn vi phạm

        [HttpGet]

        public JsonResult GetReportDetail(int id)

        {

            var currentAdmin = GetCurrentAdmin();

            if (currentAdmin == null || !CanManageReports(currentAdmin))

                return Json(new { success = false, message = "Bạn không có quyền xem báo cáo này." }, JsonRequestBehavior.AllowGet);



            var notif = db.Notifications.FirstOrDefault(n => n.NotificationID == id && n.UserID == currentAdmin.UserID);

            if (notif == null)

                return Json(new { success = false, message = "Không tìm thấy báo cáo hoặc bạn không có quyền xem." }, JsonRequestBehavior.AllowGet);



            // Parse query string from Url

            int? reportedUserId = null;

            int? reporterId = null;

            int? messageId = null;

            int? roomId = null;

            int? postId = null;

            string reportType = "";

            string targetUrl = "/";

            string reason = "";

            string details = "";

            string violatingMessage = "";

            string violatingImageUrl = null;

            string violatingSentAt = "";



            if (!string.IsNullOrEmpty(notif.Url))

            {

                try

                {

                    var uri = new System.Uri("http://dummy" + notif.Url);

                    var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);

                    if (!string.IsNullOrEmpty(qs["reportedUserId"]))

                    {

                        int rid;

                        if (int.TryParse(qs["reportedUserId"], out rid) && rid > 0)

                            reportedUserId = rid;

                    }

                    if (!string.IsNullOrEmpty(qs["reporterId"]))

                    {

                        int rpid;

                        if (int.TryParse(qs["reporterId"], out rpid) && rpid > 0)

                            reporterId = rpid;

                    }

                    if (!string.IsNullOrEmpty(qs["messageId"]))

                    {

                        int mid;

                        if (int.TryParse(qs["messageId"], out mid) && mid > 0)

                            messageId = mid;

                    }

                    if (!string.IsNullOrEmpty(qs["roomId"]))

                    {

                        int rmid;

                        if (int.TryParse(qs["roomId"], out rmid) && rmid > 0)

                            roomId = rmid;

                    }

                    int pid;

                    if (int.TryParse(qs["postId"], out pid) && pid > 0) postId = pid;

                    reportType = qs["reportType"] ?? "";

                    targetUrl = qs["targetUrl"] ?? targetUrl;

                }

                catch { }

            }



            if (postId.HasValue)

            {

                if (reportType.Equals("feed", StringComparison.OrdinalIgnoreCase)) violatingMessage = db.Posts.Where(p => p.PostID == postId.Value).Select(p => p.Content).FirstOrDefault() ?? violatingMessage;

                else if (reportType.Equals("market", StringComparison.OrdinalIgnoreCase)) violatingMessage = db.MarketPosts.Where(p => p.PostID == postId.Value).Select(p => p.Title + " - " + p.Description).FirstOrDefault() ?? violatingMessage;

                else if (reportType.Equals("connect", StringComparison.OrdinalIgnoreCase)) violatingMessage = db.ConnectPosts.Where(p => p.PostID == postId.Value).Select(p => p.Title + " - " + p.Description).FirstOrDefault() ?? violatingMessage;

            }



            // Nếu có messageId, truy xuất trực tiếp từ ChatMessages

            if (messageId.HasValue && messageId.Value > 0)

            {

                var targetMsg = db.ChatMessages.Find(messageId.Value);

                if (targetMsg != null)

                {

                    violatingMessage = targetMsg.Content;

                    violatingImageUrl = targetMsg.ImageUrl;

                    violatingSentAt = targetMsg.SentAt.HasValue ? targetMsg.SentAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "";

                    if (!roomId.HasValue && targetMsg.RoomID.HasValue)

                        roomId = targetMsg.RoomID.Value;

                    if (!reportedUserId.HasValue && targetMsg.SenderID.HasValue)

                        reportedUserId = targetMsg.SenderID.Value;

                }

            }



            // Phân tích text từ notif.Message nếu thiếu dữ liệu

            string notifText = notif.Message ?? "";

            try

            {

                if (string.IsNullOrEmpty(violatingMessage) && notifText.Contains("Nội dung: \""))

                {

                    int startIdx = notifText.IndexOf("Nội dung: \"") + "Nội dung: \"".Length;

                    int endIdx = notifText.LastIndexOf("\"");

                    if (endIdx > startIdx)

                        violatingMessage = notifText.Substring(startIdx, endIdx - startIdx);

                }



                if (notifText.Contains("Lý do: "))

                {

                    int rStart = notifText.IndexOf("Lý do: ") + "Lý do: ".Length;

                    int rEnd = notifText.IndexOf(". Chi tiết:", rStart);

                    if (rEnd > rStart)

                        reason = notifText.Substring(rStart, rEnd - rStart).Trim();

                }



                if (notifText.Contains("Chi tiết: "))

                {

                    int dStart = notifText.IndexOf("Chi tiết: ") + "Chi tiết: ".Length;

                    int dEnd = notifText.IndexOf(" | Nội dung:", dStart);

                    if (dEnd > dStart)

                        details = notifText.Substring(dStart, dEnd - dStart).Trim();

                    else if (dEnd == -1)

                        details = notifText.Substring(dStart).Trim();

                }

            }

            catch { }



            // Người bị tố cáo

            object reportedUserInfo = null;

            if (reportedUserId.HasValue && reportedUserId.Value > 0)

            {

                int rUid = reportedUserId.Value;

                var rUser = db.Users.Find(rUid);

                if (rUser != null)

                {

                    int marketCount = db.MarketPosts.Count(p => p.UserID == rUid);

                    int connectCount = db.ConnectPosts.Count(p => p.UserID == rUid);

                    int msgCount = db.ChatMessages.Count(m => m.SenderID == rUid);

                    reportedUserInfo = new

                    {

                        UserID = rUser.UserID,

                        Username = rUser.Username ?? "Chưa đặt tên",

                        Email = rUser.Email ?? "Chưa có email",

                        PhoneNumber = string.IsNullOrEmpty(rUser.PhoneNumber) ? "Chưa cập nhật" : rUser.PhoneNumber,

                        Avatar = string.IsNullOrEmpty(rUser.Avatar) ? DefaultImagePlaceholders.AvatarPath : rUser.Avatar,

                        IsTDMUStudent = rUser.IsTDMUStudent,

                        IsAdmin = rUser.IsAdmin,

                        CreatedAt = rUser.CreatedAt.HasValue ? rUser.CreatedAt.Value.ToString("dd/MM/yyyy") : "---",

                        TotalPosts = marketCount + connectCount,

                        TotalMessages = msgCount

                    };

                }

            }



            // Người tố cáo

            object reporterInfo = null;

            if (reporterId.HasValue && reporterId.Value > 0)

            {

                var rpUser = db.Users.Find(reporterId.Value);

                if (rpUser != null)

                {

                    reporterInfo = new

                    {

                        UserID = rpUser.UserID,

                        Username = rpUser.Username ?? rpUser.Email,

                        Email = rpUser.Email ?? ""

                    };

                }

            }



            // Đánh dấu đã đọc thông báo này

            if (!notif.IsRead)

            {

                notif.IsRead = true;

                db.SaveChanges();

            }



            return Json(new

            {

                success = true,

                NotificationID = notif.NotificationID,

                Message = notif.Message,

                Url = notif.Url,

                CreatedAt = notif.CreatedAt.ToString("dd/MM/yyyy HH:mm"),

                RoomId = roomId,

                PostId = postId,

                ReportType = reportType,

                TargetUrl = targetUrl,

                MessageId = messageId,

                Reason = string.IsNullOrEmpty(reason) ? "Báo cáo vi phạm" : reason,

                Details = details,

                ViolatingMessage = violatingMessage,

                ViolatingImageUrl = violatingImageUrl,

                ViolatingSentAt = violatingSentAt,

                ReportedUser = reportedUserInfo,

                Reporter = reporterInfo

            }, JsonRequestBehavior.AllowGet);

        }



        // Gửi cảnh báo vi phạm đến người dùng

        [HttpPost]

        public JsonResult WarnUser(int userId, string warningMessage, string targetUrl = null)

        {

            var user = db.Users.Find(userId);

            if (user == null)

                return Json(new { success = false, message = "Người dùng không tồn tại." });



            string msg = "⚠️ CẢNH BÁO VI PHẠM TỪ BAN QUẢN TRỊ: " + (string.IsNullOrEmpty(warningMessage)

                ? "Tài khoản của bạn vừa bị báo cáo do phát ngôn hoặc hành vi vi phạm chuẩn mực cộng đồng TDMU. Vui lòng kiểm tra lại để tránh bị khóa tài khoản vĩnh viễn."

                : warningMessage);



            CreateNotification(userId, "warning", msg, string.IsNullOrWhiteSpace(targetUrl) ? "/Chat/Index" : targetUrl);



            return Json(new { success = true, message = $"Đã gửi cảnh báo thành công đến tài khoản \"{user.Username ?? user.Email}\"." });

        }



        // Xóa tài khoản người dùng vi phạm và TOÀN BỘ dữ liệu liên quan (Cascade delete sạch 100%)

        [HttpPost]

        public JsonResult DeleteUser(int userId)

        {

            var user = db.Users.Find(userId);

            if (user == null)

                return Json(new { success = false, message = "Người dùng không tồn tại hoặc đã bị xóa." });



            if (user.IsAdmin)

                return Json(new { success = false, message = "Bảo mật hệ thống: Không thể xóa tài khoản Quản trị viên (Admin)." });



            string deletedName = user.Username ?? user.Email;



            using (var transaction = db.Database.BeginTransaction())

            {

                try

                {

                    // 1. Lấy danh sách ID các bài đăng Market & Connect của user

                    var marketPostIds = db.MarketPosts.Where(p => p.UserID == userId).Select(p => p.PostID).ToList();

                    var connectPostIds = db.ConnectPosts.Where(p => p.UserID == userId).Select(p => p.PostID).ToList();



                    // 2. Xóa các Thông báo (Notifications)

                    // a) Thông báo gửi cho user

                    var userNotifs = db.Notifications.Where(n => n.UserID == userId).ToList();

                    db.Notifications.RemoveRange(userNotifs);



                    // b) Thông báo dẫn đến các bài viết của user

                    foreach (var pid in marketPostIds)

                    {

                        string mUrl = $"/Market/Details/{pid}".ToLower();

                        var mNotifs = db.Notifications.Where(n => n.Url != null && n.Url.ToLower().Contains(mUrl)).ToList();

                        db.Notifications.RemoveRange(mNotifs);

                    }

                    foreach (var cid in connectPostIds)

                    {

                        string cUrl = $"/Connect/Details/{cid}".ToLower();

                        var cNotifs = db.Notifications.Where(n => n.Url != null && n.Url.ToLower().Contains(cUrl)).ToList();

                        db.Notifications.RemoveRange(cNotifs);

                    }



                    // 3. Xóa Đánh giá & Bình luận (Reviews)

                    // a) Reviews do user này viết

                    // b) Reviews viết vào các bài Market hoặc Connect của user này

                    var relatedReviews = db.Reviews.Where(r => r.UserID == userId

                        || (r.PostType == "Market" && r.PostID.HasValue && marketPostIds.Contains(r.PostID.Value))

                        || (r.PostType == "Connect" && r.PostID.HasValue && connectPostIds.Contains(r.PostID.Value))).ToList();

                    db.Reviews.RemoveRange(relatedReviews);



                    // 4. Xóa Wishlists chợ

                    // a) Wishlists do user lưu

                    // b) Wishlists của người khác lưu bài chợ của user này

                    var wishlists = db.MarketWishlists.Where(w => w.UserID == userId

                        || (w.PostID.HasValue && marketPostIds.Contains(w.PostID.Value))).ToList();

                    db.MarketWishlists.RemoveRange(wishlists);



                    // 5. Xóa Phòng chat chợ (MarketChatRooms)

                    // a) User là người mua hoặc người bán

                    // b) Phòng chat gắn với bài chợ của user này

                    var marketRooms = db.MarketChatRooms.Where(r => r.BuyerID == userId

                        || r.SellerID == userId

                        || (r.PostID.HasValue && marketPostIds.Contains(r.PostID.Value))).ToList();

                    db.MarketChatRooms.RemoveRange(marketRooms);



                    // 6. Xóa Ảnh bài đăng chợ (MarketImages)

                    var marketImages = db.MarketImages.Where(img => img.PostID.HasValue && marketPostIds.Contains(img.PostID.Value)).ToList();

                    db.MarketImages.RemoveRange(marketImages);



                    // 7. Xóa Bài đăng chợ (MarketPosts)

                    var marketPosts = db.MarketPosts.Where(p => p.UserID == userId).ToList();

                    db.MarketPosts.RemoveRange(marketPosts);



                    // 8. Xóa Bài đăng kết nối (ConnectPosts)

                    var connectPosts = db.ConnectPosts.Where(p => p.UserID == userId).ToList();

                    db.ConnectPosts.RemoveRange(connectPosts);



                    // 9. Xóa Tin nhắn Chat (ChatMessages)

                    // a) Gỡ liên kết ReplyToID cho các tin nhắn khác đang reply vào tin của user này

                    var userMessageIds = db.ChatMessages.Where(m => m.SenderID == userId).Select(m => m.MessageID).ToList();

                    if (userMessageIds.Any())

                    {

                        var dependentReplies = db.ChatMessages.Where(m => m.ReplyToID.HasValue && userMessageIds.Contains(m.ReplyToID.Value)).ToList();

                        foreach (var dr in dependentReplies)

                        {

                            dr.ReplyToID = null;

                        }

                        db.SaveChanges(); // Lưu để gỡ khóa ngoại tự tham chiếu

                    }



                    // b) Xóa các tin nhắn do user này gửi

                    var userMessages = db.ChatMessages.Where(m => m.SenderID == userId).ToList();

                    db.ChatMessages.RemoveRange(userMessages);



                    // 10. Xóa Thành viên phòng chat (ChatParticipants)

                    var participants = db.ChatParticipants.Where(p => p.UserID == userId).ToList();

                    db.ChatParticipants.RemoveRange(participants);



                    // 11. Cuối cùng, xóa User

                    db.Users.Remove(user);

                    db.SaveChanges();



                    transaction.Commit();



                    return Json(new

                    {

                        success = true,

                        message = $"Đã xóa vĩnh viễn tài khoản \"{deletedName}\" cùng toàn bộ dữ liệu ({marketPosts.Count} bài chợ, {connectPosts.Count} bài kết nối, {userMessages.Count} tin nhắn) thành công!"

                    });

                }

                catch (System.Exception ex)

                {

                    transaction.Rollback();

                    return Json(new { success = false, message = "Có lỗi xảy ra khi xóa dữ liệu tài khoản: " + ex.Message });

                }

            }

        }



        protected override void Dispose(bool disposing)

        {

            if (disposing) db.Dispose();

            base.Dispose(disposing);

        }

    }

}

