using System;

namespace StudentConnect.Helpers
{
    /// <summary>
    /// Helper chuẩn hóa múi giờ hệ thống cho toàn bộ ứng dụng StudentConnect / TDMU EcoSystem.
    /// Đảm bảo tính toán thời gian Việt Nam (SE Asia Standard Time / UTC+7) chính xác 100%,
    /// độc lập hoàn toàn với múi giờ OS của máy chủ chạy ứng dụng (Local, UTC, US Central,...).
    /// </summary>
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo _vietnamTimeZone = InitVietnamTimeZone();

        private static TimeZoneInfo InitVietnamTimeZone()
        {
            try
            {
                // Múi giờ chuẩn trên hệ điều hành Windows (SE Asia Standard Time = UTC+7)
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                try
                {
                    // Múi giờ chuẩn IANA trên Linux / macOS / container
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch
                {
                    // Fallback an toàn tuyệt đối với độ lệch cố định +07:00
                    return TimeZoneInfo.CreateCustomTimeZone("SEAsia_UTC7", TimeSpan.FromHours(7), "SE Asia Standard Time", "SE Asia Standard Time");
                }
            }
        }

        /// <summary>
        /// Múi giờ chuẩn Việt Nam (UTC+7 / SE Asia Standard Time).
        /// </summary>
        public static TimeZoneInfo VietnamTimeZone => _vietnamTimeZone;

        /// <summary>
        /// Thời gian hiện tại theo giờ Việt Nam (UTC+7).
        /// Sử dụng thay cho DateTime.Now để không phụ thuộc mù quáng vào múi giờ của máy chủ.
        /// </summary>
        public static DateTime VietnamNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _vietnamTimeZone);

        /// <summary>
        /// Chuyển đổi một mốc thời gian UTC sang giờ Việt Nam.
        /// </summary>
        public static DateTime UtcToVietnam(DateTime utcDateTime)
        {
            DateTime safeUtc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(safeUtc, _vietnamTimeZone);
        }

        /// <summary>
        /// Chuyển đổi một mốc thời gian Việt Nam sang UTC.
        /// </summary>
        public static DateTime VietnamToUtc(DateTime vnDateTime)
        {
            DateTime safeVn = DateTime.SpecifyKind(vnDateTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(safeVn, _vietnamTimeZone);
        }
    }
}
