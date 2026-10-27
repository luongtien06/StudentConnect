using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace StudentConnect.Models.Assistant
{
    public class AiApiClient
    {
        private static readonly HttpClient Http = CreateClient();

        public string AgentName { get; }
        public string ApiKey { get; }
        public string Model { get; }

        public AiApiClient()
        {
            string rawKey = ConfigurationManager.AppSettings["GeminiApiKey"]
                            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                            ?? "";

            // Xử lý khoảng trắng thừa hoặc dấu ngoặc kép vô tình dán vào
            ApiKey = (rawKey ?? "").Trim().Trim('"', '\'');

            AgentName = ConfigurationManager.AppSettings["GeminiAgentName"] ?? "Trợ lý ảo Mr. T";

            // Ưu tiên gemini-3.5-flash-lite hoạt động ổn định và dồi dào quota nhất
            Model = ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-3.5-flash-lite";
        }

        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(45)
            };
        }

        /// <summary>
        /// Gọi Google Gemini REST API generateContent.
        /// Hỗ trợ cả khóa API dạng mới (AQ.) lẫn dạng cũ (AIza).
        /// </summary>
        public async Task<JObject> GenerateContentAsync(JArray contents, JObject systemInstruction = null, JArray tools = null)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException("Chưa cấu hình. Vui lòng cập nhật!");

            // Danh sách model fallback theo thứ tự ổn định
            var candidateModels = new List<string>();
            if (!string.IsNullOrWhiteSpace(Model)) candidateModels.Add(Model);

            foreach (var fallback in new[] { "gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.1-flash-lite", "gemini-3.5-flash", "gemini-3.8-flash" })
            {
                if (!candidateModels.Contains(fallback))
                    candidateModels.Add(fallback);
            }

            string primaryError = null;

            foreach (var currentModel in candidateModels)
            {
                try
                {
                    var body = new JObject
                    {
                        ["contents"] = contents
                    };

                    if (systemInstruction != null)
                    {
                        body["systemInstruction"] = systemInstruction;
                    }

                    if (tools != null && tools.Count > 0)
                    {
                        body["tools"] = tools;
                    }

                    // Gọi Google Gemini API với query key hoặc header
                    string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{currentModel}:generateContent?key={Uri.EscapeDataString(ApiKey)}";

                    using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                    {
                        // Luôn gắn header x-goog-api-key để đảm bảo tương thích mọi loại khóa (AQ. và AIza)
                        request.Headers.TryAddWithoutValidation("x-goog-api-key", ApiKey);

                        request.Content = new StringContent(
                            body.ToString(Formatting.None),
                            Encoding.UTF8,
                            "application/json");

                        using (var response = await Http.SendAsync(request))
                        {
                            var json = await response.Content.ReadAsStringAsync();

                            if (response.IsSuccessStatusCode)
                            {
                                return JObject.Parse(json);
                            }

                            var errObj = JObject.Parse(json);
                            string errMsg = (string)errObj["error"]?["message"] ?? ("HTTP " + (int)response.StatusCode);
                            string errReason = (string)errObj["error"]?["details"]?[0]?["reason"] ?? "";
                            string errStatus = (string)errObj["error"]?["status"] ?? "";

                            if (primaryError == null)
                                primaryError = errMsg;

                            // 1. Lỗi xác thực khóa API (400 INVALID_ARGUMENT hoặc 401 UNAUTHENTICATED)
                            if ((int)response.StatusCode == 401 ||
                                errReason == "ACCESS_TOKEN_TYPE_UNSUPPORTED" ||
                                errReason == "API_KEY_INVALID" ||
                                errMsg.IndexOf("API key not valid", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                throw new InvalidOperationException(
                                    $"Key không hợp lệ hoặc chưa được cấp quyền.\n" +
                                    "Vui lòng cập nhật Key mới!");
                            }

                            // 2. Hết hạn mức tạm thời hoặc Rate limit theo phút (429) -> Thử chờ và retry với model khác
                            if ((int)response.StatusCode == 429 ||
                                errMsg.IndexOf("Quota", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                errReason == "RESOURCE_EXHAUSTED")
                            {
                                await Task.Delay(2000);
                                continue;
                            }

                            // 3. Project bị chặn truy cập
                            if ((int)response.StatusCode == 403)
                            {
                                throw new InvalidOperationException(
                                    $"Google Cloud Project bị từ chối quyền truy cập Gemini API ({errMsg}). " +
                                    "Vui lòng cập nhật Key mới.");
                            }

                            // 4. Nếu model bị quá tải (503) hoặc không tìm thấy (404) -> thử model fallback tiếp theo
                            if ((int)response.StatusCode == 503 || (int)response.StatusCode == 404)
                            {
                                continue;
                            }
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    throw; // Giữ nguyên thông báo lỗi nghiệp vụ rõ ràng
                }
                catch (Exception ex)
                {
                    if (primaryError == null)
                        primaryError = ex.Message;
                }
            }

            throw new InvalidOperationException(primaryError ?? "Không thể kết nối API.");
        }

        /// <summary>
        /// Kiểm tra thực tế kết nối tới Google Gemini API với thời gian trễ và trạng thái quota
        /// </summary>
        public async Task<ApiTestResult> TestConnectionAsync(string customKey = null, string customModel = null)
        {
            string keyToUse = !string.IsNullOrWhiteSpace(customKey) ? customKey.Trim().Trim('"', '\'') : ApiKey;
            if (string.IsNullOrWhiteSpace(keyToUse))
            {
                return new ApiTestResult
                {
                    Success = false,
                    Message = "Chưa cấu hình API Key. Vui lòng nhập và lưu API Key trước khi kiểm tra.",
                    LatencyMs = 0,
                    ModelUsed = null
                };
            }

            string modelToUse = !string.IsNullOrWhiteSpace(customModel) ? customModel.Trim() : (Model ?? "gemini-3.5-flash-lite");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var testContents = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray { new JObject { ["text"] = "Ping. Trả lời đúng 1 từ 'Pong'." } }
                    }
                };

                var requestBody = new JObject
                {
                    ["contents"] = testContents,
                    ["generationConfig"] = new JObject
                    {
                        ["temperature"] = 0.1,
                        ["maxOutputTokens"] = 10
                    }
                };

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelToUse}:generateContent?key={keyToUse}";
                using (var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json"))
                {
                    var response = await Http.PostAsync(url, content);
                    sw.Stop();

                    string respText = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        return new ApiTestResult
                        {
                            Success = true,
                            Message = $"Kết nối Google Gemini API thành công! Phản hồi trong {sw.ElapsedMilliseconds}ms.",
                            LatencyMs = (int)sw.ElapsedMilliseconds,
                            ModelUsed = modelToUse
                        };
                    }

                    string errSnippet = respText;
                    try
                    {
                        var jErr = JObject.Parse(respText);
                        errSnippet = jErr["error"]?["message"]?.ToString() ?? respText;
                    }
                    catch { }

                    return new ApiTestResult
                    {
                        Success = false,
                        Message = $"Lỗi phản hồi từ Gemini API (HTTP {(int)response.StatusCode}): {errSnippet}",
                        LatencyMs = (int)sw.ElapsedMilliseconds,
                        ModelUsed = modelToUse
                    };
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new ApiTestResult
                {
                    Success = false,
                    Message = $"Lỗi kết nối mạng hoặc máy chủ: {ex.Message}",
                    LatencyMs = (int)sw.ElapsedMilliseconds,
                    ModelUsed = modelToUse
                };
            }
        }
    }
}
