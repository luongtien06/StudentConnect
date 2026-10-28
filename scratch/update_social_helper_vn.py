with open('Helpers/SocialHelper.cs', 'r', encoding='utf-8') as f:
    content = f.read()

old_code = """        public static string FormatTimeAgo(DateTime? dt)
        {
            if (!dt.HasValue) return "";

            DateTime target = dt.Value;
            var span = DateTime.Now - target;

            // Nếu dữ liệu có mốc thời gian ở tương lai bất thường (hơn 2 phút), không che giấu bằng "Vừa xong"
            // mà hiển thị thời điểm cụ thể để các bài kiểm thử và người dùng nhận diện chính xác
            if (span.TotalSeconds < -120)
            {
                if (target.Year == DateTime.Now.Year)
                    return $"{target.Day} tháng {target.Month} lúc {target:HH:mm}";
                return $"{target:dd/MM/yyyy} lúc {target:HH:mm}";
            }

            // Khoảng thời gian vừa tạo (bao gồm dung sai trôi đồng hồ nhẹ dưới 2 phút)
            if (span.TotalSeconds < 60)
            {
                return "Vừa xong";
            }
            if (span.TotalMinutes < 60)
            {
                return $"{(int)span.TotalMinutes} phút trước";
            }
            if (span.TotalHours < 24)
            {
                return $"{(int)span.TotalHours} giờ trước";
            }
            if (span.TotalDays < 2)
            {
                return $"Hôm qua lúc {target:HH:mm}";
            }
            if (span.TotalDays < 7)
            {
                return $"{(int)span.TotalDays} ngày trước";
            }
            if (target.Year == DateTime.Now.Year)
            {
                return $"{target.Day} tháng {target.Month} lúc {target:HH:mm}";
            }

            return $"{target:dd/MM/yyyy} lúc {target:HH:mm}";
        }"""

new_code = """        public static string FormatTimeAgo(DateTime? dt)
        {
            if (!dt.HasValue) return "";

            DateTime target = dt.Value;
            // Chuẩn hóa thời điểm hiện tại theo múi giờ Việt Nam (SE Asia Standard Time / UTC+7),
            // độc lập hoàn toàn với múi giờ của máy chủ OS để tránh hiển thị sai lệch khi deploy production
            DateTime now = DateTimeHelper.VietnamNow;
            var span = now - target;

            // Nếu dữ liệu có mốc thời gian ở tương lai bất thường (hơn 2 phút), không che giấu bằng "Vừa xong"
            // mà hiển thị thời điểm cụ thể để các bài kiểm thử và người dùng nhận diện chính xác
            if (span.TotalSeconds < -120)
            {
                if (target.Year == now.Year)
                    return $"{target.Day} tháng {target.Month} lúc {target:HH:mm}";
                return $"{target:dd/MM/yyyy} lúc {target:HH:mm}";
            }

            // Khoảng thời gian vừa tạo (bao gồm dung sai trôi đồng hồ nhẹ dưới 2 phút)
            if (span.TotalSeconds < 60)
            {
                return "Vừa xong";
            }
            if (span.TotalMinutes < 60)
            {
                return $"{(int)span.TotalMinutes} phút trước";
            }
            if (span.TotalHours < 24)
            {
                return $"{(int)span.TotalHours} giờ trước";
            }
            if (span.TotalDays < 2)
            {
                return $"Hôm qua lúc {target:HH:mm}";
            }
            if (span.TotalDays < 7)
            {
                return $"{(int)span.TotalDays} ngày trước";
            }
            if (target.Year == now.Year)
            {
                return $"{target.Day} tháng {target.Month} lúc {target:HH:mm}";
            }

            return $"{target:dd/MM/yyyy} lúc {target:HH:mm}";
        }"""

norm_content = content.replace('\r\n', '\n')
norm_old = old_code.replace('\r\n', '\n')
norm_new = new_code.replace('\r\n', '\n')

if norm_old in norm_content:
    norm_content = norm_content.replace(norm_old, norm_new)
    if '\r\n' in content:
        res = norm_content.replace('\n', '\r\n')
    else:
        res = norm_content
    with open('Helpers/SocialHelper.cs', 'w', encoding='utf-8') as f:
        f.write(res)
    print("SUCCESS: Updated Helpers/SocialHelper.cs")
else:
    print("FAILED: norm_old not in norm_content")
