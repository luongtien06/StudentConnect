import re

path_helper = r"E:\LTWEB\StudentConnect\Helpers\ChatFileModerationHelper.cs"
with open(path_helper, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace("\r\n", "\n")

# 1. Replace ModerateImageWithAiAsync body
old_ai_method = """        /// <summary>
        /// Kiểm duyệt nội dung ảnh bằng dịch vụ trí tuệ nhân tạo thực tế (Google Gemini Vision API)
        /// Tuân thủ nguyên tắc: Nếu chưa cấu hình hoặc dịch vụ không phản hồi, báo rõ tình trạng, KHÔNG giả lập kết quả.
        /// </summary>
        public static async Task<(string status, bool isSafe, string reason)> ModerateImageWithAiAsync(string imagePath, string mimeType)
        {
            string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"]
                         ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return ("NOT_CONFIGURED", true, "Chưa cấu hình khóa API dịch vụ AI kiểm duyệt ảnh (GeminiApiKey).");
            }

            apiKey = apiKey.Trim().Trim('"', '\\'');

            byte[] fileBytes;
            try
            {
                fileBytes = File.ReadAllBytes(imagePath);
            }
            catch (Exception ex)
            {
                return ("ERROR", false, "Không thể đọc dữ liệu ảnh để gửi kiểm duyệt: " + ex.Message);
            }

            // Nếu ảnh quá lớn để gửi qua API base64 nhanh (trên 8MB), bỏ qua quét AI để tránh nghẽn mạng
            if (fileBytes.Length > 8 * 1024 * 1024)
            {
                return ("SERVICE_UNAVAILABLE", true, "Ảnh dung lượng lớn (>8MB), bỏ qua kiểm duyệt AI từ xa.");
            }

            string base64Data = Convert.ToBase64String(fileBytes);
            string prompt = "Analyze this image for content safety in an educational student chat network. " +
                           "Check for: adult/nsfw, violence/gore, hate/harassment, dangerous weapons, illegal drugs. " +
                           "Respond with JSON: {\\\"isSafe\\\": true or false, \\\"flaggedCategory\\\": \\\"none\\\" or category name, \\\"reason\\\": \\\"brief explanation\\\"}";

            var payload = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["parts"] = new JArray
                        {
                            new JObject { ["text"] = prompt },
                            new JObject
                            {
                                ["inlineData"] = new JObject
                                {
                                    ["mimeType"] = mimeType,
                                    ["data"] = base64Data
                                }
                            }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json"
                }
            };

            var candidateModels = new[] { "gemini-3.5-flash", "gemini-3.5-flash-lite", "gemini-flash-latest" };

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(7); // Giới hạn 7s timeout
                foreach (var model in candidateModels)
                {
                    try
                    {
                        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
                        using (var req = new HttpRequestMessage(HttpMethod.Post, endpoint))
                        {
                            req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                            req.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                            using (var resp = await client.SendAsync(req))
                            {
                                if (resp.IsSuccessStatusCode)
                                {
                                    string jsonResp = await resp.Content.ReadAsStringAsync();
                                    var jobj = JObject.Parse(jsonResp);
                                    string text = (string)jobj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"];
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        var parsed = JObject.Parse(text);
                                        bool isSafe = (bool)(parsed["isSafe"] ?? true);
                                        string reason = (string)parsed["reason"] ?? (isSafe ? "Ảnh an toàn." : "Phát hiện nội dung nhạy cảm/không phù hợp.");
                                        string cat = (string)parsed["flaggedCategory"] ?? "none";

                                        return (isSafe ? "SAFE" : "FLAGGED", isSafe, $"{cat}: {reason}");
                                    }
                                }
                            }
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        return ("TIMEOUT", false, "Dịch vụ AI kiểm duyệt ảnh phản hồi quá thời gian quy định (Timeout 7s).");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ChatFileModeration] AI model {model} attempt failed: {ex.Message}");
                    }
                }
            }

            // Nếu không thể kết nối hoặc lỗi dịch vụ: Báo rõ trạng thái và từ chối an toàn (Fail-Closed)
            return ("ERROR", false, "Dịch vụ AI kiểm duyệt đám mây gặp sự cố hoặc không thể kết nối.");
        }"""

new_ai_method = """        /// <summary>
        /// Kiểm duyệt nội dung ảnh bằng AI: ĐÃ LOẠI BỎ hoàn toàn để không tiêu hao hạn ngạch (quota) Gemini.
        /// Toàn bộ tệp và hình ảnh được bảo vệ bởi các lớp bảo mật cục bộ (Magic Bytes, GDI+ Bitmap Decode, Antivirus, Archive Inspection).
        /// Phương thức này giữ lại chữ ký để đảm bảo tính tương thích và trả về ngay kết quả vô hiệu hóa mà không tạo bất kỳ HTTP request nào.
        /// </summary>
        public static Task<(string status, bool isSafe, string reason)> ModerateImageWithAiAsync(string imagePath, string mimeType)
        {
            // Trả về ngay lập tức, không gửi bất kỳ HTTP request hay tiêu hao quota nào tới Google Gemini API
            return Task.FromResult(("DISABLED_LOCAL_ONLY", true, "Đã loại bỏ Gemini khỏi quy trình kiểm duyệt tệp để tối ưu quota; toàn bộ tệp được bảo vệ bởi các lớp bảo mật cục bộ."));
        }"""

if old_ai_method in content:
    content = content.replace(old_ai_method, new_ai_method)
    print("Replaced ModerateImageWithAiAsync")
else:
    print("WARNING: old_ai_method not exact match, using regex search")
    pattern = r"/// <summary>\s*/// Kiểm duyệt nội dung ảnh bằng dịch vụ trí tuệ nhân tạo.*?return \(\"ERROR\", false, \"Dịch vụ AI kiểm duyệt đám mây gặp sự cố hoặc không thể kết nối\.\"\);\s*}"
    content, count = re.subn(pattern, new_ai_method.strip(), content, flags=re.DOTALL)
    print(f"Regex replacement count: {count}")

# 2. Update ValidateAndModerateFileAsync step 7
old_step7 = """            // 7. Kiểm duyệt nội dung ảnh bằng AI thực tế (nếu là ảnh)
            if (result.IsImage)
            {
                var aiMod = await ModerateImageWithAiAsync(stagedFilePath, result.DetectedMimeType);
                result.ImageModerationStatus = aiMod.status;
                result.ImageModerationReason = aiMod.reason;
                audit.ImageModerationStatus = $"{aiMod.status} ({aiMod.reason})";

                if (aiMod.status == "FLAGGED")
                {
                    result.ErrorCode = "CONTENT_UNSAFE";
                    result.UserFriendlyMessage = $"Hình ảnh '{originalFileName}' bị từ chối do vi phạm quy chuẩn cộng đồng: {aiMod.reason}";
                    audit.FinalDecision = "REJECTED";
                    audit.DecisionReason = $"AI Moderation flagged: {aiMod.reason}";
                    LogModerationAudit(audit);
                    SafeDeleteFile(stagedFilePath);
                    return result;
                }
                else if (aiMod.status == "TIMEOUT" || aiMod.status == "ERROR")
                {
                    result.ErrorCode = "AI_MODERATION_FAILED";
                    result.UserFriendlyMessage = $"Không thể hoàn tất kiểm duyệt hình ảnh do lỗi dịch vụ AI ({aiMod.reason}). Tệp bị từ chối an toàn.";
                    audit.FinalDecision = "REJECTED";
                    audit.DecisionReason = $"AI Moderation service error/timeout: {aiMod.reason}";
                    LogModerationAudit(audit);
                    SafeDeleteFile(stagedFilePath);
                    return result;
                }
            }
            else
            {
                result.ImageModerationStatus = "NOT_APPLICABLE";
                audit.ImageModerationStatus = "NOT_APPLICABLE (Document)";
            }"""

new_step7 = """            // 7. Kiểm duyệt nội dung ảnh bằng AI: ĐÃ LOẠI BỎ (Không gọi Gemini Vision API để tiết kiệm quota)
            // Toàn bộ quy trình dựa 100% vào các lớp bảo mật cục bộ (Magic Bytes, GDI+ Bitmap Decode, Antivirus, Archive Inspection).
            // Lưu ý: Sau thay đổi này, hệ thống kiểm tra an toàn kỹ thuật và mã độc, không còn AI phân tích nội dung ngữ nghĩa của ảnh.
            result.ImageModerationStatus = "DISABLED_LOCAL_ONLY";
            result.ImageModerationReason = "Đã loại bỏ Gemini khỏi quy trình kiểm duyệt tệp để tối ưu quota; toàn bộ tệp được bảo vệ bởi các lớp bảo mật cục bộ.";
            audit.ImageModerationStatus = "DISABLED_LOCAL_ONLY";"""

if old_step7 in content:
    content = content.replace(old_step7, new_step7)
    print("Replaced step 7")
else:
    print("WARNING: old_step7 not exact match, using regex search")
    pattern_step7 = r"// 7\. Kiểm duyệt nội dung ảnh bằng AI thực tế.*?audit\.ImageModerationStatus = \"NOT_APPLICABLE \(Document\)\";\s*}"
    content, count_step7 = re.subn(pattern_step7, new_step7, content, flags=re.DOTALL)
    print(f"Regex replacement step7 count: {count_step7}")

# Update step 8 approval text
content = content.replace(
    'audit.DecisionReason = "All checks passed (Signature, Antivirus, AI Moderation).";',
    'audit.DecisionReason = "All local security checks passed (Signature, Antivirus, Archive Inspection).";'
)

content = content.replace("\n", "\r\n")

with open(path_helper, "w", encoding="utf-8") as f:
    f.write(content)

print("Updated Helpers/ChatFileModerationHelper.cs successfully")

# Also update comment in Controllers/ChatController.cs
path_ctrl = r"E:\LTWEB\StudentConnect\Controllers\ChatController.cs"
with open(path_ctrl, "r", encoding="utf-8") as f:
    ctrl_content = f.read()

ctrl_content = ctrl_content.replace(
    "// Bước 2: Thực hiện chu trình kiểm duyệt toàn diện (Chữ ký Magic Bytes, Antivirus, AI Vision)",
    "// Bước 2: Thực hiện chu trình kiểm duyệt toàn diện bằng các lớp bảo mật cục bộ (Chữ ký Magic Bytes, Antivirus, GDI+, Archive Inspection)"
)

with open(path_ctrl, "w", encoding="utf-8") as f:
    f.write(ctrl_content)

print("Updated Controllers/ChatController.cs successfully")
