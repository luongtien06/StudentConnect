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
    }
}