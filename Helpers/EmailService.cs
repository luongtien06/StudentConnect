using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace StudentConnect.Helpers
{
    /// <summary>
    /// Dịch vụ gửi email bảo mật của TDMUEcoSystem.
    /// Lấy cấu hình SMTP từ Web.config / AppSettings.config và KHÔNG hardcode thông tin bí mật.
    /// </summary>
    public static class EmailService
    {
        private static string SmtpHost => ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
        private static int SmtpPort => int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out int p) ? p : 587;
        private static bool SmtpEnableSsl => bool.TryParse(ConfigurationManager.AppSettings["SmtpEnableSsl"], out bool ssl) ? ssl : true;
        private static string SmtpEmail => ConfigurationManager.AppSettings["SmtpEmail"] ?? string.Empty;
        private static string SmtpPassword => ConfigurationManager.AppSettings["SmtpPassword"] ?? string.Empty;
        private static string SmtpDisplayName => ConfigurationManager.AppSettings["SmtpDisplayName"] ?? "TDMUEcoSystem - Nền tảng Sinh viên TDMU";

        /// <summary>
        /// Gửi mã xác thực OTP đăng ký tài khoản sinh viên.
        /// </summary>
        public static bool SendRegistrationOtp(string toEmail, string username, string otp, out string errorMessage)
        {
            string subject = "Mã xác thực kích hoạt tài khoản Sinh viên TDMU";
            string bodyHtml = BuildOtpEmailBody(
                username: username,
                otp: otp,
                purposeTitle: "KÍCH HOẠT TÀI KHOẢN SINH VIÊN TDMU",
                purposeDesc: "Bạn vừa đăng ký tài khoản trên nền tảng kết nối cộng đồng sinh viên TDMUEcoSystem. Dưới đây là mã xác thực OTP của bạn:"
            );

            return SendEmail(toEmail, subject, bodyHtml, out errorMessage);
        }

        /// <summary>
        /// Gửi mã OTP khôi phục mật khẩu.
        /// </summary>
        public static bool SendPasswordResetOtp(string toEmail, string username, string otp, out string errorMessage)
        {
            string subject = "Mã xác thực đặt lại mật khẩu - TDMUEcoSystem";
            string bodyHtml = BuildOtpEmailBody(
                username: username,
                otp: otp,
                purposeTitle: "ĐẶT LẠI MẬT KHẨU TÀI KHOẢN",
                purposeDesc: "Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản sinh viên của bạn. Dưới đây là mã xác thực OTP để tiến hành khôi phục:"
            );

            return SendEmail(toEmail, subject, bodyHtml, out errorMessage);
        }

        /// <summary>
        /// Gửi email qua SMTP với thông tin cấu hình từ AppSettings.
        /// </summary>
        private static bool SendEmail(string toEmail, string subject, string bodyHtml, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(SmtpEmail) || string.IsNullOrWhiteSpace(SmtpPassword))
            {
                errorMessage = "Hệ thống chưa được cấu hình thông tin gửi email SMTP. Vui lòng liên hệ quản trị viên.";
                return false;
            }

            try
            {
                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(SmtpEmail, SmtpDisplayName, Encoding.UTF8);
                    mail.To.Add(toEmail);
                    mail.Subject = subject;
                    mail.SubjectEncoding = Encoding.UTF8;
                    mail.Body = bodyHtml;
                    mail.BodyEncoding = Encoding.UTF8;
                    mail.IsBodyHtml = true;

                    using (var client = new SmtpClient(SmtpHost, SmtpPort))
                    {
                        client.EnableSsl = SmtpEnableSsl;
                        client.DeliveryMethod = SmtpDeliveryMethod.Network;
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential(SmtpEmail, SmtpPassword);
                        client.Timeout = 15000; // 15 giây timeout
                        client.Send(mail);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // Chỉ ghi nhận thông điệp lỗi chung, tuyệt đối không in mật khẩu hoặc thông tin nhạy cảm
                errorMessage = "Không thể gửi email OTP (Lỗi: " + ex.Message + "). Vui lòng thử lại sau.";
                return false;
            }
        }

        /// <summary>
        /// Tạo giao diện HTML cho email thông báo mã OTP.
        /// </summary>
        private static string BuildOtpEmailBody(string username, string otp, string purposeTitle, string purposeDesc)
        {
            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Xác thực mã OTP - TDMUEcoSystem</title>
</head>
<body style=""margin:0; padding:0; background-color:#0d111a; font-family:'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color:#ffffff;"">
    <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""background-color:#0d111a; padding:40px 10px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" border=""0"" cellspacing=""0"" cellpadding=""0"" style=""max-width:560px; background-color:#161c2e; border:1px solid #28324e; border-radius:16px; overflow:hidden; box-shadow:0 12px 40px rgba(0,0,0,0.5);"">
                    <!-- Header -->
                    <tr>
                        <td style=""background:linear-gradient(135deg, #0d6efd 0%, #00c8ff 100%); padding:28px 32px; text-align:center;"">
                            <h1 style=""margin:0; font-size:22px; color:#ffffff; font-weight:700; letter-spacing:0.04em;"">TDMUEcoSystem</h1>
                            <p style=""margin:6px 0 0 0; font-size:13px; color:rgba(255,255,255,0.85); text-transform:uppercase; letter-spacing:0.08em;"">{purposeTitle}</p>
                        </td>
                    </tr>
                    <!-- Content -->
                    <tr>
                        <td style=""padding:32px 32px 24px 32px;"">
                            <p style=""font-size:15px; color:#e2e8f0; margin-top:0; line-height:1.6;"">
                                Xin chào <b>{WebUtility.HtmlEncode(username ?? "Sinh viên TDMU")}</b>,
                            </p>
                            <p style=""font-size:14px; color:#94a3b8; line-height:1.6; margin-bottom:24px;"">
                                {purposeDesc}
                            </p>

                            <!-- OTP Box -->
                            <div style=""background-color:#0b0f19; border:1px dashed #00c8ff; border-radius:12px; padding:22px; text-align:center; margin:24px 0;"">
                                <span style=""display:block; font-size:11px; text-transform:uppercase; color:#00c8ff; letter-spacing:0.12em; margin-bottom:8px; font-weight:600;"">MÃ XÁC THỰC OTP (6 CHỮ SỐ)</span>
                                <span style=""display:inline-block; font-family:'Courier New', Courier, monospace; font-size:36px; font-weight:bold; letter-spacing:10px; color:#38bdf8; text-shadow:0 0 12px rgba(56,189,248,0.4);"">{otp}</span>
                                <p style=""margin:10px 0 0 0; font-size:12px; color:#f59e0b;"">⏱️ Mã có hiệu lực trong vòng <b>5 phút</b></p>
                            </div>

                            <!-- Security Notes -->
                            <div style=""background-color:rgba(239,68,68,0.08); border-left:4px solid #ef4444; border-radius:4px; padding:12px 16px; margin-top:20px;"">
                                <p style=""margin:0; font-size:12px; color:#fca5a5; line-height:1.5;"">
                                    <b>LƯU Ý BẢO MẬT:</b> Tuyệt đối không chia sẻ mã này cho bất kỳ ai, kể cả quản trị viên hệ thống. Đội ngũ TDMUEcoSystem sẽ không bao giờ yêu cầu bạn cung cấp mã xác thực.
                                </p>
                            </div>

                            <p style=""font-size:13px; color:#64748b; margin-top:24px; line-height:1.5;"">
                                Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email hoặc thông báo ngay cho ban quản trị.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#0f1422; padding:18px 32px; border-top:1px solid #1e263d; text-align:center;"">
                            <p style=""margin:0; font-size:12px; color:#64748b;"">
                                © 2026 TDMUEcoSystem — Nền tảng kết nối cộng đồng sinh viên Đại học Thủ Dầu Một.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }
    }
}
