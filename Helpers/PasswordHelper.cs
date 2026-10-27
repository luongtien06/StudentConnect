using System;
using System.Security.Cryptography;
using System.Text;

namespace StudentConnect.Helpers
{
    public static class PasswordHelper
    {
        private const int SaltSize = 16; // 128 bits
        private const int KeySize = 32;  // 256 bits
        private const int Iterations = 10000;

        /// <summary>
        /// Băm mật khẩu mới sử dụng PBKDF2 với Salt
        /// Dạng lưu trữ: PBKDF2$10000$Base64Salt$Base64Hash
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            using (var algorithm = new Rfc2898DeriveBytes(password, SaltSize, Iterations, HashAlgorithmName.SHA256))
            {
                string salt = Convert.ToBase64String(algorithm.Salt);
                string key = Convert.ToBase64String(algorithm.GetBytes(KeySize));

                return $"PBKDF2${Iterations}${salt}${key}";
            }
        }

        /// <summary>
        /// Kiểm tra mật khẩu đầu vào với chuỗi hash đã lưu trong DB (hỗ trợ cả PBKDF2 mới và MD5 cũ)
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash)) return false;

            // 1. Kiểm tra nếu là hash định dạng PBKDF2 mới
            if (storedHash.StartsWith("PBKDF2$"))
            {
                var parts = storedHash.Split('$');
                if (parts.Length != 4) return false;

                int iterations = int.Parse(parts[1]);
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] key = Convert.FromBase64String(parts[3]);

                using (var algorithm = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                {
                    byte[] keyToCheck = algorithm.GetBytes(KeySize);
                    return SlowEquals(key, keyToCheck);
                }
            }

            // 2. Nếu là hash MD5 cũ (32 ký tự hex)
            string md5Hash = GetMD5(password);
            return string.Equals(md5Hash, storedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Kiểm tra xem chuỗi hash trong DB có phải dạng MD5 cũ cần nâng cấp không
        /// </summary>
        public static bool IsLegacyMD5(string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;
            return !storedHash.StartsWith("PBKDF2$");
        }

        /// <summary>
        /// Hàm băm MD5 legacy
        /// </summary>
        public static string GetMD5(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(str);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static bool SlowEquals(byte[] a, byte[] b)
        {
            uint diff = (uint)a.Length ^ (uint)b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                diff |= (uint)(a[i] ^ b[i]);
            }
            return diff == 0;
        }
    }
}
