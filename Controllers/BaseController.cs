// Controllers/BaseController.cs
using StudentConnect.Models;
using System;
using System.Linq;
using System.Diagnostics;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class BaseController : Controller
    {
        protected readonly TDMUEcoSystemEntities db = new TDMUEcoSystemEntities();

        protected void CreateNotification(int receiverId, string type, string message, string url)
        {
            var userExists = db.Users.Any(u => u.UserID == receiverId);
            if (!userExists) return; // Nếu không tồn tại, thoát luôn để tránh crash lỗi Foreign Key

            var notif = new Notification
            {
                UserID = receiverId,
                Message = message,
                Type = type?.ToLower(), // Đảm bảo luôn lưu chữ thường để View filter chuẩn
                Url = url,
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            db.Notifications.Add(notif);
            db.SaveChanges();

            // diagnostic trace for debugging notification creation
            try
            {
                Trace.WriteLine($"[Notify] Created notification (ID={notif.NotificationID}) for user {receiverId}, type={notif.Type}");
            }
            catch { }

            // ✅ Real-time Notification Push qua SignalR ChatHub
            try
            {
                var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<StudentConnect.Hubs.ChatHub>();
                hubContext.Clients.Group("user_" + receiverId).receiveNotification(new
                {
                    NotificationID = notif.NotificationID,
                    Message = notif.Message,
                    Type = notif.Type,
                    Url = notif.Url,
                    CreatedAt = notif.CreatedAt
                });
            }
            catch { }
        }

        protected void NotifyAdmins(string type, string message, string url)
        {
            try
            {
                var adminQuery = db.Users.Where(u => u.IsAdmin == true);
                if (type == "report")
                {
                    adminQuery = adminQuery.Where(u => u.AdminRole == "SuperAdmin" || u.AdminRole == "AdminUser" || string.IsNullOrEmpty(u.AdminRole));
                }
                var adminIds = adminQuery.Select(u => u.UserID).ToList();

                // If no explicit admin flags exist, try common fallbacks so reports are not lost.
                if (!adminIds.Any())
                {
                    // Try to find a user containing 'admin' in email/username
                    var fallbackAdmin = db.Users.FirstOrDefault(u => (u.Email ?? "").ToLower().Contains("admin") || (u.Username ?? "").ToLower().Contains("admin"));
                    if (fallbackAdmin != null)
                    {
                        adminIds.Add(fallbackAdmin.UserID);
                    }
                }

                // Ultimate fallback: add the first user in the database (likely the initial admin/owner)
                if (!adminIds.Any())
                {
                    var firstUser = db.Users.OrderBy(u => u.UserID).FirstOrDefault();
                    if (firstUser != null) adminIds.Add(firstUser.UserID);
                }

                foreach (var adminId in adminIds.Distinct())
                {
                    CreateNotification(adminId, type, message, url);
                }
            }
            catch { }
        }

        /// <summary>
        /// Xóa tất cả các thông báo liên quan đến một bài viết khi bài viết đó bị xóa
        /// </summary>
        protected void DeleteRelatedNotifications(string postType, int postId)
        {
            if (string.IsNullOrEmpty(postType) || postId <= 0) return;

            string targetUrl = $"/{postType}/Details/{postId}".ToLower();
            var relatedNotifs = db.Notifications
                .Where(n => n.Url != null && n.Url.ToLower().Contains(targetUrl))
                .ToList();

            if (relatedNotifs.Any())
            {
                db.Notifications.RemoveRange(relatedNotifs);
                db.SaveChanges();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}