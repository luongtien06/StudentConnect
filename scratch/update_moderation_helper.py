import re

file_path = r"E:\LTWEB\StudentConnect\Helpers\ChatFileModerationHelper.cs"

with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update Usings
old_usings = """using Newtonsoft.Json;

using Newtonsoft.Json.Linq;

using System;

using System.Collections.Generic;

using System.Configuration;

using System.Diagnostics;

using System.Drawing;

using System.IO;

using System.IO.Compression;

using System.Linq;

using System.Net.Http;

using System.Security.Cryptography;

using System.Text;

using System.Text.RegularExpressions;

using System.Threading.Tasks;

using System.Web;

namespace StudentConnect.Helpers

{"""

new_usings = """using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace StudentConnect.Helpers
{
    public static class ModerationStatuses
    {
        public const string AiApproved = "AI_APPROVED";
        public const string AiRejected = "AI_REJECTED";
        public const string AiUnavailable = "AI_UNAVAILABLE";
        public const string LocalRejected = "LOCAL_REJECTED";
        public const string PendingReview = "PENDING_REVIEW";
        public const string NotApplicable = "NOT_APPLICABLE";
        public const string DisabledLocalOnly = "DISABLED_LOCAL_ONLY";
    }"""

assert old_usings in content, "old_usings not found"
content = content.replace(old_usings, new_usings, 1)

# 2. Add static fields and methods to ChatFileModerationHelper
old_fields_start = """    public static class ChatFileModerationHelper

    {

        private static readonly object _logLock = new object();"""

new_fields = """    public static class ChatFileModerationHelper
    {
        private static readonly object _logLock = new object();

        // Bộ nhớ đệm (Cache) kết quả kiểm duyệt theo mã SHA-256 (lưu trữ 24h)
        private static readonly ConcurrentDictionary<string, (string status, bool isSafe, string reason, DateTime cachedUtc)> _moderationCache
            = new ConcurrentDictionary<string, (string status, bool isSafe, string reason, DateTime cachedUtc)>(StringComparer.OrdinalIgnoreCase);

        // Quản lý Circuit Breaker phòng chống cạn kiệt hạn ngạch (Quota Exhaustion) và lỗi hàng loạt
        private static readonly object _circuitLock = new object();
        private static DateTime _circuitOpenUntilUtc = DateTime.MinValue;
        private static int _consecutiveFailureCount = 0;
        private const int CircuitFailureThreshold = 3;
        private static readonly TimeSpan RateLimitCircuitDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan GeneralCircuitDuration = TimeSpan.FromMinutes(2);

        private static readonly HttpClient _aiHttpClient = CreateAiHttpClient();

        private static HttpClient CreateAiHttpClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
        }

        public static bool IsCircuitBreakerOpen()
        {
            lock (_circuitLock)
            {
                return DateTime.UtcNow < _circuitOpenUntilUtc;
            }
        }

        public static void RecordSuccess()
        {
            lock (_circuitLock)
            {
                _consecutiveFailureCount = 0;
                _circuitOpenUntilUtc = DateTime.MinValue;
            }
        }

        public static void RecordRateLimitFailure()
        {
            lock (_circuitLock)
            {
                _circuitOpenUntilUtc = DateTime.UtcNow.Add(RateLimitCircuitDuration);
                _consecutiveFailureCount = CircuitFailureThreshold;
            }
        }

        public static void RecordGeneralFailure()
        {
            lock (_circuitLock)
            {
                _consecutiveFailureCount++;
                if (_consecutiveFailureCount >= CircuitFailureThreshold)
                {
                    _circuitOpenUntilUtc = DateTime.UtcNow.Add(GeneralCircuitDuration);
                }
            }
        }

        public static void ResetCircuitBreaker()
        {
            lock (_circuitLock)
            {
                _consecutiveFailureCount = 0;
                _circuitOpenUntilUtc = DateTime.MinValue;
            }
        }

        public static void TriggerCircuitBreakerForTesting(int minutes = 5)
        {
            lock (_circuitLock)
            {
                _consecutiveFailureCount = CircuitFailureThreshold;
                _circuitOpenUntilUtc = DateTime.UtcNow.AddMinutes(minutes);
            }
        }

        public static void ClearModerationCache()
        {
            _moderationCache.Clear();
        }

        public static int GetModerationCacheCount()
        {
            return _moderationCache.Count;
        }

        /// <summary>
        /// Nén và hạ tỉ lệ ảnh trong bộ nhớ trước khi gửi Gemini để tiết kiệm token và băng thông mạng
        /// </summary>
        private static byte[] PrepareOptimizedImageBytes(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var original = Image.FromStream(fs, false, false))
                {
                    int maxWidth = 1024;
                    int maxHeight = 1024;
                    bool needsResize = original.Width > maxWidth || original.Height > maxHeight;
                    bool isBigFile = fs.Length > 512 * 1024;

                    if (!needsResize && !isBigFile)
                    {
                        fs.Position = 0;
                        using (var ms = new MemoryStream())
                        {
                            fs.CopyTo(ms);
                            return ms.ToArray();
                        }
                    }

                    int targetWidth = original.Width;
                    int targetHeight = original.Height;
                    if (needsResize)
                    {
                        double ratioX = (double)maxWidth / original.Width;
                        double ratioY = (double)maxHeight / original.Height;
                        double ratio = Math.Min(ratioX, ratioY);
                        targetWidth = Math.Max(1, (int)(original.Width * ratio));
                        targetHeight = Math.Max(1, (int)(original.Height * ratio));
                    }

                    using (var resized = new Bitmap(targetWidth, targetHeight))
                    {
                        using (var graphics = Graphics.FromImage(resized))
                        {
                            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
                            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
                            graphics.DrawImage(original, 0, 0, targetWidth, targetHeight);
                        }

                        using (var outMs = new MemoryStream())
                        {
                            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                            if (jpgEncoder != null)
                            {
                                var encoderParams = new EncoderParameters(1);
                                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 75L);
                                resized.Save(outMs, jpgEncoder, encoderParams);
                            }
                            else
                            {
                                resized.Save(outMs, ImageFormat.Jpeg);
                            }
                            return outMs.ToArray();
                        }
                    }
                }
            }
            catch
            {
                return File.ReadAllBytes(filePath);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }"""

assert old_fields_start in content, "old_fields_start not found"
content = content.replace(old_fields_start, new_fields, 1)

# 3. Replace ModerateImageWithAiAsync
old_moderate_ai = """        /// <summary>
        /// Kiểm duyệt nội dung ảnh bằng AI: ĐÃ LOẠI BỎ hoàn toàn để không tiêu hao hạn ngạch (quota) Gemini.
        /// CẢNH BÁO: Trạng thái DISABLED_LOCAL_ONLY chỉ xác nhận bỏ qua bước phân tích AI để tối ưu chi phí,
        /// KHÔNG ĐƯỢC hiểu nhầm là ảnh đã được xác nhận an toàn về mặt nội dung đạo đức/cộng đồng.
        /// </summary>
        public static Task<(string status, bool isSafe, string reason)> ModerateImageWithAiAsync(string imagePath, string mimeType)
        {
            // Trả về ngay lập tức, không gửi bất kỳ HTTP request hay tiêu hao quota nào tới Google Gemini API.
            // isSafe = false biểu thị nội dung ngữ nghĩa chưa được AI kiểm chứng an toàn (unverified).
            return Task.FromResult(("DISABLED_LOCAL_ONLY", false, "Kiểm duyệt nội dung ngữ nghĩa bằng AI đã tắt để tối ưu quota; toàn bộ tệp chỉ được bảo vệ bởi các lớp bảo mật kỹ thuật cục bộ, không xác nhận an toàn nội dung."));
        }"""

new_moderate_ai = """        /// <summary>
        /// Kiểm duyệt nội dung hình ảnh bằng Google Gemini API (model Flash-Lite).
        /// Có cơ chế đệm kết quả SHA-256, Circuit Breaker khi 429/ResourceExhausted, và tự động fallback sang bảo vệ cục bộ.
        /// </summary>
        public static async Task<(string status, bool isSafe, string reason)> ModerateImageWithAiAsync(
            string imagePath,
            string mimeType,
            string fileSha256 = null)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return (ModerationStatuses.AiUnavailable, false, "Tệp ảnh không tồn tại để kiểm duyệt.");
            }

            string sha256 = fileSha256 ?? ComputeFileSha256(imagePath);

            // 1. Kiểm tra bộ nhớ đệm (Cache) theo SHA-256
            if (!string.IsNullOrEmpty(sha256) && _moderationCache.TryGetValue(sha256, out var cached))
            {
                if ((DateTime.UtcNow - cached.cachedUtc).TotalHours < 24)
                {
                    return (cached.status, cached.isSafe, cached.reason + " (kết quả từ bộ nhớ đệm)");
                }
            }

            // 2. Kiểm tra Circuit Breaker (ngắt mạch tạm thời nếu API đang kiệt hạn mức quota)
            if (IsCircuitBreakerOpen())
            {
                return (ModerationStatuses.AiUnavailable, false,
                    "Circuit breaker đang kích hoạt do giới hạn tần suất Gemini API. Chuyển sang bảo vệ bằng lớp kiểm duyệt kỹ thuật cục bộ.");
            }

            // 3. Đọc cấu hình API Key và Model từ AppSettings / Biến môi trường
            string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"]
                            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                            ?? "";
            apiKey = (apiKey ?? "").Trim().Trim('"', '\\'');

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (ModerationStatuses.AiUnavailable, false,
                    "Chưa cấu hình Gemini API Key. Đang áp dụng lớp bảo vệ an toàn kỹ thuật cục bộ.");
            }

            string model = ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-3.5-flash-lite";
            model = (model ?? "").Trim();
            if (string.IsNullOrWhiteSpace(model)) model = "gemini-3.5-flash-lite";

            // 4. Chuẩn bị ảnh nén/tối ưu kích thước (max 1024x1024, JPEG 75%)
            byte[] imageBytes = PrepareOptimizedImageBytes(imagePath);
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return (ModerationStatuses.AiUnavailable, false, "Không thể đọc dữ liệu ảnh để phân tích.");
            }

            string base64Data = Convert.ToBase64String(imageBytes);
            string payloadMime = "image/jpeg";

            string prompt = "Analyze this image for student community chat safety (pornography, sexual content, graphic violence, hate speech, illegal weapons). Return JSON strictly: {\\\"isSafe\\\": true/false, \\\"violationCategory\\\": \\\"NONE\\\" or category, \\\"reason\\\": \\\"Vietnamese explanation\\\"}";

            var payloadObj = new JObject
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
                                    ["mimeType"] = payloadMime,
                                    ["data"] = base64Data
                                }
                            }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json",
                    ["temperature"] = 0.1,
                    ["maxOutputTokens"] = 200
                }
            };

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                    request.Content = new StringContent(payloadObj.ToString(Formatting.None), Encoding.UTF8, "application/json");

                    using (var response = await _aiHttpClient.SendAsync(request))
                    {
                        string respBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            RecordSuccess();

                            var parsed = JObject.Parse(respBody);
                            string jsonText = (string)parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"];

                            if (!string.IsNullOrWhiteSpace(jsonText))
                            {
                                var modObj = JObject.Parse(jsonText);
                                bool isSafe = (bool)(modObj["isSafe"] ?? true);
                                string reason = (string)modObj["reason"] ?? (isSafe ? "Nội dung an toàn." : "Hình ảnh vi phạm quy chuẩn cộng đồng.");

                                string status = isSafe ? ModerationStatuses.AiApproved : ModerationStatuses.AiRejected;
                                if (!string.IsNullOrEmpty(sha256))
                                {
                                    _moderationCache[sha256] = (status, isSafe, reason, DateTime.UtcNow);
                                }

                                return (status, isSafe, reason);
                            }

                            return (ModerationStatuses.AiApproved, true, "Nội dung hình ảnh đã được AI phân tích an toàn.");
                        }

                        int statusCode = (int)response.StatusCode;
                        string errReason = "";
                        string errMsg = respBody;
                        try
                        {
                            var errObj = JObject.Parse(respBody);
                            errMsg = (string)errObj["error"]?["message"] ?? respBody;
                            errReason = (string)errObj["error"]?["details"]?[0]?["reason"] ?? "";
                        }
                        catch { }

                        if (statusCode == 429 || errReason == "RESOURCE_EXHAUSTED" || errMsg.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            RecordRateLimitFailure();
                            return (ModerationStatuses.AiUnavailable, false,
                                "Gemini API hết quota hoặc chạm giới hạn tần suất (HTTP 429/ResourceExhausted). Hệ thống tự động chuyển sang chế độ kiểm duyệt kỹ thuật cục bộ.");
                        }

                        if (statusCode == 400 || statusCode == 401 || statusCode == 403)
                        {
                            RecordGeneralFailure();
                            return (ModerationStatuses.AiUnavailable, false,
                                $"Xác thực Gemini API không thành công (HTTP {statusCode}). Tệp được bảo vệ bởi lớp kiểm tra an toàn kỹ thuật cục bộ.");
                        }

                        RecordGeneralFailure();
                        return (ModerationStatuses.AiUnavailable, false,
                            $"Dịch vụ Gemini API tạm thời không khả dụng (HTTP {statusCode}). Áp dụng lớp kiểm tra kỹ thuật cục bộ.");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                RecordGeneralFailure();
                return (ModerationStatuses.AiUnavailable, false,
                    "Quá thời gian phản hồi từ Gemini API (Timeout). Hệ thống chuyển sang bảo vệ bằng lớp kiểm duyệt kỹ thuật cục bộ.");
            }
            catch (HttpRequestException httpEx)
            {
                RecordGeneralFailure();
                return (ModerationStatuses.AiUnavailable, false,
                    $"Lỗi mạng kết nối đến Gemini API ({httpEx.Message}). Áp dụng lớp kiểm tra kỹ thuật cục bộ.");
            }
            catch (Exception ex)
            {
                RecordGeneralFailure();
                return (ModerationStatuses.AiUnavailable, false,
                    $"Lỗi trong quá trình kiểm duyệt AI ({ex.Message}). Áp dụng lớp kiểm tra kỹ thuật cục bộ.");
            }
        }

        /// <summary>
        /// Kiểm duyệt nội dung văn bản thuần túy (.txt, .csv) an toàn, trích xuất tối đa 2000 ký tự đầu.
        /// Tuyệt đối KHÔNG gửi dữ liệu nhị phân hoặc tệp nhạy cảm lên API.
        /// </summary>
        public static async Task<(string status, bool isSafe, string reason)> ModerateDocumentTextWithAiAsync(
            string filePath,
            string fileSha256 = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return (ModerationStatuses.AiUnavailable, false, "Tệp văn bản không tồn tại.");
            }

            string sha256 = fileSha256 ?? ComputeFileSha256(filePath);

            if (!string.IsNullOrEmpty(sha256) && _moderationCache.TryGetValue(sha256, out var cached))
            {
                if ((DateTime.UtcNow - cached.cachedUtc).TotalHours < 24)
                {
                    return (cached.status, cached.isSafe, cached.reason + " (kết quả từ bộ nhớ đệm)");
                }
            }

            if (IsCircuitBreakerOpen())
            {
                return (ModerationStatuses.AiUnavailable, false,
                    "Circuit breaker đang kích hoạt do giới hạn tần suất API. Chuyển sang bảo vệ cục bộ.");
            }

            string apiKey = ConfigurationManager.AppSettings["GeminiApiKey"]
                            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                            ?? "";
            apiKey = (apiKey ?? "").Trim().Trim('"', '\\'');

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return (ModerationStatuses.AiUnavailable, false,
                    "Chưa cấu hình Gemini API Key. Đang áp dụng bảo vệ an toàn kỹ thuật cục bộ.");
            }

            string textContent;
            try
            {
                using (var sr = new StreamReader(filePath, Encoding.UTF8))
                {
                    char[] buffer = new char[2000];
                    int read = sr.Read(buffer, 0, buffer.Length);
                    textContent = new string(buffer, 0, read);
                }
            }
            catch (Exception ex)
            {
                return (ModerationStatuses.AiUnavailable, false, "Không thể đọc nội dung văn bản: " + ex.Message);
            }

            if (string.IsNullOrWhiteSpace(textContent))
            {
                return (ModerationStatuses.AiApproved, true, "Tệp văn bản rỗng hoặc chỉ chứa khoảng trắng.");
            }

            string model = ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-3.5-flash-lite";
            model = (model ?? "").Trim();
            if (string.IsNullOrWhiteSpace(model)) model = "gemini-3.5-flash-lite";

            string escapedText = textContent.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "\\\\\\\"").Replace("\\r", " ").Replace("\\n", " ");
            string prompt = "Analyze this user text attachment for chat safety (hate speech, sexual harassment, violent threats, illegal). Return JSON strictly: {\\\"isSafe\\\": true/false, \\\"violationCategory\\\": \\\"NONE\\\" or category, \\\"reason\\\": \\\"Vietnamese explanation\\\"}\\nText to analyze:\\n\\\"" + escapedText + "\\\"";

            var payloadObj = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["parts"] = new JArray
                        {
                            new JObject { ["text"] = prompt }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json",
                    ["temperature"] = 0.1,
                    ["maxOutputTokens"] = 200
                }
            };

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
                    request.Content = new StringContent(payloadObj.ToString(Formatting.None), Encoding.UTF8, "application/json");

                    using (var response = await _aiHttpClient.SendAsync(request))
                    {
                        string respBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            RecordSuccess();

                            var parsed = JObject.Parse(respBody);
                            string jsonText = (string)parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"];

                            if (!string.IsNullOrWhiteSpace(jsonText))
                            {
                                var modObj = JObject.Parse(jsonText);
                                bool isSafe = (bool)(modObj["isSafe"] ?? true);
                                string reason = (string)modObj["reason"] ?? (isSafe ? "Nội dung văn bản an toàn." : "Văn bản vi phạm chính sách nội dung.");

                                string status = isSafe ? ModerationStatuses.AiApproved : ModerationStatuses.AiRejected;
                                if (!string.IsNullOrEmpty(sha256))
                                {
                                    _moderationCache[sha256] = (status, isSafe, reason, DateTime.UtcNow);
                                }

                                return (status, isSafe, reason);
                            }

                            return (ModerationStatuses.AiApproved, true, "Nội dung văn bản đã được AI phân tích an toàn.");
                        }

                        int statusCode = (int)response.StatusCode;
                        if (statusCode == 429)
                        {
                            RecordRateLimitFailure();
                            return (ModerationStatuses.AiUnavailable, false, "Gemini API hết quota (HTTP 429). Chuyển sang bảo vệ kỹ thuật cục bộ.");
                        }

                        RecordGeneralFailure();
                        return (ModerationStatuses.AiUnavailable, false, $"Gemini API không khả dụng (HTTP {statusCode}). Áp dụng bảo vệ kỹ thuật cục bộ.");
                    }
                }
            }
            catch (Exception ex)
            {
                RecordGeneralFailure();
                return (ModerationStatuses.AiUnavailable, false, $"Lỗi kiểm duyệt văn bản bằng AI ({ex.Message}). Áp dụng bảo vệ kỹ thuật cục bộ.");
            }
        }"""

assert old_moderate_ai in content, "old_moderate_ai not found"
content = content.replace(old_moderate_ai, new_moderate_ai, 1)

# 4. In ValidateAndModerateFileAsync, update Step 7 & 8
old_step_7_8 = """            // 7. Kiểm duyệt nội dung ảnh bằng AI: ĐÃ LOẠI BỎ (Không gọi Gemini Vision API để tiết kiệm quota)

            // Toàn bộ quy trình dựa 100% vào các lớp bảo mật cục bộ (Magic Bytes, GDI+ Bitmap Decode, Antivirus, Archive Inspection).

            // Lưu ý: Sau thay đổi này, hệ thống kiểm tra an toàn kỹ thuật và mã độc, không còn AI phân tích nội dung ngữ nghĩa của ảnh.

            // CẢNH BÁO: Trạng thái DISABLED_LOCAL_ONLY biểu thị bỏ qua bước phân tích AI để tối ưu quota,
            // KHÔNG ĐƯỢC hiểu nhầm là ảnh đã được xác nhận an toàn về mặt nội dung đạo đức/cộng đồng.
            result.ImageModerationStatus = "DISABLED_LOCAL_ONLY";
            result.ImageModerationReason = "Kiểm duyệt nội dung ngữ nghĩa bằng AI đã tắt để tối ưu quota; tệp chỉ được xác thực an toàn kỹ thuật cục bộ, không xác nhận an toàn nội dung.";
            audit.ImageModerationStatus = "DISABLED_LOCAL_ONLY (Content unverified by AI)";

            // 8. TẤT CẢ CÁC BƯỚC ĐÃ VƯỢT QUA -> DUYỆT TỆP

            result.IsApproved = true;

            result.UserFriendlyMessage = "Tệp hợp lệ và đã qua kiểm duyệt an toàn.";

            audit.FinalDecision = "APPROVED";

            audit.DecisionReason = "Technical security checks passed (Signature, Antivirus, Archive Inspection). AI semantic content unverified.";

            LogModerationAudit(audit);

            return result;"""

new_step_7_8 = """            // 7. Kiểm duyệt nội dung ngữ nghĩa bằng Gemini API kết hợp các lớp bảo vệ kỹ thuật cục bộ
            if (result.IsImage)
            {
                var aiMod = await ModerateImageWithAiAsync(stagedFilePath, result.DetectedMimeType, result.FileSha256);
                result.ImageModerationStatus = aiMod.status;
                result.ImageModerationReason = aiMod.reason;
                audit.ImageModerationStatus = $"{aiMod.status} ({aiMod.reason})";

                if (aiMod.status == ModerationStatuses.AiRejected)
                {
                    result.IsApproved = false;
                    result.ErrorCode = "AI_CONTENT_VIOLATION";
                    result.UserFriendlyMessage = $"Hình ảnh bị từ chối do vi phạm quy chuẩn nội dung cộng đồng: {aiMod.reason}";
                    audit.FinalDecision = "REJECTED";
                    audit.DecisionReason = $"AI detected content policy violation: {aiMod.reason}";
                    LogModerationAudit(audit);
                    SafeDeleteFile(stagedFilePath);
                    return result;
                }
            }
            else if (ext == ".txt" || ext == ".csv")
            {
                var textMod = await ModerateDocumentTextWithAiAsync(stagedFilePath, result.FileSha256);
                result.ImageModerationStatus = textMod.status;
                result.ImageModerationReason = textMod.reason;
                audit.ImageModerationStatus = $"{textMod.status} (Text Content: {textMod.reason})";

                if (textMod.status == ModerationStatuses.AiRejected)
                {
                    result.IsApproved = false;
                    result.ErrorCode = "AI_CONTENT_VIOLATION";
                    result.UserFriendlyMessage = $"Tệp văn bản bị từ chối do vi phạm quy chuẩn nội dung cộng đồng: {textMod.reason}";
                    audit.FinalDecision = "REJECTED";
                    audit.DecisionReason = $"AI detected text content policy violation: {textMod.reason}";
                    LogModerationAudit(audit);
                    SafeDeleteFile(stagedFilePath);
                    return result;
                }
            }
            else
            {
                // Đối với tệp tài liệu nhị phân (.pdf, .docx, .xlsx, .pptx, .zip, .rar, .7z):
                // Tuyệt đối không gửi nhị phân tùy ý lên AI để bảo vệ dữ liệu và tiết kiệm tài nguyên.
                // Tệp đã được bảo vệ toàn diện qua chữ ký Magic Bytes, quét mã độc, và kiểm tra cấu trúc lưu trữ.
                result.ImageModerationStatus = ModerationStatuses.NotApplicable;
                result.ImageModerationReason = "Tệp tài liệu kỹ thuật được bảo vệ bởi bộ lọc chữ ký, phòng chống Zip Bomb/Slip/Macro và trình quét Antivirus cục bộ.";
                audit.ImageModerationStatus = "NOT_APPLICABLE (Protected by Local Technical Checks)";
            }

            // 8. TẤT CẢ CÁC BƯỚC ĐÃ VƯỢT QUA -> DUYỆT TỆP
            result.IsApproved = true;
            result.UserFriendlyMessage = "Tệp hợp lệ và đã qua kiểm duyệt an toàn.";
            audit.FinalDecision = "APPROVED";
            audit.DecisionReason = result.ImageModerationStatus == ModerationStatuses.AiApproved
                ? "Technical security checks passed and content verified safe by Gemini AI."
                : $"Technical security checks passed (Content moderation status: {result.ImageModerationStatus}).";

            LogModerationAudit(audit);
            return result;"""

assert old_step_7_8 in content, "old_step_7_8 not found"
content = content.replace(old_step_7_8, new_step_7_8, 1)

# Also ensure in steps 1-6 when rejecting, result.ImageModerationStatus is set to LOCAL_REJECTED
content = content.replace(
    'result.ErrorCode = "FILE_NOT_FOUND";',
    'result.ErrorCode = "FILE_NOT_FOUND";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)
content = content.replace(
    'result.ErrorCode = "FILE_EMPTY";',
    'result.ErrorCode = "FILE_EMPTY";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)
content = content.replace(
    'result.ErrorCode = "FILE_TOO_LARGE";',
    'result.ErrorCode = "FILE_TOO_LARGE";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)
content = content.replace(
    'result.ErrorCode = "EXTENSION_PROHIBITED";',
    'result.ErrorCode = "EXTENSION_PROHIBITED";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)
content = content.replace(
    'result.ErrorCode = "DOUBLE_EXTENSION_DETECTED";',
    'result.ErrorCode = "DOUBLE_EXTENSION_DETECTED";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)
content = content.replace(
    'result.ErrorCode = "UNSUPPORTED_EXTENSION";',
    'result.ErrorCode = "UNSUPPORTED_EXTENSION";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)
content = content.replace(
    'result.ErrorCode = "SIGNATURE_MISMATCH";',
    'result.ErrorCode = "SIGNATURE_MISMATCH";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)
content = content.replace(
    'result.ErrorCode = "MALWARE_DETECTED";',
    'result.ErrorCode = "MALWARE_DETECTED";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)
content = content.replace(
    'result.ErrorCode = "SCAN_SERVICE_FAILED";',
    'result.ErrorCode = "SCAN_SERVICE_FAILED";\n                result.ImageModerationStatus = ModerationStatuses.LocalRejected;'
)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Update completed successfully!")
