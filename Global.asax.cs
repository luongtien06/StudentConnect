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
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var exception = Server.GetLastError();
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
