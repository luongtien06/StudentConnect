using System.Linq;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class NotificationController : BaseController
    {
        private int GetUserId() => Session["UserID"] != null ? (int)Session["UserID"] : 0;

        public JsonResult GetNotifications()
        {
            int userId = GetUserId();
            if (userId <= 0) return Json(new object[0], JsonRequestBehavior.AllowGet);

            var notifs = db.Notifications
                .AsNoTracking()
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(30)
                .Select(n => new
                {
                    n.NotificationID,
                    n.Message,
                    n.Type,
                    n.Url,
                    n.IsRead,
                    n.CreatedAt
                })
                .ToList();

            return Json(notifs, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetUnreadCount()
        {
            int userId = GetUserId();
            if (userId <= 0) return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);

            int count = db.Notifications.AsNoTracking().Count(n => n.UserID == userId && !n.IsRead);
            return Json(new { count }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarkRead(int id)
        {
            int userId = GetUserId();
            if (userId <= 0) return Json(new { success = false });

            var notif = db.Notifications
                .FirstOrDefault(x => x.NotificationID == id && x.UserID == userId);

            if (notif != null)
            {
                notif.IsRead = true;
                db.SaveChanges();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarkAllRead()
        {
            int userId = GetUserId();
            if (userId <= 0) return Json(new { success = false });

            db.Notifications
                .Where(n => n.UserID == userId && !n.IsRead)
                .ToList()
                .ForEach(n => n.IsRead = true);

            db.SaveChanges();
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Delete(int id)
        {
            int userId = GetUserId();
            if (userId <= 0) return Json(new { success = false });

            var notif = db.Notifications
                .FirstOrDefault(x => x.NotificationID == id && x.UserID == userId);

            if (notif != null)
            {
                db.Notifications.Remove(notif);
                db.SaveChanges();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteAll()
        {
            int userId = GetUserId();
            if (userId <= 0) return Json(new { success = false });

            var notifs = db.Notifications.Where(n => n.UserID == userId).ToList();
            if (notifs.Any())
            {
                db.Notifications.RemoveRange(notifs);
                db.SaveChanges();
            }

            return Json(new { success = true });
        }

        public ActionResult Index()
        {
            int userId = GetUserId();
            if (userId <= 0) return RedirectToAction("Login", "User");

            var notifs = db.Notifications
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            return View(notifs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ReportPost(string postType, int postId, string reason, string details)
        {
            int reporterId = GetUserId();
            if (reporterId <= 0) return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để gửi báo cáo vi phạm." });

            int? ownerId = null;
            string content = "";
            string targetUrl = "";
            switch ((postType ?? "").ToLowerInvariant())
            {
                case "feed":
                    var feed = db.Posts.Find(postId); if (feed != null) { ownerId = feed.UserID; content = feed.Content ?? ""; targetUrl = "/User/Profile/" + feed.UserID + "#post-" + postId; } break;
                case "market":
                    var market = db.MarketPosts.Find(postId); if (market != null) { ownerId = market.UserID; content = (market.Title ?? "") + " - " + (market.Description ?? ""); targetUrl = "/Market/Details/" + postId; } break;
                case "connect":
                    var connect = db.ConnectPosts.Find(postId); if (connect != null) { ownerId = connect.UserID; content = (connect.Title ?? "") + " - " + (connect.Description ?? ""); targetUrl = "/Connect/Details/" + postId; } break;
                default: return Json(new { success = false, message = "Loại bài đăng không hợp lệ." });
            }
            if (!ownerId.HasValue) return Json(new { success = false, message = "Bài đăng không còn tồn tại." });
            if (ownerId.Value == reporterId) return Json(new { success = false, message = "Bạn không thể tố cáo bài đăng của chính mình." });
            reason = (reason ?? "").Trim();
            if (string.IsNullOrWhiteSpace(reason)) return Json(new { success = false, message = "Vui lòng chọn lý do tố cáo." });
            details = (details ?? "").Trim();
            if (details.Length > 1000) details = details.Substring(0, 1000);

            var reporter = db.Users.Find(reporterId);
            var reportUrl = "/Admin/AdminHome/Index?reportType=" + postType + "&postId=" + postId + "&reportedUserId=" + ownerId + "&reporterId=" + reporterId + "&targetUrl=" + System.Web.HttpUtility.UrlEncode(targetUrl);
            var threshold = System.DateTime.Now.AddSeconds(-30);
            if (db.Notifications.Any(n => n.Type == "report" && n.Url == reportUrl && n.CreatedAt > threshold))
                return Json(new { success = true, message = "Báo cáo của bạn đã được tiếp nhận." });
            NotifyAdmins("report", "🚨 BÁO CÁO BÀI ĐĂNG: " + (reporter?.DisplayName ?? reporter?.Username ?? "Người dùng") + " đã tố cáo bài " + postType + ". Lý do: " + reason + ". Chi tiết: " + details + " | Nội dung: \"" + (content.Length > 160 ? content.Substring(0, 160) + "..." : content) + "\"", reportUrl);
            return Json(new { success = true, message = "Đã gửi tố cáo đến ban quản trị." });
        }
    }
}
