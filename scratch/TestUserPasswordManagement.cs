using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using StudentConnect.Areas.Admin.Controllers;
using StudentConnect.Helpers;
using StudentConnect.Models;

namespace StudentConnect.Verification
{
    public class FakeHttpContext : HttpContextBase
    {
        private readonly FakeHttpRequest _request;
        private readonly FakeHttpResponse _response;
        private readonly FakeHttpSessionState _session;
        private readonly System.Security.Principal.IPrincipal _user;

        public FakeHttpContext(int userId, string username, string email, string role = "", bool isAjax = false)
        {
            _request = new FakeHttpRequest(isAjax);
            _response = new FakeHttpResponse();
            _session = new FakeHttpSessionState();
            _session["UserID"] = userId;
            _session["Username"] = username;
            _session["Email"] = email;
            if (!string.IsNullOrEmpty(role))
            {
                _session["AdminRole"] = role;
                _session["Role"] = role;
                _session["IsAdmin"] = true;
                _session["AdminEmail"] = email;
                _session["AdminID"] = userId;
            }

            var identity = new System.Security.Principal.GenericIdentity(username);
            _user = new System.Security.Principal.GenericPrincipal(identity, string.IsNullOrEmpty(role) ? new string[0] : new[] { role, "Admin" });
        }

        public override HttpRequestBase Request => _request;
        public override HttpResponseBase Response => _response;
        public override HttpSessionStateBase Session => _session;
        public override System.Security.Principal.IPrincipal User
        {
            get => _user;
            set { }
        }
        public override HttpServerUtilityBase Server => new FakeHttpServerUtility();
    }

    public class FakeHttpRequest : HttpRequestBase
    {
        private readonly System.Collections.Specialized.NameValueCollection _headers = new System.Collections.Specialized.NameValueCollection();
        private readonly HttpCookieCollection _cookies = new HttpCookieCollection();
        private readonly bool _isAjax;

        public FakeHttpRequest(bool isAjax = false)
        {
            _isAjax = isAjax;
            if (_isAjax)
            {
                _headers["X-Requested-With"] = "XMLHttpRequest";
            }
        }

        public override string this[string key] => _headers[key];
        public override System.Collections.Specialized.NameValueCollection Headers => _headers;
        public override HttpCookieCollection Cookies => _cookies;
        public override string HttpMethod => "POST";
        public override bool IsAuthenticated => true;
        public override Uri Url => new Uri("http://localhost:59174/");
        public override System.Collections.Specialized.NameValueCollection Form => new System.Collections.Specialized.NameValueCollection();
    }

    public class FakeHttpResponse : HttpResponseBase
    {
        public override int StatusCode { get; set; } = 200;
        public override string StatusDescription { get; set; } = "OK";
    }

    public class FakeHttpSessionState : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> _session = new Dictionary<string, object>();

        public override object this[string name]
        {
            get => _session.ContainsKey(name) ? _session[name] : null;
            set => _session[name] = value;
        }

        public override void Remove(string name) => _session.Remove(name);
        public override void Clear() => _session.Clear();
    }

    public class FakeHttpServerUtility : HttpServerUtilityBase
    {
        public override string MapPath(string path)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (path.StartsWith("~/")) path = path.Substring(2);
            return Path.Combine(baseDir, path.Replace('/', '\\'));
        }
    }

    class Program
    {
        static int _pass = 0;
        static int _fail = 0;
        static int _total = 0;

        static void AssertCheck(string id, string desc, bool condition, string detail)
        {
            _total++;
            if (condition)
            {
                _pass++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[PASS] {id}: {desc}");
            }
            else
            {
                _fail++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {id}: {desc}");
            }
            Console.ResetColor();

            if (!string.IsNullOrEmpty(detail))
            {
                Console.WriteLine($"       -> Bằng chứng: {detail}");
            }
        }

        static T CreateController<T>(int userId, string username, string email, string role = "", bool isAjax = false) where T : Controller, new()
        {
            var controller = new T();
            var httpContext = new FakeHttpContext(userId, username, email, role, isAjax);
            controller.ControllerContext = new ControllerContext(httpContext, new RouteData(), controller);
            return controller;
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("================================================================================");
            Console.WriteLine("   BỘ KIỂM THỬ: NÂNG CẤP ĐỔI MẬT KHẨU KHI CHỈNH SỬA NGƯỜI DÙNG (TDMUECOSYSTEM)");
            Console.WriteLine("   Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("================================================================================");

            using (var db = new TDMUEcoSystemEntities())
            {
                // Tìm kiếm tài khoản quản trị viên và người dùng thử nghiệm
                var superAdmin = db.Users.FirstOrDefault(u => u.IsAdmin && u.AdminRole == "SuperAdmin")
                                 ?? db.Users.FirstOrDefault(u => u.IsAdmin);
                var adminUser = db.Users.FirstOrDefault(u => u.IsAdmin && u.AdminRole == "AdminUser");
                var regularUser = db.Users.FirstOrDefault(u => !u.IsAdmin);

                if (superAdmin == null)
                {
                    Console.WriteLine("Lỗi: Không tìm thấy SuperAdmin trong hệ thống!");
                    return;
                }

                Console.WriteLine($"* SuperAdmin: #{superAdmin.UserID} - {superAdmin.Username} ({superAdmin.Email}, Role: {superAdmin.AdminRole})");
                if (adminUser != null)
                    Console.WriteLine($"* AdminUser : #{adminUser.UserID} - {adminUser.Username} ({adminUser.Email}, Role: {adminUser.AdminRole})");
                if (regularUser != null)
                    Console.WriteLine($"* User thường: #{regularUser.UserID} - {regularUser.Username} ({regularUser.Email})");

                // Tạo một tài khoản người dùng test riêng biệt để kiểm thử các tình huống đổi mật khẩu
                string testEmail = "test_pwd_change_" + Guid.NewGuid().ToString("N").Substring(0, 6) + "@student.tdmu.edu.vn";
                string testUsername = "user_test_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                string originalPassword = "InitialPassword@2026";
                string originalHash = PasswordHelper.HashPassword(originalPassword);

                var targetUser = new User
                {
                    Username = testUsername,
                    Email = testEmail,
                    PasswordHash = originalHash,
                    PhoneNumber = "0987654321",
                    IsTDMUStudent = true,
                    IsAdmin = false,
                    AdminRole = null,
                    Avatar = "/Content/Images/default-avatar.png",
                    CreatedAt = DateTime.Now,
                    LastActivityAt = DateTime.Now
                };
                db.Users.Add(targetUser);
                db.SaveChanges();
                int targetId = targetUser.UserID;
                Console.WriteLine($"\n[SETUP] Đã tạo người dùng thử nghiệm: #{targetId} - {testUsername} ({testEmail})");

                try
                {
                    // =========================================================================
                    // MỤC 1: ĐỂ TRỐNG CẢ 2 TRƯỜNG MẬT KHẨU -> GIỮ NGUYÊN MẬT KHẨU HIỆN TẠI
                    // =========================================================================
                    Console.WriteLine("\n>>> [MỤC 1] KIỂM TRA ĐỂ TRỐNG CẢ 2 TRƯỜNG MẬT KHẨU");
                    var adminCtrl = CreateController<AdminUserController>(superAdmin.UserID, superAdmin.Username, superAdmin.Email, "SuperAdmin");
                    
                var editModel1 = new User
                {
                    UserID = targetId,
                    Username = testUsername + "_mod1",
                    Email = testEmail,
                    PhoneNumber = "0911223344",
                    IsTDMUStudent = false,
                    IsAdmin = false,
                    AdminRole = null
                };

                    var res1 = adminCtrl.Edit(editModel1, "", "") as RedirectToRouteResult;
                    db.Entry(targetUser).Reload();

                    bool pass1_1 = (targetUser.Username == testUsername + "_mod1") && (targetUser.PhoneNumber == "0911223344");
                    bool pass1_2 = (targetUser.PasswordHash == originalHash);
                    bool pass1_3 = PasswordHelper.VerifyPassword(originalPassword, targetUser.PasswordHash);

                    AssertCheck("TEST-1.1", "Thông tin người dùng được cập nhật bình thường khi để trống mật khẩu",
                        pass1_1, $"Username={targetUser.Username}, Phone={targetUser.PhoneNumber}");
                    AssertCheck("TEST-1.2", "Mật khẩu cũ (PasswordHash) được giữ nguyên vẹn 100%",
                        pass1_2, $"Hash trước: {originalHash.Substring(0, 25)}... | Hash sau: {targetUser.PasswordHash.Substring(0, 25)}...");
                    AssertCheck("TEST-1.3", "Mật khẩu cũ đăng nhập thành công với VerifyPassword",
                        pass1_3, $"VerifyPassword('{originalPassword}') = {pass1_3}");

                    // =========================================================================
                    // MỤC 2: ĐỔI MẬT KHẨU THÀNH CÔNG VỚI MẬT KHẨU MỚI HỢP LỆ
                    // =========================================================================
                    Console.WriteLine("\n>>> [MỤC 2] ĐỔI MẬT KHẨU HỢP LỆ (MẬT KHẨU MẠNH, KHỚP NHAU)");
                    string newPassword = "NewPassword@2026#Secure";

                    var editModel2 = new User
                    {
                        UserID = targetId,
                        Username = targetUser.Username,
                        Email = targetUser.Email,
                        PhoneNumber = targetUser.PhoneNumber,
                        IsTDMUStudent = targetUser.IsTDMUStudent,
                        IsAdmin = false,
                        AdminRole = null
                    };

                    var res2 = adminCtrl.Edit(editModel2, newPassword, newPassword) as RedirectToRouteResult;
                    db.Entry(targetUser).Reload();

                    bool pass2_1 = targetUser.PasswordHash.StartsWith("PBKDF2$");
                    bool pass2_2 = PasswordHelper.VerifyPassword(newPassword, targetUser.PasswordHash);
                    bool pass2_3 = !PasswordHelper.VerifyPassword(originalPassword, targetUser.PasswordHash);
                    bool pass2_4 = !targetUser.PasswordHash.Contains(newPassword); // Không lưu plain text

                    AssertCheck("TEST-2.1", "Mật khẩu mới được băm bằng chuẩn PBKDF2 với Salt",
                        pass2_1, $"Stored Hash Prefix: {targetUser.PasswordHash.Substring(0, 25)}...");
                    AssertCheck("TEST-2.2", "Mật khẩu mới đăng nhập thành công (VerifyPassword = true)",
                        pass2_2, $"VerifyPassword('{newPassword}') = {pass2_2}");
                    AssertCheck("TEST-2.3", "Mật khẩu cũ KHÔNG CÒN đăng nhập được nữa (VerifyPassword = false)",
                        pass2_3, $"VerifyPassword('{originalPassword}') = {PasswordHelper.VerifyPassword(originalPassword, targetUser.PasswordHash)}");
                    AssertCheck("TEST-2.4", "Tuyệt đối không lưu mật khẩu ở dạng plain text trong DB",
                        pass2_4, "Mật khẩu đã được mã hóa một chiều qua PBKDF2 SHA-256");

                    // =========================================================================
                    // MỤC 3: MẬT KHẨU XÁC NHẬN KHÔNG KHỚP & CHỈ NHẬP 1 TRƯỜNG
                    // =========================================================================
                    Console.WriteLine("\n>>> [MỤC 3] KIỂM TRA MẬT KHẨU KHÔNG KHỚP & THIẾU TRƯỜNG");
                    string currentHashBeforeMismatch = targetUser.PasswordHash;

                    // 3.1 Mismatch
                    var adminCtrlAjax = CreateController<AdminUserController>(superAdmin.UserID, superAdmin.Username, superAdmin.Email, "SuperAdmin", isAjax: true);
                    var resMismatch = adminCtrlAjax.Edit(editModel2, "ValidPass1@abc", "DifferentPass2@xyz") as JsonResult;
                    dynamic mismatchData = resMismatch.Data;
                    bool mismatchSuccess = (bool)mismatchData.GetType().GetProperty("success").GetValue(mismatchData);
                    string mismatchMsg = (string)mismatchData.GetType().GetProperty("message").GetValue(mismatchData);
                    db.Entry(targetUser).Reload();

                    bool pass3_1 = (!mismatchSuccess) && mismatchMsg.Contains("không khớp") && (targetUser.PasswordHash == currentHashBeforeMismatch);
                    AssertCheck("TEST-3.1", "Từ chối khi mật khẩu xác nhận không khớp, giữ nguyên hash hiện tại",
                        pass3_1, $"Success={mismatchSuccess}, Message='{mismatchMsg}'");

                    // 3.2 Nhập mật khẩu mới nhưng bỏ trống xác nhận
                    var resMissingConfirm = adminCtrlAjax.Edit(editModel2, "ValidPass1@abc", "") as JsonResult;
                    dynamic missingConfirmData = resMissingConfirm.Data;
                    bool missingSuccess = (bool)missingConfirmData.GetType().GetProperty("success").GetValue(missingConfirmData);
                    string missingMsg = (string)missingConfirmData.GetType().GetProperty("message").GetValue(missingConfirmData);
                    db.Entry(targetUser).Reload();

                    bool pass3_2 = (!missingSuccess) && missingMsg.Contains("đầy đủ") && (targetUser.PasswordHash == currentHashBeforeMismatch);
                    AssertCheck("TEST-3.2", "Từ chối khi chỉ nhập 1 trong 2 trường mật khẩu",
                        pass3_2, $"Success={missingSuccess}, Message='{missingMsg}'");

                    // =========================================================================
                    // MỤC 4: KIỂM TRA QUY TẮC ĐỘ PHỨC TẠP CỦA MẬT KHẨU (PASSWORD COMPLEXITY)
                    // =========================================================================
                    Console.WriteLine("\n>>> [MỤC 4] KIỂM TRA QUY TẮC MẬT KHẨU MẠNH (TỐI THIỂU 8 KÝ TỰ, CHỮ, SỐ, KÝ TỰ ĐẶC BIỆT)");

                    string[] weakPasswords = new[]
                    {
                        "Ab1!",         // < 8 ký tự
                        "Password!@#",  // Thiếu số
                        "12345678!@#",  // Thiếu chữ cái
                        "Password1234"  // Thiếu ký tự đặc biệt
                    };

                    int weakRejectedCount = 0;
                    foreach (var weak in weakPasswords)
                    {
                        var resWeak = adminCtrlAjax.Edit(editModel2, weak, weak) as JsonResult;
                        dynamic weakData = resWeak.Data;
                        bool wSuccess = (bool)weakData.GetType().GetProperty("success").GetValue(weakData);
                        string wMsg = (string)weakData.GetType().GetProperty("message").GetValue(weakData);
                        if (!wSuccess && wMsg.Contains("8 ký tự"))
                        {
                            weakRejectedCount++;
                        }
                    }

                    bool pass4_1 = (weakRejectedCount == weakPasswords.Length);
                    AssertCheck("TEST-4.1", "Chặn 4/4 mật khẩu yếu vi phạm quy chuẩn độ phức tạp",
                        pass4_1, $"Đã từ chối an toàn {weakRejectedCount}/{weakPasswords.Length} mật khẩu yếu.");

                    // =========================================================================
                    // MỤC 5: BẢO MẬT PHÂN QUYỀN & CHỐNG GIẢ MẠO YÊU CẦU (IDOR / PRIVILEGE ESCALATION)
                    // =========================================================================
                    Console.WriteLine("\n>>> [MỤC 5] BẢO MẬT PHÂN QUYỀN & CHỐNG TẤN CÔNG THAO TÁC TRÁI QUYỀN");

                    // 5.1 Người dùng thông thường gọi Edit -> OnActionExecuting chặn
                    if (regularUser != null)
                    {
                        var unauthCtrl = CreateController<AdminUserController>(regularUser.UserID, regularUser.Username, regularUser.Email, role: "");
                        var filterCtx = new ActionExecutingContext
                        {
                            HttpContext = unauthCtrl.HttpContext,
                            RouteData = new RouteData(),
                            Controller = unauthCtrl
                        };
                        var onAction = typeof(AdminUserController).GetMethod("OnActionExecuting", BindingFlags.Instance | BindingFlags.NonPublic);
                        onAction.Invoke(unauthCtrl, new object[] { filterCtx });

                        bool pass5_1 = (filterCtx.Result != null);
                        AssertCheck("TEST-5.1", "Tài khoản thường bị OnActionExecuting từ chối truy cập",
                            pass5_1, $"Result: {filterCtx.Result?.GetType().Name} (Chặn thành công)");
                    }

                    // 5.2 AdminChat (không có quyền AdminUser) gọi Edit -> OnActionExecuting chặn
                    var adminChatCtrl = CreateController<AdminUserController>(4, "AdminChatUser", "adminchat@tdmu.edu.vn", role: "AdminChat");
                    var filterCtxChat = new ActionExecutingContext
                    {
                        HttpContext = adminChatCtrl.HttpContext,
                        RouteData = new RouteData(),
                        Controller = adminChatCtrl
                    };
                    var onActionChat = typeof(AdminUserController).GetMethod("OnActionExecuting", BindingFlags.Instance | BindingFlags.NonPublic);
                    onActionChat.Invoke(adminChatCtrl, new object[] { filterCtxChat });
                    bool pass5_2 = (filterCtxChat.Result != null);
                    AssertCheck("TEST-5.2", "AdminChat không có quyền AdminUser bị chặn truy cập",
                        pass5_2, $"Result: {filterCtxChat.Result?.GetType().Name} (Chặn thành công)");

                    // 5.3 AdminUser thường cố tình sửa tài khoản SuperAdmin
                    var adminUserCtrl = CreateController<AdminUserController>(1002, "SubAdminUser", "subadmin@tdmu.edu.vn", role: "AdminUser", isAjax: true);
                    var tamperAdminEdit = new User
                    {
                        UserID = superAdmin.UserID,
                        Username = superAdmin.Username,
                        Email = superAdmin.Email
                    };
                    var resTamperAdmin = adminUserCtrl.Edit(tamperAdminEdit, "HackedPassword@123", "HackedPassword@123") as JsonResult;
                    dynamic tamperData = resTamperAdmin.Data;
                    bool tamperSuccess = (bool)tamperData.GetType().GetProperty("success").GetValue(tamperData);
                    string tamperMsg = (string)tamperData.GetType().GetProperty("message").GetValue(tamperData);

                    bool pass5_3 = (!tamperSuccess) && tamperMsg.Contains("Chỉ Quản trị viên tối cao");
                    AssertCheck("TEST-5.3", "Admin thường không thể sửa hoặc đổi mật khẩu của tài khoản Admin/SuperAdmin",
                        pass5_3, $"Success={tamperSuccess}, Message='{tamperMsg}'");

                    // 5.4 Giả mạo UserID không tồn tại
                    var invalidIdEdit = new User
                    {
                        UserID = 9999999,
                        Username = "ghost_user",
                        Email = "ghost@tdmu.edu.vn"
                    };
                    var resInvalidId = adminCtrlAjax.Edit(invalidIdEdit, "GhostPass@1234", "GhostPass@1234") as JsonResult;
                    dynamic invalidIdData = resInvalidId.Data;
                    bool invSuccess = (bool)invalidIdData.GetType().GetProperty("success").GetValue(invalidIdData);
                    string invMsg = (string)invalidIdData.GetType().GetProperty("message").GetValue(invalidIdData);

                    bool pass5_4 = (!invSuccess) && invMsg.Contains("không tồn tại");
                    AssertCheck("TEST-5.4", "Chặn giả mạo UserID không tồn tại (Chống IDOR)",
                        pass5_4, $"Success={invSuccess}, Message='{invMsg}'");

                    // =========================================================================
                    // MỤC 6: HỒI QUY CÁC CHỨC NĂNG QUẢN LÝ NGƯỜI DÙNG KHÁC (INDEX, CREATE, DELETE)
                    // =========================================================================
                    Console.WriteLine("\n>>> [MỤC 6] KIỂM THỬ HỒI QUY CÁC CHỨC NĂNG HIỆN CÓ");

                    // 6.1 Index load danh sách người dùng bình thường
                    var indexRes = adminCtrl.Index() as ViewResult;
                    var userList = indexRes?.Model as List<User>;
                    bool pass6_1 = (userList != null && userList.Any(u => u.UserID == targetId));
                    AssertCheck("TEST-6.1", "Trang Index tải danh sách người dùng đầy đủ và chính xác",
                        pass6_1, $"Số lượng người dùng tải được: {userList?.Count}");

                    // 6.2 Chặn xóa tài khoản Admin (bao gồm cả tài khoản Admin đang đăng nhập)
                    var selfDeleteRes = adminCtrl.Delete(superAdmin.UserID) as RedirectToRouteResult;
                    string selfDelMsg = adminCtrl.TempData["Error"] as string ?? "";
                    bool pass6_2 = (selfDeleteRes != null) && selfDelMsg.Contains("Không thể xóa tài khoản Quản trị viên");
                    AssertCheck("TEST-6.2", "Chặn xóa tài khoản Quản trị viên (Admin)",
                        pass6_2, $"Result={selfDeleteRes?.RouteValues["action"]}, TempData['Error']='{selfDelMsg}'");
                }
                finally
                {
                    // Dọn dẹp tài khoản test đã tạo
                    var cleanUser = db.Users.Find(targetId);
                    if (cleanUser != null)
                    {
                        db.Users.Remove(cleanUser);
                        db.SaveChanges();
                        Console.WriteLine($"\n[CLEANUP] Đã xóa người dùng thử nghiệm #{targetId} khỏi cơ sở dữ liệu.");
                    }
                }
            }

            Console.WriteLine("\n================================================================================");
            Console.WriteLine($"   TỔNG KẾT KẾT QUẢ KIỂM THỬ: {_pass}/{_total} PASS ({_fail} FAIL)");
            Console.WriteLine("================================================================================");
        }
    }
}
