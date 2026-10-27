using System;
using System.Security.Cryptography;
using System.Text;

namespace StudentConnect.Helpers
{
    /// <summary>
    /// Tiện ích sinh, băm bảo mật và kiểm tra mã OTP.
    /// Tuyệt đối KHÔNG lưu OTP dạng plain-text vào database hay ghi ra log.
    /// </summary>
    public static class OtpHelper
    {
        public const int OtpLength = 6;
        public const int OtpValidityMinutes = 5;
        public const int MaxOtpAttempts = 5;
        public const int ResendCooldownSeconds = 60;

        private const string Pepper = "TDMU_ECOSYSTEM_OTP_PEPPER_V1";

        /// <summary>
        /// Sinh mã OTP 6 chữ số ngẫu nhiên bằng bộ sinh số ngẫu nhiên mật mã (RNGCryptoServiceProvider).
        /// </summary>
        public static string GenerateOtp()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                uint val = BitConverter.ToUInt32(bytes, 0);
                int code = (int)(val % 900000) + 100000; // Phạm vi 100000 -> 999999
                return code.ToString("D6");
            }
        }

        /// <summary>
        /// Băm mã OTP bằng SHA-256 kết hợp với Salt (Email sinh viên) và Pepper bí mật.
        /// </summary>
        public static string HashOtp(string otp, string saltEmail)
        {
            if (string.IsNullOrWhiteSpace(otp)) return null;

            string normalizedSalt = (saltEmail ?? string.Empty).Trim().ToLowerInvariant();
            string raw = $"{otp.Trim()}:{normalizedSalt}:{Pepper}";

            using (var sha = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(raw);
                byte[] hashBytes = sha.ComputeHash(inputBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        /// <summary>
        /// Kiểm tra tính chính xác của OTP bằng so sánh hash theo thời gian cố định (Constant-time comparison).
        /// </summary>
        public static bool VerifyOtp(string inputOtp, string saltEmail, string expectedHash)
        {
            if (string.IsNullOrWhiteSpace(inputOtp) || string.IsNullOrWhiteSpace(expectedHash))
            {
                return false;
            }

            string computedHash = HashOtp(inputOtp, saltEmail);
            return FixedTimeEquals(computedHash, expectedHash);
        }

        /// <summary>
        /// So sánh hai chuỗi hash với thời gian thực thi hằng số nhằm chống tấn công Timing Attack.
        /// </summary>
        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        /// <summary>
        /// Kiểm tra thời gian cooldown giữa 2 lần yêu cầu gửi lại OTP.
        /// Trả về true nếu đã đủ thời gian chờ (>= 60s), false nếu còn trong thời gian chờ.
        /// </summary>
        public static bool CanResend(DateTime? lastSentAt, out int remainingSeconds)
        {
            remainingSeconds = 0;
            if (!lastSentAt.HasValue) return true;

            var elapsed = (DateTime.Now - lastSentAt.Value).TotalSeconds;
            if (elapsed < ResendCooldownSeconds)
            {
                remainingSeconds = (int)Math.Ceiling(ResendCooldownSeconds - elapsed);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Kiểm tra mã OTP còn trong thời hạn sử dụng (5 phút) hay không.
        /// </summary>
        public static bool IsExpired(DateTime? expiry)
        {
            if (!expiry.HasValue) return true;
            return DateTime.Now > expiry.Value;
        }
    }
}
