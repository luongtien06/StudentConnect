using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;
using Microsoft.AspNet.SignalR.Hubs;
using StudentConnect.Models;

namespace StudentConnect.Helpers
{
    public static class UserAuthHelper
    {
        public const string CookieName = "SC_UserAuthTicket";
        private const string Purpose = "StudentConnect.UserAuth.v1";
        private const int CookieLifetimeDays = 7;

        /// <summary>
        /// Tạo và ghi Cookie xác thực đã được mã hóa và ký điện tử bằng MachineKey.
        /// </summary>
        public static void SetAuthCookie(HttpResponseBase response, int userId)
        {
            try
            {
                if (response == null || userId <= 0) return;

                // Payload: UserID|ExpiryTicks
                long expiryTicks = DateTime.UtcNow.AddDays(CookieLifetimeDays).Ticks;
                string payload = $"{userId}|{expiryTicks}";
                byte[] plainBytes = Encoding.UTF8.GetBytes(payload);
                byte[] protectedBytes = MachineKey.Protect(plainBytes, Purpose);
                string token = Convert.ToBase64String(protectedBytes);

                var cookie = new HttpCookie(CookieName, token)
                {
                    HttpOnly = true,
                    Path = "/",
                    Expires = DateTime.Now.AddDays(CookieLifetimeDays)
                };

                response.Cookies.Add(cookie);
            }
            catch { }
        }

        /// <summary>
        /// Xóa Cookie xác thực khi đăng xuất.
        /// </summary>
        public static void ClearAuthCookie(HttpResponseBase response)
        {
            try
            {
                if (response == null) return;
                var cookie = new HttpCookie(CookieName, "")
                {
                    HttpOnly = true,
                    Path = "/",
                    Expires = DateTime.Now.AddDays(-2)
                };
                response.Cookies.Add(cookie);
            }
            catch { }
        }

        /// <summary>
        /// Giải mã và trích xuất UserID từ Cookie HTTP Request.
        /// </summary>
        public static int? GetUserIdFromCookie(HttpRequestBase request)
        {
            try
            {
                if (request == null) return null;
                var cookie = request.Cookies[CookieName];
                if (cookie == null || string.IsNullOrEmpty(cookie.Value)) return null;

                byte[] protectedBytes = Convert.FromBase64String(cookie.Value);
                byte[] plainBytes = MachineKey.Unprotect(protectedBytes, Purpose);
                string payload = Encoding.UTF8.GetString(plainBytes);

                var parts = payload.Split('|');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int userId) && userId > 0)
                {
                    if (long.TryParse(parts[1], out long expiryTicks))
                    {
                        if (DateTime.UtcNow.Ticks <= expiryTicks)
                        {
                            return userId;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Giải mã UserID từ SignalR Hub Caller Context.
        /// </summary>
        public static int? GetUserIdFromSignalR(HubCallerContext context)
        {
            try
            {
                if (context == null) return null;

                // 1. Kiểm tra session từ HttpContext (nếu có qua PostAuthorizeRequest)
                if (context.Request.Environment != null && context.Request.Environment.TryGetValue("System.Web.HttpContextBase", out object httpContextObj))
                {
                    var httpContext = httpContextObj as HttpContextBase;
                    if (httpContext?.Session != null && httpContext.Session["UserID"] != null)
                    {
                        int sId = Convert.ToInt32(httpContext.Session["UserID"]);
                        if (sId > 0) return sId;
                    }
                }

                // 2. Kiểm tra cookie trong SignalR Request
                var cookie = context.Request.Cookies[CookieName];
                if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                {
                    byte[] protectedBytes = Convert.FromBase64String(cookie.Value);
                    byte[] plainBytes = MachineKey.Unprotect(protectedBytes, Purpose);
                    string payload = Encoding.UTF8.GetString(plainBytes);

                    var parts = payload.Split('|');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int userId) && userId > 0)
                    {
                        if (long.TryParse(parts[1], out long expiryTicks))
                        {
                            if (DateTime.UtcNow.Ticks <= expiryTicks)
                            {
                                return userId;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Xác thực danh tính người dùng hiện tại từ Session hoặc Cookie, đồng thời đối chiếu với DB.
        /// Trả về true nếu người dùng hợp lệ và gán userId hợp lệ.
        /// </summary>
        public static bool TryGetAuthenticatedUser(HttpSessionStateBase session, HttpRequestBase request, TDMUEcoSystemEntities db, out User authenticatedUser)
        {
            authenticatedUser = null;
            int? detectedUserId = null;

            if (session != null && session["UserID"] != null)
            {
                int sId = Convert.ToInt32(session["UserID"]);
                if (sId > 0) detectedUserId = sId;
            }

            if (!detectedUserId.HasValue && request != null)
            {
                detectedUserId = GetUserIdFromCookie(request);
            }

            if (!detectedUserId.HasValue || detectedUserId.Value <= 0)
            {
                return false;
            }

            if (db == null)
            {
                using (var localDb = new TDMUEcoSystemEntities())
                {
                    authenticatedUser = localDb.Users.FirstOrDefault(u => u.UserID == detectedUserId.Value);
                }
            }
            else
            {
                authenticatedUser = db.Users.FirstOrDefault(u => u.UserID == detectedUserId.Value);
            }

            if (authenticatedUser == null)
            {
                return false;
            }

            // Chặn truy cập nếu tài khoản mới chưa hoàn tất xác thực email sinh viên qua OTP
            // Lưu ý: Tài khoản cũ (IsEmailVerified == null) vẫn được an toàn truy cập theo phương án chuyển tiếp
            if (authenticatedUser.IsEmailVerified == false)
            {
                return false;
            }

            // Đồng bộ lại Session nếu trước đó bị rỗng (ví dụ sau khi mở lại tab hoặc khôi phục từ cookie)
            if (session != null)
            {
                session["UserID"] = authenticatedUser.UserID;
                if (session["Username"] == null) session["Username"] = authenticatedUser.Username;
                if (session["Avatar"] == null) session["Avatar"] = authenticatedUser.SafeAvatar;
            }

            return true;
        }
    }
}
