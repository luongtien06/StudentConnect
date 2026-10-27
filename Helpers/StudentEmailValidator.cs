using System;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace StudentConnect.Helpers
{
    /// <summary>
    /// Trình kiểm tra và chuẩn hóa email sinh viên TDMU.
    /// Đảm bảo tên miền chính xác tuyệt đối là @student.tdmu.edu.vn ở backend,
    /// chống lại các kỹ thuật bypass, domain spoofing hoặc substring matching.
    /// </summary>
    public static class StudentEmailValidator
    {
        public const string AllowedDomain = "student.tdmu.edu.vn";
        public const string AllowedSuffix = "@student.tdmu.edu.vn";

        // Regex nghiêm ngặt: ký tự chữ/số/dấu chấm/gạch dưới trước @, kết thúc chính xác bằng @student.tdmu.edu.vn
        private static readonly Regex EmailRegex = new Regex(
            @"^[a-zA-Z0-9._%+-]+@student\.tdmu\.edu\.vn$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Chuẩn hóa địa chỉ email: cắt khoảng trắng đầu cuối và chuyển về chữ thường.
        /// </summary>
        public static string NormalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;
            return email.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Xác thực tính hợp lệ của email sinh viên TDMU.
        /// </summary>
        public static bool IsValid(string rawEmail, out string normalizedEmail, out string errorMessage)
        {
            normalizedEmail = NormalizeEmail(rawEmail);
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                errorMessage = "Vui lòng nhập địa chỉ email sinh viên.";
                return false;
            }

            // 1. Kiểm tra độ dài cơ bản
            if (normalizedEmail.Length > 100)
            {
                errorMessage = "Địa chỉ email quá dài (tối đa 100 ký tự).";
                return false;
            }

            // 2. Kiểm tra định dạng bằng Regex nghiêm ngặt
            if (!EmailRegex.IsMatch(normalizedEmail))
            {
                errorMessage = "Chỉ chấp nhận email sinh viên TDMU có đuôi chính xác là @student.tdmu.edu.vn.";
                return false;
            }

            // 3. Phân tích qua System.Net.Mail.MailAddress để chống bypass ký tự đặc biệt, nhiều dấu @, hoặc domain lồng nhau
            try
            {
                var mailAddress = new MailAddress(normalizedEmail);

                // Kiểm tra Host phải khớp tuyệt đối "student.tdmu.edu.vn"
                if (!string.Equals(mailAddress.Host, AllowedDomain, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Tên miền email không hợp lệ. Chỉ chấp nhận @student.tdmu.edu.vn.";
                    return false;
                }

                // Kiểm tra phần Username trước @ không được rỗng hoặc chứa ký tự nguy hiểm
                if (string.IsNullOrWhiteSpace(mailAddress.User) || mailAddress.User.Contains(" ") || mailAddress.User.Contains("@"))
                {
                    errorMessage = "Tên người dùng trong email không hợp lệ.";
                    return false;
                }
            }
            catch
            {
                errorMessage = "Định dạng email không hợp lệ theo chuẩn Internet Mail.";
                return false;
            }

            return true;
        }
    }
}
