using StudentConnect.Models;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace StudentConnect.Areas.Admin.Controllers
{
    public class AdminBaseController : StudentConnect.Controllers.BaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Kiểm tra Session
            if (Session["AdminRole"] == null)
            {
                // Nếu không có Session, tìm Cookie Ghi nhớ và XÁC THỰC VỚI DATABASE
                HttpCookie adminCookie = filterContext.HttpContext.Request.Cookies["AdminLogin"];
                bool isAuthenticated = false;

                if (adminCookie != null && !string.IsNullOrEmpty(adminCookie.Value))
                {
                    string email = adminCookie.Value;
                    using (var db = new TDMUEcoSystemEntities())
                    {
                        var adminUser = db.Users.FirstOrDefault(u => u.Email == email && u.IsAdmin == true);
                        if (adminUser != null)
                        {
                            Session["AdminRole"] = string.IsNullOrEmpty(adminUser.AdminRole) ? "SuperAdmin" : adminUser.AdminRole;
                            Session["AdminEmail"] = adminUser.Email;
                            Session["AdminUsername"] = adminUser.Username;
                            Session["AdminAvatar"] = adminUser.Avatar;
                            isAuthenticated = true;
                        }
                    }
                }

                if (!isAuthenticated)
                {
                    // Nếu cookie không hợp lệ hoặc không có quyền Admin, xóa cookie giả mạo (nếu có) và đá về trang Login
                    if (adminCookie != null)
                    {
                        HttpCookie invalidCookie = new HttpCookie("AdminLogin");
                        invalidCookie.Expires = System.DateTime.Now.AddDays(-1);
                        filterContext.HttpContext.Response.Cookies.Add(invalidCookie);
                    }

                    filterContext.Result = new RedirectToRouteResult(
                        new RouteValueDictionary(new { controller = "Auth", action = "Login", area = "Admin" }));
                }
            }
            base.OnActionExecuting(filterContext);
        }

        /// <summary>
        /// Kiểm tra quyền truy cập theo vai trò.
        /// SuperAdmin luôn có toàn quyền.
        /// Các vai trò khác phải nằm trong danh sách allowedRoles.
        /// </summary>
        protected bool AuthorizeRole(ActionExecutingContext filterContext, params string[] allowedRoles)
        {
            string currentRole = Session["AdminRole"] as string ?? "";
            if (currentRole == "SuperAdmin" || (allowedRoles != null && allowedRoles.Contains(currentRole)))
            {
                return true;
            }

            TempData["ErrorMessage"] = "Bạn không có quyền truy cập vào chức năng này!";
            filterContext.Result = new RedirectToRouteResult(
                new RouteValueDictionary(new { controller = "AdminHome", action = "Index", area = "Admin" }));
            return false;
        }
    }
}