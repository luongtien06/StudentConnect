using System;
using StudentConnect.Helpers;

namespace TestTime
{
    class Program
    {
        static void Main()
        {
            DateTime vnNow = DateTimeHelper.VietnamNow;
            DateTime utcNow = DateTime.UtcNow;
            Console.WriteLine("=== DATETIME HELPER & SOCIAL HELPER TIMEZONE AUDIT ===");
            Console.WriteLine($"VietnamNow: {vnNow:yyyy-MM-dd HH:mm:ss} | UtcNow: {utcNow:yyyy-MM-dd HH:mm:ss}");

            TimeSpan diff = vnNow - utcNow;
            bool tzOk = Math.Abs(diff.TotalHours - 7.0) < 0.05;
            Console.WriteLine($"[{ (tzOk ? "PASS" : "FAIL") }] 0. VietnamNow is exactly UTC+7 (Diff: {diff.TotalHours:F2}h)");

            Test("1. Null Value", null, "");
            Test("2. Exactly VietnamNow", vnNow, "Vừa xong");
            Test("3. 30s in Past", vnNow.AddSeconds(-30), "Vừa xong");
            Test("4. 45s in Future (drift tolerance)", vnNow.AddSeconds(45), "Vừa xong");
            Test("5. 10m in Future (abnormal anomaly)", vnNow.AddMinutes(10), "NOT_VUA_XONG");
            Test("6. 7h in Future (UTC shift anomaly)", vnNow.AddHours(7), "NOT_VUA_XONG");
            Test("7. 5m in Past", vnNow.AddMinutes(-5), "5 phút trước");
            Test("8. 3h in Past", vnNow.AddHours(-3), "3 giờ trước");
            Test("9. 25h in Past (yesterday)", vnNow.AddHours(-25), "Hôm qua lúc");
            Test("10. 4 days in Past", vnNow.AddDays(-4), "4 ngày trước");
            Test("11. Same year earlier", new DateTime(vnNow.Year, 1, 15, 9, 30, 0), "tháng 1 lúc 09:30");
            Test("12. Prior year", new DateTime(2024, 8, 20, 14, 15, 0), "20/08/2024 lúc 14:15");
        }

        static void Test(string name, DateTime? dt, string expected)
        {
            string actual = SocialHelper.FormatTimeAgo(dt);
            bool ok = false;
            if (expected == "NOT_VUA_XONG")
            {
                ok = actual != "Vừa xong" && actual.Length > 0;
            }
            else
            {
                ok = actual.Contains(expected);
            }
            Console.WriteLine($"[{ (ok ? "PASS" : "FAIL") }] {name} -> Output: '{actual}'");
        }
    }
}
