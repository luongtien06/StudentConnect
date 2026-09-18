using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.Linq;
using System.Web.Mvc;

namespace StudentConnect.Areas.Admin.Controllers
{
    public class AdminUserController : AdminBaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (filterContext.Result != null) return;
            AuthorizeRole(filterContext, "AdminUser");
        }

        // Admin/AdminUser/Index
        public ActionResult Index()
        {
            var users = db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).ToList();
            return View(users);
        }

        // Admin/AdminUser/Create 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(User user)
        {
            try
            {
                user.CreatedAt = DateTime.Now;
                user.LastActivityAt = DateTime.Now;

                string currentRole = Session["AdminRole"] as string ?? "";
                if (currentRole == "SuperAdmin")
                {
                    if (!user.IsAdmin)
                    {
                        user.AdminRole = null;
                    }
                    else if (string.IsNullOrEmpty(user.AdminRole))
                    {
                        user.AdminRole = "AdminUser";
                    }
                }
                else
                {
                    user.IsAdmin = false;
                    user.AdminRole = null;
                }

                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    user.PasswordHash = PasswordHelper.HashPassword("123456");
                }
                else
                {
                    user.PasswordHash = PasswordHelper.HashPassword(user.PasswordHash);
                }

                if (string.IsNullOrEmpty(user.Avatar))
                {
                    user.Avatar = "/Content/Images/default-avatar.png";
                }

                db.Users.Add(user);
                db.SaveChanges();

                TempData["Success"] = "Thêm người dùng mới thành công! (Mật khẩu đã được mã hóa)";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi thêm: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // Admin/AdminUser/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(User user)
        {
            try
            {
                var existingUser = db.Users.Find(user.UserID);
                if (existingUser != null)
                {
                    string currentRole = Session["AdminRole"] as string ?? "";

                    if (currentRole != "SuperAdmin" && existingUser.IsAdmin)
                    {
                        TempData["Error"] = "Chỉ Quản trị viên tối cao mới có quyền chỉnh sửa tài khoản Quản trị viên!";
                        return RedirectToAction("Index");
                    }

                    existingUser.Username = user.Username;
                    existingUser.Email = user.Email;
                    existingUser.PhoneNumber = user.PhoneNumber;
                    existingUser.IsTDMUStudent = user.IsTDMUStudent;

                    if (currentRole == "SuperAdmin")
                    {
                        existingUser.IsAdmin = user.IsAdmin;
                        existingUser.AdminRole = user.IsAdmin ? (string.IsNullOrEmpty(user.AdminRole) ? "AdminUser" : user.AdminRole) : null;
                    }

                    db.SaveChanges();
                    TempData["Success"] = "Cập nhật thông tin người dùng thành công!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi cập nhật: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // Admin/AdminUser/Delete
        [HttpPost]
        public ActionResult Delete(int id)
        {
            var user = db.Users.Find(id);
            if (user == null)
            {
                TempData["Error"] = "Người dùng không tồn tại!";
                return RedirectToAction("Index");
            }

            if (user.IsAdmin)
            {
                TempData["Error"] = "Bảo mật hệ thống: Không thể xóa tài khoản Quản trị viên (Admin)!";
                return RedirectToAction("Index");
            }

            string deletedName = user.Username ?? user.Email;

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var marketPostIds = db.MarketPosts.Where(p => p.UserID == id).Select(p => p.PostID).ToList();
                    var connectPostIds = db.ConnectPosts.Where(p => p.UserID == id).Select(p => p.PostID).ToList();

                    // Notifications
                    var userNotifs = db.Notifications.Where(n => n.UserID == id).ToList();
                    db.Notifications.RemoveRange(userNotifs);

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

                    // Reviews
                    var relatedReviews = db.Reviews.Where(r => r.UserID == id
                        || (r.PostType == "Market" && r.PostID.HasValue && marketPostIds.Contains(r.PostID.Value))
                        || (r.PostType == "Connect" && r.PostID.HasValue && connectPostIds.Contains(r.PostID.Value))).ToList();
                    db.Reviews.RemoveRange(relatedReviews);

                    // Wishlists
                    var wishlists = db.MarketWishlists.Where(w => w.UserID == id
                        || (w.PostID.HasValue && marketPostIds.Contains(w.PostID.Value))).ToList();
                    db.MarketWishlists.RemoveRange(wishlists);

                    // MarketChatRooms
                    var marketRooms = db.MarketChatRooms.Where(r => r.BuyerID == id
                        || r.SellerID == id
                        || (r.PostID.HasValue && marketPostIds.Contains(r.PostID.Value))).ToList();
                    db.MarketChatRooms.RemoveRange(marketRooms);

                    // MarketImages
                    var marketImages = db.MarketImages.Where(img => img.PostID.HasValue && marketPostIds.Contains(img.PostID.Value)).ToList();
                    db.MarketImages.RemoveRange(marketImages);

                    // MarketPosts
                    var marketPosts = db.MarketPosts.Where(p => p.UserID == id).ToList();
                    db.MarketPosts.RemoveRange(marketPosts);

                    // ConnectPosts
                    var connectPosts = db.ConnectPosts.Where(p => p.UserID == id).ToList();
                    db.ConnectPosts.RemoveRange(connectPosts);

                    // ChatMessages (unlink ReplyToID first)
                    var userMessageIds = db.ChatMessages.Where(m => m.SenderID == id).Select(m => m.MessageID).ToList();
                    if (userMessageIds.Any())
                    {
                        var dependentReplies = db.ChatMessages.Where(m => m.ReplyToID.HasValue && userMessageIds.Contains(m.ReplyToID.Value)).ToList();
                        foreach (var dr in dependentReplies)
                        {
                            dr.ReplyToID = null;
                        }
                        db.SaveChanges();
                    }
                    var userMessages = db.ChatMessages.Where(m => m.SenderID == id).ToList();
                    db.ChatMessages.RemoveRange(userMessages);

                    // ChatParticipants
                    var participants = db.ChatParticipants.Where(p => p.UserID == id).ToList();
                    db.ChatParticipants.RemoveRange(participants);

                    // Delete User
                    db.Users.Remove(user);
                    db.SaveChanges();

                    transaction.Commit();
                    TempData["Success"] = $"Đã xóa thành công tài khoản \"{deletedName}\" và toàn bộ dữ liệu liên quan ({marketPosts.Count} bài chợ, {connectPosts.Count} bài kết nối, {userMessages.Count} tin nhắn)!";
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    TempData["Error"] = "Lỗi khi xóa người dùng: " + ex.Message;
                }
            }

            return RedirectToAction("Index");
        }

        // Dọn dẹp kết nối Database
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