path_helper = r"E:\LTWEB\StudentConnect\Helpers\ChatFileModerationHelper.cs"
with open(path_helper, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace("\r\n", "\n")

target1 = '''        public static Task<(string status, bool isSafe, string reason)> ModerateImageWithAiAsync(string imagePath, string mimeType)
        {
            // Trả về ngay lập tức, không gửi bất kỳ HTTP request hay tiêu hao quota nào tới Google Gemini API
            return Task.FromResult(("DISABLED_LOCAL_ONLY", true, "Đã loại bỏ Gemini khỏi quy trình kiểm duyệt tệp để tối ưu quota; toàn bộ tệp được bảo vệ bởi các lớp bảo mật cục bộ."));
        }'''

rep1 = '''        /// <summary>
        /// Kiểm duyệt nội dung ảnh bằng AI: ĐÃ LOẠI BỎ hoàn toàn để không tiêu hao hạn ngạch (quota) Gemini.
        /// CẢNH BÁO: Trạng thái DISABLED_LOCAL_ONLY chỉ xác nhận bỏ qua bước phân tích AI để tối ưu chi phí,
        /// KHÔNG ĐƯỢC hiểu nhầm là ảnh đã được xác nhận an toàn về mặt nội dung đạo đức/cộng đồng.
        /// </summary>
        public static Task<(string status, bool isSafe, string reason)> ModerateImageWithAiAsync(string imagePath, string mimeType)
        {
            // Trả về ngay lập tức, không gửi bất kỳ HTTP request hay tiêu hao quota nào tới Google Gemini API.
            // isSafe = false biểu thị nội dung ngữ nghĩa chưa được AI kiểm chứng an toàn (unverified).
            return Task.FromResult(("DISABLED_LOCAL_ONLY", false, "Kiểm duyệt nội dung ngữ nghĩa bằng AI đã tắt để tối ưu quota; toàn bộ tệp chỉ được bảo vệ bởi các lớp bảo mật kỹ thuật cục bộ, không xác nhận an toàn nội dung."));
        }'''

if target1 in content:
    content = content.replace(target1, rep1)
    print("Replaced target1")
else:
    print("WARNING: target1 not found")

target2 = '''            result.ImageModerationStatus = "DISABLED_LOCAL_ONLY";
            result.ImageModerationReason = "Đã loại bỏ Gemini khỏi quy trình kiểm duyệt tệp để tối ưu quota; toàn bộ tệp được bảo vệ bởi các lớp bảo mật cục bộ.";
            audit.ImageModerationStatus = "DISABLED_LOCAL_ONLY";'''

rep2 = '''            // CẢNH BÁO: Trạng thái DISABLED_LOCAL_ONLY biểu thị bỏ qua bước phân tích AI để tối ưu quota,
            // KHÔNG ĐƯỢC hiểu nhầm là ảnh đã được xác nhận an toàn về mặt nội dung đạo đức/cộng đồng.
            result.ImageModerationStatus = "DISABLED_LOCAL_ONLY";
            result.ImageModerationReason = "Kiểm duyệt nội dung ngữ nghĩa bằng AI đã tắt để tối ưu quota; tệp chỉ được xác thực an toàn kỹ thuật cục bộ, không xác nhận an toàn nội dung.";
            audit.ImageModerationStatus = "DISABLED_LOCAL_ONLY (Content unverified by AI)";'''

if target2 in content:
    content = content.replace(target2, rep2)
    print("Replaced target2")
else:
    print("WARNING: target2 not found")

content = content.replace(
    'audit.DecisionReason = "All local security checks passed (Signature, Antivirus, Archive Inspection).";',
    'audit.DecisionReason = "Technical security checks passed (Signature, Antivirus, Archive Inspection). AI semantic content unverified.";'
)

content = content.replace("\n", "\r\n")

with open(path_helper, "w", encoding="utf-8") as f:
    f.write(content)

print("Updated ChatFileModerationHelper.cs successfully")
