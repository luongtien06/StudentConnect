path = r"E:\LTWEB\StudentConnect\Helpers\ChatFileModerationHelper.cs"
with open(path, "rb") as f:
    raw = f.read()

# Replace all combinations of \r and \n with clean \n
text = raw.decode("utf-8")
text = text.replace("\r\n", "\n").replace("\r", "\n")

# Collapse 3 or more consecutive \n into 2 \n
import re
text = re.sub(r'\n{3,}', '\n\n', text)

new_ai_block = '''        /// <summary>
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

pattern = r"/// <summary>\s*/// Kiểm duyệt nội dung ảnh bằng AI.*?public static Task<\(string status, bool isSafe, string reason\)> ModerateImageWithAiAsync.*?\}"
text, count = re.subn(pattern, new_ai_block.strip(), text, flags=re.DOTALL)
print(f"Regex ai_block count: {count}")

new_step7 = '''            // CẢNH BÁO: Trạng thái DISABLED_LOCAL_ONLY biểu thị bỏ qua bước phân tích AI để tối ưu quota,
            // KHÔNG ĐƯỢC hiểu nhầm là ảnh đã được xác nhận an toàn về mặt nội dung đạo đức/cộng đồng.
            result.ImageModerationStatus = "DISABLED_LOCAL_ONLY";
            result.ImageModerationReason = "Kiểm duyệt nội dung ngữ nghĩa bằng AI đã tắt để tối ưu quota; tệp chỉ được xác thực an toàn kỹ thuật cục bộ, không xác nhận an toàn nội dung.";
            audit.ImageModerationStatus = "DISABLED_LOCAL_ONLY (Content unverified by AI)";'''

pattern2 = r'result\.ImageModerationStatus = "DISABLED_LOCAL_ONLY";.*?audit\.ImageModerationStatus = "DISABLED_LOCAL_ONLY";'
text, count2 = re.subn(pattern2, new_step7.strip(), text, flags=re.DOTALL)
print(f"Regex step7 count: {count2}")

text = text.replace(
    'audit.DecisionReason = "All local security checks passed (Signature, Antivirus, Archive Inspection).";',
    'audit.DecisionReason = "Technical security checks passed (Signature, Antivirus, Archive Inspection). AI semantic content unverified.";'
)

# Convert to standard Windows CRLF
text = text.replace("\n", "\r\n")

with open(path, "wb") as f:
    f.write(text.encode("utf-8"))

print("Cleaned and updated Helpers/ChatFileModerationHelper.cs successfully")
