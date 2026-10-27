using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace StudentConnect
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Khởi chạy Agent Background Task Scheduler
            StudentConnect.Models.Assistant.AgentSchedulerService.Start();
        }

        protected void Application_PostAuthorizeRequest()
        {
            if (Request.AppRelativeCurrentExecutionFilePath != null &&
                Request.AppRelativeCurrentExecutionFilePath.StartsWith("~/signalr", StringComparison.OrdinalIgnoreCase))
            {
                // Dùng ReadOnly thay vì Required để tránh chiếm giữ Exclusive Session Lock
                // làm nghẽn (deadlock/pending vô hạn) các request MVC và AJAX khác của người dùng
                HttpContext.Current.SetSessionStateBehavior(System.Web.SessionState.SessionStateBehavior.ReadOnly);
            }
        }

        protected void Application_End()
        {
            // Dừng an toàn Background Scheduler khi AppDomain tái khởi động
            StudentConnect.Models.Assistant.AgentSchedulerService.Stop();
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var exception = Server.GetLastError();

            // Tự phục hồi khi Anti-Forgery Token bị mismatch / expired (Deploy IIS, Recycle AppPool, browser cache token lệch)
            var antiForgeryEx = exception as HttpAntiForgeryException 
                ?? (exception != null ? exception.InnerException as HttpAntiForgeryException : null);

            if (antiForgeryEx != null)
            {
                Server.ClearError();
                Response.Clear();

                string cookieName = "__RequestVerificationToken";
                try
                {
                    cookieName = System.Web.Helpers.AntiForgeryConfig.CookieName;
                }
                catch { }

                if (Request.Cookies[cookieName] != null)
                {
                    var expiredCookie = new HttpCookie(cookieName, "")
                    {
                        Expires = DateTime.Now.AddDays(-1),
                        Path = "/"
                    };
                    Response.Cookies.Add(expiredCookie);
                }

                bool isAjax = false;
                try
                {
                    isAjax = new HttpRequestWrapper(Request).IsAjaxRequest();
                }
                catch { }

                if (isAjax)
                {
                    Response.ContentType = "application/json";
                    Response.StatusCode = 200;
                    Response.Write("{\"success\":false,\"message\":\"Phiên làm việc hoặc mã xác thực bảo mật đã hết hạn. Vui lòng làm mới trang.\",\"csrfExpired\":true}");
                    Response.End();
                    return;
                }

                string requestUrl = Request.RawUrl ?? "";
                if (requestUrl.IndexOf("/Admin", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Response.Redirect("~/Admin/Auth/Login?csrfExpired=1");
                    return;
                }

                string returnUrl = Request.UrlReferrer != null ? Request.UrlReferrer.ToString() : "/Home/Index";
                Response.Redirect(returnUrl + (returnUrl.Contains("?") ? "&" : "?") + "csrfExpired=1");
                return;
            }

            if (exception is HttpException httpEx)
            {
                if (httpEx.WebEventCode == System.Web.Management.WebEventCodes.RuntimeErrorPostTooLarge
                    || (httpEx.Message != null && httpEx.Message.Contains("Maximum request length exceeded")))
                {
                    Server.ClearError();
                    Response.Clear();
                    string returnUrl = Request.UrlReferrer != null ? Request.UrlReferrer.ToString() : "/Connect/Index";
                    Response.Redirect(returnUrl + (returnUrl.Contains("?") ? "&" : "?") + "uploadError=tooLarge");
                }
            }
        }
    }
}
