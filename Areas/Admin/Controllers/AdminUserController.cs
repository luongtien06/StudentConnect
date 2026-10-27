using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.Collections.Generic;
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
                    user.Avatar = DefaultImagePlaceholders.AvatarPath;
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
        public ActionResult Edit(User user, string newPassword = null, string confirmPassword = null)
        {
            bool isAjax = false;
            try { isAjax = Request != null && Request.IsAjaxRequest(); } catch { }

            if (user == null || user.UserID <= 0)
            {
                if (isAjax) return Json(new { success = false, message = "Mã người dùng không hợp lệ!" });
                TempData["Error"] = "Mã người dùng không hợp lệ!";
                return RedirectToAction("Index");
            }

            try
            {
                var existingUser = db.Users.Find(user.UserID);
                if (existingUser == null)
                {
                    if (isAjax) return Json(new { success = false, message = "Người dùng không tồn tại!" });
                    TempData["Error"] = "Người dùng không tồn tại!";
                    return RedirectToAction("Index");
                }

                string currentRole = Session["AdminRole"] as string ?? "";

                // Phân quyền bảo mật: Chỉ Quản trị viên tối cao (SuperAdmin) mới có quyền chỉnh sửa tài khoản Quản trị viên
                if (currentRole != "SuperAdmin" && existingUser.IsAdmin)
                {
                    if (isAjax) return Json(new { success = false, message = "Chỉ Quản trị viên tối cao mới có quyền chỉnh sửa tài khoản Quản trị viên!" });
                    TempData["Error"] = "Chỉ Quản trị viên tối cao mới có quyền chỉnh sửa tài khoản Quản trị viên!";
                    return RedirectToAction("Index");
                }

                string cleanUsername = (user.Username ?? "").Trim();
                string cleanEmail = (user.Email ?? "").Trim();

                if (string.IsNullOrWhiteSpace(cleanUsername))
                {
                    if (isAjax) return Json(new { success = false, message = "Tên tài khoản không được để trống!" });
                    TempData["Error"] = "Tên tài khoản không được để trống!";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(cleanEmail))
                {
                    if (isAjax) return Json(new { success = false, message = "Email không được để trống!" });
                    TempData["Error"] = "Email không được để trống!";
                    return RedirectToAction("Index");
                }

                // Kiểm tra trùng Email với tài khoản khác
                var dupEmail = db.Users.FirstOrDefault(u => u.UserID != user.UserID && u.Email.ToLower() == cleanEmail.ToLower());
                if (dupEmail != null)
                {
                    if (isAjax) return Json(new { success = false, message = "Email này đã được sử dụng bởi tài khoản khác!" });
                    TempData["Error"] = "Email này đã được sử dụng bởi tài khoản khác!";
                    return RedirectToAction("Index");
                }

                // Kiểm tra trùng Username với tài khoản khác
                var dupUsername = db.Users.FirstOrDefault(u => u.UserID != user.UserID && u.Username.ToLower() == cleanUsername.ToLower());
                if (dupUsername != null)
                {
                    if (isAjax) return Json(new { success = false, message = "Tên đăng nhập này đã được sử dụng bởi tài khoản khác!" });
                    TempData["Error"] = "Tên đăng nhập này đã được sử dụng bởi tài khoản khác!";
                    return RedirectToAction("Index");
                }

                existingUser.Username = cleanUsername;
                existingUser.Email = cleanEmail;
                if (user.PhoneNumber != null)
                {
                    existingUser.PhoneNumber = user.PhoneNumber;
                }
                existingUser.IsTDMUStudent = user.IsTDMUStudent;

                // Chỉ SuperAdmin mới được phân quyền hoặc thay đổi cờ Quản trị viên
                if (currentRole == "SuperAdmin")
                {
                    existingUser.IsAdmin = user.IsAdmin;
                    existingUser.AdminRole = user.IsAdmin ? (string.IsNullOrEmpty(user.AdminRole) ? "AdminUser" : user.AdminRole) : null;
                }

                // Xử lý đổi mật khẩu:
                // Nếu cả hai trường đều để trống -> Giữ nguyên mật khẩu hiện tại (không thay đổi PasswordHash)
                bool passwordChanged = false;
                string trimmedNewPwd = (newPassword ?? "").Trim();
                string trimmedConfirmPwd = (confirmPassword ?? "").Trim();

                if (!string.IsNullOrEmpty(trimmedNewPwd) || !string.IsNullOrEmpty(trimmedConfirmPwd))
                {
                    if (string.IsNullOrEmpty(trimmedNewPwd) || string.IsNullOrEmpty(trimmedConfirmPwd))
                    {
                        if (isAjax) return Json(new { success = false, message = "Vui lòng nhập đầy đủ cả mật khẩu mới và xác nhận mật khẩu mới!" });
                        TempData["Error"] = "Vui lòng nhập đầy đủ cả mật khẩu mới và xác nhận mật khẩu mới!";
                        return RedirectToAction("Index");
                    }

                    if (trimmedNewPwd != trimmedConfirmPwd)
                    {
                        if (isAjax) return Json(new { success = false, message = "Mật khẩu xác nhận không khớp!" });
                        TempData["Error"] = "Mật khẩu xác nhận không khớp!";
                        return RedirectToAction("Index");
                    }

                    // Quy tắc mật khẩu mạnh của hệ thống (tối thiểu 8 ký tự, có chữ cái, số và ký tự đặc biệt)
                    var hasNumber = new System.Text.RegularExpressions.Regex(@"[0-9]+");
                    var hasChar = new System.Text.RegularExpressions.Regex(@"[a-zA-Z]+");
                    var hasSymbols = new System.Text.RegularExpressions.Regex(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]");

                    if (trimmedNewPwd.Length < 8 ||
                        !hasNumber.IsMatch(trimmedNewPwd) ||
                        !hasChar.IsMatch(trimmedNewPwd) ||
                        !hasSymbols.IsMatch(trimmedNewPwd))
                    {
                        if (isAjax) return Json(new { success = false, message = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm cả chữ cái, số và ký tự đặc biệt!" });
                        TempData["Error"] = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm cả chữ cái, số và ký tự đặc biệt!";
                        return RedirectToAction("Index");
                    }

                    // Băm bằng PasswordHelper chuẩn (PBKDF2 với Salt 128-bit SHA-256)
                    existingUser.PasswordHash = PasswordHelper.HashPassword(trimmedNewPwd);
                    passwordChanged = true;
                }

                db.SaveChanges();

                string successMsg = passwordChanged
                    ? "Cập nhật thông tin và đổi mật khẩu người dùng thành công!"
                    : "Cập nhật thông tin người dùng thành công!";

                if (isAjax) return Json(new { success = true, message = successMsg, passwordChanged = passwordChanged });
                TempData["Success"] = successMsg;
            }
            catch (Exception ex)
            {
                string errMsg = "Có lỗi xảy ra khi cập nhật: " + ex.Message;
                if (isAjax) return Json(new { success = false, message = errMsg });
                TempData["Error"] = errMsg;
            }

            return RedirectToAction("Index");
        }

        // Admin/AdminUser/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
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

        // Admin/AdminUser/BatchAssignRole (Chỉ dành riêng cho SuperAdmin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BatchAssignRole(List<int> userIds, string scope, string sourceRole, string targetRole)
        {
            try
            {
                string currentRole = Session["AdminRole"] as string ?? "";
                if (currentRole != "SuperAdmin")
                {
                    return Json(new { success = false, message = "Chỉ Quản trị viên tối cao (SuperAdmin) mới có quyền phân vai trò theo nhóm!" });
                }

                if (string.IsNullOrEmpty(targetRole))
                {
                    return Json(new { success = false, message = "Vui lòng chọn vai trò mới cần phân bổ!" });
                }

                var validRoles = new[] { "User", "SuperAdmin", "AdminUser", "AdminMarket", "AdminConnect", "adminFeedStory" };
                if (!validRoles.Contains(targetRole))
                {
                    return Json(new { success = false, message = "Vai trò đích không hợp lệ trong hệ thống!" });
                }

                string myEmail = (Session["AdminEmail"] as string ?? "").Trim().ToLower();

                IQueryable<User> query = db.Users;

                if (scope == "selected")
                {
                    if (userIds == null || !userIds.Any())
                    {
                        return Json(new { success = false, message = "Không có người dùng nào được chọn để phân quyền!" });
                    }
                    userIds = userIds.Distinct().ToList();
                    query = query.Where(u => userIds.Contains(u.UserID));
                }
                else if (scope == "tdmu")
                {
                    query = query.Where(u => u.IsTDMUStudent == true);
                }
                else if (scope == "guest")
                {
                    query = query.Where(u => u.IsTDMUStudent == false);
                }
                else if (scope == "by_role")
                {
                    if (sourceRole == "User")
                    {
                        query = query.Where(u => u.IsAdmin == false);
                    }
                    else if (sourceRole == "AllAdmin")
                    {
                        query = query.Where(u => u.IsAdmin == true);
                    }
                    else if (!string.IsNullOrEmpty(sourceRole))
                    {
                        query = query.Where(u => u.IsAdmin == true && u.AdminRole == sourceRole);
                    }
                    else
                    {
                        return Json(new { success = false, message = "Vui lòng chọn nhóm vai trò hiện tại cần chuyển đổi!" });
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Phạm vi phân quyền không hợp lệ!" });
                }

                var targetUsers = query.ToList();

                if (!targetUsers.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng nào phù hợp với phạm vi đã chọn." });
                }

                // Bảo vệ tài khoản SuperAdmin của người đang đăng nhập
                int skippedSelf = 0;
                if (targetRole != "SuperAdmin")
                {
                    var selfUser = targetUsers.FirstOrDefault(u => !string.IsNullOrEmpty(u.Email) && u.Email.Trim().ToLower() == myEmail);
                    if (selfUser != null)
                    {
                        targetUsers.Remove(selfUser);
                        skippedSelf = 1;
                    }
                }

                if (!targetUsers.Any())
                {
                    return Json(new { success = false, message = "Bạn không thể tự hạ quyền Quản trị tối cao của chính tài khoản mình đang sử dụng!" });
                }

                bool isNewAdmin = targetRole != "User";
                string newAdminRole = isNewAdmin ? targetRole : null;

                foreach (var user in targetUsers)
                {
                    user.IsAdmin = isNewAdmin;
                    user.AdminRole = newAdminRole;
                }

                db.SaveChanges();

                string roleTitle = targetRole == "User" ? "Người dùng (User)"
                    : targetRole == "SuperAdmin" ? "Admin tối cao (SuperAdmin)"
                    : targetRole == "AdminUser" ? "Admin Quản lý Người dùng"
                    : targetRole == "AdminMarket" ? "Admin Quản lý Chợ"
                    : targetRole == "AdminConnect" ? "Admin Quản lý Kết nối"
                    : targetRole == "adminFeedStory" ? "Admin Quản lý Feed và Tin"
                    : targetRole;

                string note = skippedSelf > 0 ? " (Đã tự động giữ nguyên quyền cho tài khoản của bạn)" : "";

                return Json(new
                {
                    success = true,
                    count = targetUsers.Count,
                    targetRole = targetRole,
                    roleTitle = roleTitle,
                    message = $"Đã phân quyền thành công vai trò [{roleTitle}] cho {targetUsers.Count} người dùng!{note}"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi phân quyền hàng loạt: " + ex.Message });
            }
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