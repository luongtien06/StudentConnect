using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StudentConnect.Areas.Admin.Controllers
{
    public class AuthController : Controller
    {
        private readonly TDMUEcoSystemEntities db = new TDMUEcoSystemEntities();

        // Admin/Auth/Login
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Login(string csrfExpired = null)
        {
            Response.Cache.SetNoStore();
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));

            if (csrfExpired == "1")
            {
                ViewBag.Error = "Phiên làm việc đã được làm mới an toàn sau khi cập nhật hệ thống. Vui lòng đăng nhập lại.";
            }

            HttpCookie adminCookie = Request.Cookies["AdminLogin"];
            if (adminCookie != null && !string.IsNullOrEmpty(adminCookie.Value))
            {
                string cookieEmail = adminCookie.Value;
                var validAdmin = db.Users.FirstOrDefault(u => u.Email == cookieEmail && u.IsAdmin == true);
                if (validAdmin != null)
                {
                    Session["AdminRole"] = string.IsNullOrEmpty(validAdmin.AdminRole) ? "SuperAdmin" : validAdmin.AdminRole;
                    Session["AdminEmail"] = validAdmin.Email;
                    Session["AdminUsername"] = validAdmin.Username;
                    Session["AdminAvatar"] = validAdmin.Avatar;
                    Session["AdminID"] = validAdmin.UserID;
                    Session["UserID"] = validAdmin.UserID;
                    Session["AdminPreviousLogin"] = validAdmin.LastActivityAt;
                    Session["AdminLoginTime"] = DateTime.Now;
                    validAdmin.LastActivityAt = DateTime.Now;
                    db.SaveChanges();
                    return RedirectToAdminLanding();
                }
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password, bool rememberMe = false)
        {
            var adminUser = db.Users.FirstOrDefault(u => u.Email == username && u.IsAdmin == true);

            if (adminUser != null && PasswordHelper.VerifyPassword(password, adminUser.PasswordHash))
            {
                // Tự động nâng cấp hash MD5 sang PBKDF2 nếu đăng nhập thành công
                if (PasswordHelper.IsLegacyMD5(adminUser.PasswordHash))
                {
                    adminUser.PasswordHash = PasswordHelper.HashPassword(password);
                    db.SaveChanges();
                }

                Session["AdminRole"] = string.IsNullOrEmpty(adminUser.AdminRole) ? "SuperAdmin" : adminUser.AdminRole;
                Session["AdminEmail"] = adminUser.Email;
                Session["AdminUsername"] = adminUser.Username;
                Session["AdminAvatar"] = adminUser.Avatar;
                Session["AdminID"] = adminUser.UserID;
                Session["UserID"] = adminUser.UserID;
                Session["AdminPreviousLogin"] = adminUser.LastActivityAt;
                Session["AdminLoginTime"] = DateTime.Now;
                adminUser.LastActivityAt = DateTime.Now;
                db.SaveChanges();

                if (rememberMe)
                {
                    HttpCookie adminCookie = new HttpCookie("AdminLogin");
                    adminCookie.Value = adminUser.Email;
                    adminCookie.Expires = DateTime.Now.AddDays(30);
                    Response.Cookies.Add(adminCookie);
                }

                return RedirectToAdminLanding();
            }

            ViewBag.Error = "Tài khoản không tồn tại, sai mật khẩu hoặc không có quyền Quản trị!";
            return View();
        }

        // Admin/Auth/Logout
        public ActionResult Logout()
        {
            Session.Remove("AdminRole");
            Session.Remove("AdminEmail");
            Session.Remove("AdminUsername");
            Session.Remove("AdminAvatar");
            Session.Remove("AdminID");
            Session.Remove("UserID");

            if (Request.Cookies["AdminLogin"] != null)
            {
                HttpCookie adminCookie = new HttpCookie("AdminLogin");
                adminCookie.Expires = DateTime.Now.AddDays(-1);
                Response.Cookies.Add(adminCookie);
            }

            return RedirectToAction("Login");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        private ActionResult RedirectToAdminLanding()
        {
            string role = Session["AdminRole"] as string ?? "";
            return role == "adminFeedStory"
                ? RedirectToAction("Index", "AdminFeedStory", new { area = "Admin" })
                : RedirectToAction("Index", "AdminHome", new { area = "Admin" });
        }
    }
}