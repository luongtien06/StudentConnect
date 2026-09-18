using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class UserController : Controller
    {
        TDMUEcoSystemEntities db = new TDMUEcoSystemEntities();

        // ==========================================
        // 1. ĐĂNG KÝ TÀI KHOẢN
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User taiKhoanMoi, string confirmPassword)
        {
            if (ModelState.IsValid)
            {
                // 1. Kiểm tra xác nhận mật khẩu
                if (taiKhoanMoi.PasswordHash != confirmPassword)
                {
                    ViewBag.ErrorRegister = "Mật khẩu xác nhận không khớp!";
                    return View(taiKhoanMoi);
                }

                // 2. Kiểm tra mật khẩu mạnh
                var hasNumber = new Regex(@"[0-9]+");
                var hasChar = new Regex(@"[a-zA-Z]+");
                var hasSymbols = new Regex(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]");

                if (taiKhoanMoi.PasswordHash.Length < 8 ||
                    !hasNumber.IsMatch(taiKhoanMoi.PasswordHash) ||
                    !hasChar.IsMatch(taiKhoanMoi.PasswordHash) ||
                    !hasSymbols.IsMatch(taiKhoanMoi.PasswordHash))
                {
                    ViewBag.ErrorRegister = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm cả chữ cái, số và ký tự đặc biệt!";
                    return View(taiKhoanMoi);
                }

                if (!taiKhoanMoi.Email.EndsWith("@student.tdmu.edu.vn"))
                {
                    ViewBag.ErrorRegister = "Chỉ chấp nhận email sinh viên TDMU (@student.tdmu.edu.vn).";
                    return View(taiKhoanMoi);
                }

                var checkEmail = db.Users.FirstOrDefault(u => u.Email == taiKhoanMoi.Email);
                if (checkEmail != null)
                {
                    ViewBag.ErrorRegister = "Email này đã được sử dụng! Vui lòng dùng Email khác.";
                    return View(taiKhoanMoi);
                }

                taiKhoanMoi.PasswordHash = PasswordHelper.HashPassword(taiKhoanMoi.PasswordHash);
                taiKhoanMoi.CreatedAt = DateTime.Now;
                db.Users.Add(taiKhoanMoi);
                db.SaveChanges();

                Session["UserID"] = taiKhoanMoi.UserID;
                Session["Username"] = taiKhoanMoi.Username;
                return RedirectToAction("Index", "Home");
            }
            return View(taiKhoanMoi);
        }

        // ĐĂNG NHẬP
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            if (ModelState.IsValid)
            {
                var checkUser = db.Users.FirstOrDefault(u => u.Email == email);

                if (checkUser != null && PasswordHelper.VerifyPassword(password, checkUser.PasswordHash))
                {
                    // Tự động nâng cấp hash MD5 cũ lên PBKDF2 nếu đăng nhập thành công
                    if (PasswordHelper.IsLegacyMD5(checkUser.PasswordHash))
                    {
                        checkUser.PasswordHash = PasswordHelper.HashPassword(password);
                        db.SaveChanges();
                    }

                    Session["UserID"] = checkUser.UserID;
                    Session["Username"] = checkUser.Username;
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ViewBag.ErrorLogin = "Email hoặc Mật khẩu không chính xác!";
                }
            }
            return View();
        }

        // ĐĂNG XUẤT
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // QUÊN MẬT KHẨU
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            if (!email.EndsWith("@student.tdmu.edu.vn"))
            {
                ViewBag.ErrorMessage = "Chỉ chấp nhận email sinh viên TDMU (@student.tdmu.edu.vn).";
                return View();
            }
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user != null)
            {
                Random rd = new Random();
                string otp = rd.Next(100000, 999999).ToString();

                Session["ResetEmail"] = email;
                Session["OTP"] = otp;

                try
                {
                    string fromEmail = "nguyenphan2006tpk@gmail.com";
                    string appPassword = "gaibfkwazndlbuod";

                    MailMessage mail = new MailMessage();
                    mail.To.Add(email);
                    mail.From = new MailAddress(fromEmail, "Hệ thống TDMU EcoSystem");
                    mail.Subject = "Mã xác minh khôi phục mật khẩu - TDMU EcoSystem";
                    mail.Body = $"Xin chào <b>{user.Username}</b>,<br><br>" +
                                $"Bạn vừa yêu cầu đặt lại mật khẩu. Đây là mã xác minh (OTP) của bạn:<br><br>" +
                                $"<h2 style='color:blue; letter-spacing: 5px;'>{otp}</h2><br>" +
                                $"Vui lòng không chia sẻ mã này cho bất kỳ ai. Mã này dùng để xác minh tài khoản của bạn.<br><br>" +
                                $"Trân trọng,<br>Đội ngũ TDMU EcoSystem.";
                    mail.IsBodyHtml = true;

                    SmtpClient smtp = new SmtpClient("smtp.gmail.com");
                    smtp.EnableSsl = true;
                    smtp.Port = 587;
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Credentials = new NetworkCredential(fromEmail, appPassword);

                    smtp.Send(mail);

                    return RedirectToAction("VerifyOTP");
                }
                catch (Exception ex)
                {
                    ViewBag.ErrorMessage = "Lỗi khi gửi email: " + ex.Message;
                }
            }
            else
            {
                ViewBag.ErrorMessage = "Email này chưa được đăng ký trong hệ thống!";
            }

            return View();
        }

        // QUÊN MẬT KHẨU 
        public ActionResult VerifyOTP()
        {
            if (Session["ResetEmail"] == null)
            {
                return RedirectToAction("ForgotPassword");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyOTP(string otp)
        {
            if (Session["OTP"] != null && Session["OTP"].ToString() == otp)
            {
                return RedirectToAction("ResetPassword");
            }
            else
            {
                ViewBag.ErrorMessage = "Mã OTP không chính xác. Vui lòng kiểm tra lại email!";
                return View();
            }
        }

        // QUÊN MẬT KHẨU 
        public ActionResult ResetPassword()
        {
            if (Session["ResetEmail"] == null || Session["OTP"] == null)
            {
                return RedirectToAction("ForgotPassword");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.ErrorMessage = "Mật khẩu xác nhận không khớp!";
                return View();
            }

            string email = Session["ResetEmail"].ToString();
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user != null)
            {
                user.PasswordHash = PasswordHelper.HashPassword(newPassword);
                db.SaveChanges();

                Session.Remove("ResetEmail");
                Session.Remove("OTP");

                TempData["SuccessMessage"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại với mật khẩu mới.";
                return RedirectToAction("Login");
            }

            ViewBag.ErrorMessage = "Đã xảy ra lỗi, vui lòng thử lại!";
            return View();
        }

        // profile
        public new ActionResult Profile()
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");

            int userId = (int)Session["UserID"];
            var user = db.Users.Find(userId);

            if (user == null) return RedirectToAction("Login", "User");
            return View(user);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(User model, HttpPostedFileBase avatarFile)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");

            int currentUserId = (int)Session["UserID"];
            var user = db.Users.Find(currentUserId);

            if (user == null) return RedirectToAction("Login", "User");

            string email = (model.Email ?? "").Trim().ToLower();

            if (user.IsTDMUStudent && !email.EndsWith("@student.tdmu.edu.vn"))
            {
                TempData["Error"] = "Cập nhật thất bại: Bạn bắt buộc phải sử dụng email sinh viên TDMU (@student.tdmu.edu.vn).";
                return RedirectToAction("Profile");
            }

            bool emailTaken = db.Users.Any(u => u.Email == email && u.UserID != currentUserId);
            if (emailTaken)
            {
                ModelState.AddModelError("Email",
                    "Email này đã được sử dụng bởi một tài khoản khác.");
            }

            if (!ModelState.IsValid)
            {
                user.Username = model.Username;
                user.PhoneNumber = model.PhoneNumber;

                TempData["Error"] = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();

                return View("Profile", user);
            }

            try
            {
                user.Username = model.Username;
                user.Email = email;
                user.PhoneNumber = model.PhoneNumber;

                if (avatarFile != null && avatarFile.ContentLength > 0)
                {
                    string dir = Server.MapPath("~/Content/Uploads/Avatars/");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    string ext = Path.GetExtension(avatarFile.FileName).ToLower();
                    string fileName = "avatar_" + currentUserId + ext;
                    string fullPath = Path.Combine(dir, fileName);

                    avatarFile.SaveAs(fullPath);

                    user.Avatar = "/Content/Uploads/Avatars/" + fileName;
                }

                db.SaveChanges();

                Session["Username"] = user.Username;

                TempData["Success"] = "Cập nhật thông tin thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Profile");
        }
    }
}