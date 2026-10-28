using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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

            ApiKey = (rawKey ?? "").Trim().Trim('"', '\'');
            AgentName = ConfigurationManager.AppSettings["GeminiAgentName"] ?? "Trợ lý ảo Mr. T";

            // Ưu tiên đọc model từ cấu hình động trong agent_config.json, sau đó fallback sang Web.config
            string configuredModel = null;
            try
            {
                configuredModel = AgentConfigService.GetConfig()?.PrimaryModel;
            }
            catch { }

            if (string.IsNullOrWhiteSpace(configuredModel))
            {
                configuredModel = ConfigurationManager.AppSettings["GeminiModel"];
            }

            // Xác minh model có thuộc danh sách chính thức được hỗ trợ hay không
            if (!string.IsNullOrWhiteSpace(configuredModel) &&
                Array.Exists(AgentConfigModel.SupportedModels, m => string.Equals(m, configuredModel.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                Model = configuredModel.Trim();
            }
            else
            {
                Model = "gemini-3.5-flash-lite";
            }
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
        /// Gọi Google Gemini REST API v1beta generateContent.
        /// Sử dụng các model chính thức (gemini-1.5-flash, gemini-2.0-flash, gemini-1.5-pro, gemini-2.5-flash).
        /// Bảo vệ tuyệt đối thông tin khóa bí mật, không in key ra log hay exception.
        /// </summary>
        public async Task<JObject> GenerateContentAsync(JArray contents, JObject systemInstruction = null, JArray tools = null)
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
                throw new InvalidOperationException("Chưa cấu hình Google Gemini API Key. Vui lòng cập nhật API Key hợp lệ (Google AI Studio) trong trang Quản trị AI Agent.");

            // Danh sách model candidate: model hiện tại -> danh sách fallback hợp lệ
            var candidateModels = new List<string>();
            if (!string.IsNullOrWhiteSpace(Model))
            {
                candidateModels.Add(Model);
            }

            try
            {
                var configuredFallbacks = AgentConfigService.GetConfig()?.FallbackModels;
                if (configuredFallbacks != null)
                {
                    foreach (var fb in configuredFallbacks)
                    {
                        if (!string.IsNullOrWhiteSpace(fb) && !candidateModels.Contains(fb.Trim(), StringComparer.OrdinalIgnoreCase))
                        {
                            candidateModels.Add(fb.Trim());
                        }
                    }
                }
            }
            catch { }

            foreach (var supported in AgentConfigModel.SupportedModels)
            {
                if (!candidateModels.Contains(supported, StringComparer.OrdinalIgnoreCase))
                {
                    candidateModels.Add(supported);
                }
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

                    // Endpoint REST chuẩn, truyền API Key qua HTTP Header x-goog-api-key thay vì query string
                    string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{currentModel}:generateContent";

                    using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                    {
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

                            string errMsg = "HTTP " + (int)response.StatusCode;
                            string errReason = "";
                            string errStatus = "";

                            try
                            {
                                var errObj = JObject.Parse(json);
                                errMsg = (string)errObj["error"]?["message"] ?? errMsg;
                                errReason = (string)errObj["error"]?["details"]?[0]?["reason"] ?? "";
                                errStatus = (string)errObj["error"]?["status"] ?? "";
                            }
                            catch { }

                            if (primaryError == null)
                                primaryError = errMsg;

                            // 1. Lỗi xác thực khóa API (401 UNAUTHENTICATED hoặc INVALID_ARGUMENT liên quan key) -> Dừng ngay, KHÔNG lặp fallback
                            if ((int)response.StatusCode == 401 ||
                                errReason == "ACCESS_TOKEN_TYPE_UNSUPPORTED" ||
                                errReason == "API_KEY_INVALID" ||
                                errMsg.IndexOf("API key not valid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                errMsg.IndexOf("UNAUTHENTICATED", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                throw new InvalidOperationException(
                                    "Google Gemini API Key không hợp lệ hoặc chưa được cấp quyền (HTTP 401). " +
                                    "Vui lòng cập nhật API Key hợp lệ (bắt đầu bằng AIzaSy...) trong trang Quản trị AI Agent.");
                            }

                            // 2. Project bị chặn truy cập hoặc API chưa được enable (403 PERMISSION_DENIED) -> Dừng ngay, KHÔNG lặp fallback
                            if ((int)response.StatusCode == 403)
                            {
                                throw new InvalidOperationException(
                                    $"Google Cloud Project bị từ chối quyền truy cập Gemini API (HTTP 403: {errMsg}). " +
                                    "Vui lòng kiểm tra quyền và hạn ngạch của dự án.");
                            }

                            // 3. Hạn mức tạm thời hoặc Rate limit theo phút (429 RESOURCE_EXHAUSTED) -> Delay và thử fallback tiếp theo
                            if ((int)response.StatusCode == 429 ||
                                errMsg.IndexOf("Quota", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                errReason == "RESOURCE_EXHAUSTED")
                            {
                                int delayMs = 1500;
                                if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                                {
                                    delayMs = (int)Math.Min(10000, Math.Max(1000, response.Headers.RetryAfter.Delta.Value.TotalMilliseconds));
                                }
                                await Task.Delay(delayMs);
                                continue;
                            }

                            // 4. Model không tìm thấy (404) hoặc dịch vụ tạm thời quá tải (503) -> thử model fallback tiếp theo
                            if ((int)response.StatusCode == 404 || (int)response.StatusCode == 503 || (int)response.StatusCode == 500)
                            {
                                continue;
                            }
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    throw; // Giữ nguyên lỗi nghiệp vụ xác thực/phân quyền rõ ràng
                }
                catch (Exception ex)
                {
                    if (primaryError == null)
                        primaryError = ex.Message;
                }
            }

            throw new InvalidOperationException(primaryError ?? "Không thể kết nối đến Google Gemini API.");
        }

        /// <summary>
        /// Kiểm tra thực tế kết nối tới Google Gemini API với thời gian trễ và trạng thái quota.
        /// Đảm bảo không bao giờ in hay trả về API Key thô.
        /// </summary>
        public async Task<ApiTestResult> TestConnectionAsync(string customKey = null, string customModel = null)
        {
            string keyToUse = !string.IsNullOrWhiteSpace(customKey) ? customKey.Trim().Trim('"', '\'') : ApiKey;
            if (string.IsNullOrWhiteSpace(keyToUse))
            {
                return new ApiTestResult
                {
                    Success = false,
                    Message = "Chưa cấu hình API Key. Vui lòng nhập và lưu API Key hợp lệ (Google AI Studio) trước khi kiểm tra.",
                    LatencyMs = 0,
                    ModelUsed = null
                };
            }

            // Sử dụng chính xác model được yêu cầu kiểm thử để trả về phản hồi thực tế từ Google API (không âm thầm ghi đè)
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

                string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelToUse}:generateContent";
                using (var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json"))
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.TryAddWithoutValidation("x-goog-api-key", keyToUse);
                    request.Content = content;

                    var response = await Http.SendAsync(request);
                    sw.Stop();

                    string respText = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        return new ApiTestResult
                        {
                            Success = true,
                            Message = $"Kết nối Google Gemini API thành công! Model '{modelToUse}' phản hồi trong {sw.ElapsedMilliseconds}ms.",
                            LatencyMs = (int)sw.ElapsedMilliseconds,
                            ModelUsed = modelToUse
                        };
                    }

                    string errSnippet = respText;
                    string errReason = "";
                    try
                    {
                        var jErr = JObject.Parse(respText);
                        errSnippet = jErr["error"]?["message"]?.ToString() ?? respText;
                        errReason = jErr["error"]?["details"]?[0]?["reason"]?.ToString() ?? "";
                    }
                    catch { }

                    int statusCode = (int)response.StatusCode;
                    string friendlyMsg;

                    if (statusCode == 401 || errReason == "ACCESS_TOKEN_TYPE_UNSUPPORTED" || errReason == "API_KEY_INVALID" || errSnippet.IndexOf("API key not valid", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        friendlyMsg = $"Lỗi xác thực (HTTP 401): API Key không hợp lệ hoặc chưa được kích hoạt trên Google AI Studio. Chi tiết: {errSnippet}";
                    }
                    else if (statusCode == 403)
                    {
                        friendlyMsg = $"Lỗi phân quyền (HTTP 403): Dự án hoặc API Key bị từ chối truy cập. Chi tiết: {errSnippet}";
                    }
                    else if (statusCode == 404)
                    {
                        friendlyMsg = $"Mô hình không tồn tại (HTTP 404): Model '{modelToUse}' không khả dụng cho tài khoản API này.";
                    }
                    else if (statusCode == 429)
                    {
                        friendlyMsg = $"Hết hạn mức hoặc chạm giới hạn tần suất (HTTP 429): {errSnippet}";
                    }
                    else
                    {
                        friendlyMsg = $"Lỗi phản hồi từ Gemini API (HTTP {statusCode}): {errSnippet}";
                    }

                    return new ApiTestResult
                    {
                        Success = false,
                        Message = friendlyMsg,
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
                    Message = $"Lỗi kết nối mạng hoặc timeout: {ex.Message}",
                    LatencyMs = (int)sw.ElapsedMilliseconds,
                    ModelUsed = modelToUse
                };
            }
        }
    }
}
