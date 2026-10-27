using System;

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

                            Session["AdminID"] = adminUser.UserID;

                            Session["UserID"] = adminUser.UserID;

                            if (Session["AdminLoginTime"] == null)

                            {

                                Session["AdminPreviousLogin"] = adminUser.LastActivityAt;

                                Session["AdminLoginTime"] = DateTime.Now;

                                adminUser.LastActivityAt = DateTime.Now;

                                db.SaveChanges();

                            }

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

            else

            {

                // Luôn kiểm tra trực tiếp với Database để thu hồi/thay đổi quyền có hiệu lực NGAY LẬP TỨC

                string controllerName = filterContext.ActionDescriptor != null && filterContext.ActionDescriptor.ControllerDescriptor != null ? filterContext.ActionDescriptor.ControllerDescriptor.ControllerName : (filterContext.RouteData != null && filterContext.RouteData.Values.ContainsKey("controller") ? filterContext.RouteData.Values["controller"]?.ToString() : "");

                if (!string.Equals(controllerName, "Auth", StringComparison.OrdinalIgnoreCase))

                {

                    int? adminId = Session["AdminID"] as int? ?? Session["UserID"] as int?;

                    string email = Session["AdminEmail"] as string;

                    if (adminId.HasValue || !string.IsNullOrEmpty(email))

                    {

                        using (var dbCheck = new TDMUEcoSystemEntities())

                        {

                                                        var liveAdmin = adminId.HasValue

                                ? dbCheck.Users.FirstOrDefault(u => u.UserID == adminId.Value)

                                : dbCheck.Users.FirstOrDefault(u => u.Email == email);

                                                        if (liveAdmin == null || liveAdmin.IsAdmin != true)

                            {

                                Session.Clear();

                                if (filterContext.HttpContext.Request.IsAjaxRequest())

                                {

                                    filterContext.Result = new JsonResult

                                    {

                                        Data = new { success = false, message = "Quyền quản trị của bạn đã bị thu hồi. Vui lòng đăng nhập lại!" },

                                        JsonRequestBehavior = JsonRequestBehavior.AllowGet

                                    };

                                }

                                else

                                {

                                    TempData["ErrorMessage"] = "Quyền quản trị của bạn đã bị thu hồi.";

                                    filterContext.Result = new RedirectToRouteResult(

                                        new RouteValueDictionary(new { controller = "Auth", action = "Login", area = "Admin" }));

                                }

                                return;

                            }

                            string liveRole = string.IsNullOrEmpty(liveAdmin.AdminRole) ? "SuperAdmin" : liveAdmin.AdminRole;

                            Session["AdminRole"] = liveRole;

                        }

                    }

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

            if (currentRole.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||

                (allowedRoles != null && allowedRoles.Any(r => string.Equals(r, currentRole, StringComparison.OrdinalIgnoreCase))))

            {

                return true;

            }

            if (filterContext.HttpContext.Request.IsAjaxRequest())

            {

                filterContext.Result = new JsonResult

                {

                    Data = new { success = false, message = "Bạn không có quyền truy cập vào chức năng này!" },

                    JsonRequestBehavior = JsonRequestBehavior.AllowGet

                };

                return false;

            }

            TempData["ErrorMessage"] = "Bạn không có quyền truy cập vào chức năng này!";

            filterContext.Result = new RedirectToRouteResult(

                new RouteValueDictionary(new { controller = "AdminHome", action = "Index", area = "Admin" }));

            return false;

        }

    }

}
