with open('Helpers/SocialHelper.cs', 'r', encoding='utf-8') as f:
    text = f.read()

pos_start = text.find('public static string FormatTimeAgo')
pos_end = text.find('public static List<int> GetFriendIds')
if pos_start >= 0 and pos_end >= 0:
    new_block = '''public static string FormatTimeAgo(DateTime? dt)
        {
            if (!dt.HasValue) return "";

            DateTime target = dt.Value;

            // Xử lý an toàn: Nếu DateTimeKind là Utc, chuyển sang Giờ địa phương (SE Asia Standard Time)
            if (target.Kind == DateTimeKind.Utc)
            {
                target = target.ToLocalTime();
            }

            var span = DateTime.Now - target;

            // An toàn: Thời gian tương lai (do lệch đồng hồ máy chủ) hoặc vừa tạo dưới 60 giây
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
        }

        /// <summary>
        /// Lấy danh sách ID bạn bè đã chấp nhận kết bạn
        /// </summary>
        '''

    updated = text[:pos_start] + new_block + text[pos_end:]
    with open('Helpers/SocialHelper.cs', 'w', encoding='utf-8') as f:
        f.write(updated)
    print("Updated Helpers/SocialHelper.cs successfully!")
else:
    print("Could not locate positions in Helpers/SocialHelper.cs")
