using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Data.Entity;
using System.Threading.Tasks;

namespace StudentConnect.Models.Assistant
{
    public class AssistantService
    {
        private readonly AiApiClient _ai = new AiApiClient();
        private readonly SocialTools _tools = new SocialTools();
        private readonly AgentPermissionService _permService = new AgentPermissionService();
        private readonly AgentAuditService _auditService = new AgentAuditService();
        private readonly AgentConfirmationService _confirmService = new AgentConfirmationService();
        private readonly AgentConversationService _convService = new AgentConversationService();

        public string AgentName => _ai.AgentName ?? "Trợ lý ảo Mr. T";

        private static JArray GetGeminiTools()
        {
            return JArray.Parse(@"[
              {
                ""functionDeclarations"": [
                  {
                    ""name"": ""search_posts"",
                    ""description"": ""Tìm kiếm các bài viết, chia sẻ công khai của sinh viên trên bảng tin StudentConnect theo từ khóa, người đăng hoặc khoảng thời gian"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""keyword"": { ""type"": ""STRING"", ""description"": ""Từ khóa cần tìm trong bài viết"" },
                        ""author"": { ""type"": ""STRING"", ""description"": ""Tên tài khoản hoặc tên hiển thị của người đăng (nếu có)"" },
                        ""timeRange"": { ""type"": ""STRING"", ""description"": ""Khoảng thời gian: 'today' (hôm nay), 'yesterday' (hôm qua), 'week' (tuần này), 'month' (tháng này), 'all' (tất cả)"" },
                        ""limit"": { ""type"": ""INTEGER"", ""description"": ""Số lượng kết quả tối đa (mặc định 5, tối đa 10)"" }
                      }
                    }
                  },
                  {
                    ""name"": ""search_market"",
                    ""description"": ""Tìm kiếm các sản phẩm, đồ dùng học tập, giáo trình, laptop, đồ điện tử thanh lý trên Chợ sinh viên TDMU"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""keyword"": { ""type"": ""STRING"", ""description"": ""Tên hoặc từ khóa sản phẩm cần tìm trên Chợ"" },
                        ""timeRange"": { ""type"": ""STRING"", ""description"": ""Khoảng thời gian: 'today', 'yesterday', 'week', 'month', 'all'"" },
                        ""limit"": { ""type"": ""INTEGER"", ""description"": ""Số lượng kết quả tối đa (mặc định 5, tối đa 10)"" }
                      }
                    }
                  },
                  {
                    ""name"": ""search_connect"",
                    ""description"": ""Tìm kiếm tin kết nối sinh viên TDMU: tìm bạn ở ghép, tìm trọ gần trường, tìm bạn học nhóm, câu lạc bộ"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""keyword"": { ""type"": ""STRING"", ""description"": ""Từ khóa tìm kiếm kết nối"" },
                        ""timeRange"": { ""type"": ""STRING"", ""description"": ""Khoảng thời gian: 'today', 'yesterday', 'week', 'month', 'all'"" },
                        ""limit"": { ""type"": ""INTEGER"", ""description"": ""Số lượng kết quả tối đa (mặc định 5, tối đa 10)"" }
                      }
                    }
                  },
                  {
                    ""name"": ""search_stories"",
                    ""description"": ""Tìm kiếm các tin Story (24h) của bạn bè hoặc chính mình"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""keyword"": { ""type"": ""STRING"", ""description"": ""Từ khóa trong nội dung tin story hoặc tên người đăng"" },
                        ""limit"": { ""type"": ""INTEGER"", ""description"": ""Số lượng kết quả (mặc định 5)"" }
                      }
                    }
                  },
                  {
                    ""name"": ""get_my_posts"",
                    ""description"": ""Tìm và tra cứu danh sách các bài viết, tin Chợ, tin Kết nối hoặc tin Story của CHÍNH người dùng hiện tại đang đăng nhập"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""itemType"": { ""type"": ""STRING"", ""description"": ""Loại bài cần tìm: 'all' (tất cả các loại), 'Post' (bài viết bảng tin), 'Market' (tin Chợ sinh viên), 'Connect' (tin Kết nối), 'Story' (tin 24h)"" },
                        ""keyword"": { ""type"": ""STRING"", ""description"": ""Từ khóa tìm trong tiêu đề hoặc nội dung bài của tôi"" },
                        ""timeRange"": { ""type"": ""STRING"", ""description"": ""Khoảng thời gian: 'today', 'yesterday', 'week', 'month', 'all'"" },
                        ""limit"": { ""type"": ""INTEGER"", ""description"": ""Số lượng bài tối đa (mặc định 10)"" }
                      }
                    }
                  },
                  {
                    ""name"": ""get_post_details"",
                    ""description"": ""Xem nội dung chi tiết bài viết, tác giả, lượt thích và các bình luận gần nhất theo PostID"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""postId"": { ""type"": ""INTEGER"", ""description"": ""Mã số ID của bài viết"" }
                      },
                      ""required"": [""postId""]
                    }
                  },
                  {
                    ""name"": ""get_recent_notifications"",
                    ""description"": ""Xem danh sách các thông báo mới nhất của người dùng hiện tại"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""limit"": { ""type"": ""INTEGER"", ""description"": ""Số lượng thông báo muốn xem (tối đa 10)"" }
                      }
                    }
                  },
                  {
                    ""name"": ""get_user_profile"",
                    ""description"": ""Tra cứu hồ sơ thông tin sinh viên theo Tên hiển thị, Tên đăng nhập hoặc UserID (để trống nếu muốn xem của chính mình)"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""query"": { ""type"": ""STRING"", ""description"": ""Tên tài khoản, tên hiển thị hoặc ID người dùng"" }
                      }
                    }
                  },
                  {
                    ""name"": ""get_social_statistics"",
                    ""description"": ""Thống kê tổng số bài viết, lượt thích nhận được, số người theo dõi của chính người dùng hiện tại"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {}
                    }
                  },
                  {
                    ""name"": ""create_post"",
                    ""description"": ""Đăng một bài viết mới lên Bảng tin cộng đồng của sinh viên TDMU"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""content"": { ""type"": ""STRING"", ""description"": ""Nội dung chi tiết của bài viết"" },
                        ""privacy"": { ""type"": ""STRING"", ""description"": ""Quyền riêng tư: 'Public' (Công khai), 'Friends' (Bạn bè), 'OnlyMe' (Chỉ mình tôi)"" }
                      },
                      ""required"": [""content""]
                    }
                  },
                  {
                    ""name"": ""create_market_post"",
                    ""description"": ""Đăng một tin rao bán đồ dùng, sách, giáo trình, laptop lên Chợ sinh viên TDMU"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""title"": { ""type"": ""STRING"", ""description"": ""Tiêu đề tin đăng bán"" },
                        ""description"": { ""type"": ""STRING"", ""description"": ""Mô tả chi tiết sản phẩm"" },
                        ""price"": { ""type"": ""NUMBER"", ""description"": ""Giá bán (VNĐ)"" },
                        ""condition"": { ""type"": ""STRING"", ""description"": ""Tình trạng sản phẩm (ví dụ: Mới 99%, Còn dùng tốt, Hơi cũ)"" },
                        ""location"": { ""type"": ""STRING"", ""description"": ""Địa điểm trao đổi (ví dụ: Khuôn viên trường TDMU, Cổng 1)"" },
                        ""categoryId"": { ""type"": ""INTEGER"", ""description"": ""Mã danh mục (tùy chọn)"" }
                      },
                      ""required"": [""title""]
                    }
                  },
                  {
                    ""name"": ""create_connect_post"",
                    ""description"": ""Đăng một tin lên mục Kết nối sinh viên TDMU (tìm trọ, tìm bạn ở ghép, tìm bạn học nhóm, CLB)"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""title"": { ""type"": ""STRING"", ""description"": ""Tiêu đề tin kết nối"" },
                        ""description"": { ""type"": ""STRING"", ""description"": ""Nội dung chi tiết tin kết nối"" },
                        ""categoryId"": { ""type"": ""INTEGER"", ""description"": ""Mã danh mục (tùy chọn)"" }
                      },
                      ""required"": [""title""]
                    }
                  },
                  {
                    ""name"": ""edit_post"",
                    ""description"": ""Chỉnh sửa nội dung hoặc quyền riêng tư bài viết của chính người dùng hiện tại"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""postId"": { ""type"": ""INTEGER"", ""description"": ""ID bài viết cần sửa"" },
                        ""newContent"": { ""type"": ""STRING"", ""description"": ""Nội dung mới của bài viết"" },
                        ""newPrivacy"": { ""type"": ""STRING"", ""description"": ""Quyền riêng tư mới (nếu muốn đổi)"" }
                      },
                      ""required"": [""postId"", ""newContent""]
                    }
                  },
                  {
                    ""name"": ""delete_post"",
                    ""description"": ""Xóa bài viết, tin Chợ, tin Kết nối hoặc tin Story của chính người dùng hiện tại"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""postId"": { ""type"": ""INTEGER"", ""description"": ""Mã ID bài viết cần xóa"" },
                        ""itemType"": { ""type"": ""STRING"", ""description"": ""Loại bài: 'Post' (bài viết thường), 'Market' (tin Chợ sinh viên), 'Connect' (tin Kết nối), 'Story' (tin Story 24h). Mặc định là 'Post'"" },
                        ""confirmed"": { ""type"": ""BOOLEAN"", ""description"": ""True nếu người dùng đã nói rõ xác nhận đồng ý xóa bài này, False nếu người dùng mới chỉ yêu cầu xóa và chưa xác nhận"" }
                      },
                      ""required"": [""postId""]
                    }
                  },
                  {
                    ""name"": ""react_to_post"",
                    ""description"": ""Thích (Like) hoặc Bỏ thích một bài viết trên mạng xã hội"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""postId"": { ""type"": ""INTEGER"", ""description"": ""ID bài viết cần thích hoặc bỏ thích"" },
                        ""reactionType"": { ""type"": ""STRING"", ""description"": ""Loại cảm xúc (mặc định: 'Like')"" }
                      },
                      ""required"": [""postId""]
                    }
                  },
                  {
                    ""name"": ""add_comment"",
                    ""description"": ""Gửi một bình luận vào bài viết trên mạng xã hội"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""postId"": { ""type"": ""INTEGER"", ""description"": ""ID bài viết cần bình luận"" },
                        ""content"": { ""type"": ""STRING"", ""description"": ""Nội dung bình luận"" }
                      },
                      ""required"": [""postId"", ""content""]
                    }
                  },
                  {
                    ""name"": ""follow_user"",
                    ""description"": ""Theo dõi (Follow) hoặc Hủy theo dõi một sinh viên khác trên hệ thống"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""targetUserId"": { ""type"": ""INTEGER"", ""description"": ""ID người dùng cần theo dõi / hủy theo dõi"" }
                      },
                      ""required"": [""targetUserId""]
                    }
                  },
                  {
                    ""name"": ""create_scheduled_task"",
                    ""description"": ""Lập lịch một tác vụ tự động để Agent định kỳ thực hiện kể cả khi người dùng đóng trình duyệt"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""taskName"": { ""type"": ""STRING"", ""description"": ""Tên mô tả tác vụ tự động"" },
                        ""userPrompt"": { ""type"": ""STRING"", ""description"": ""Mô tả chi tiết những gì Agent cần làm khi tác vụ kích hoạt"" },
                        ""scheduleType"": { ""type"": ""STRING"", ""description"": ""Chu kỳ: 'Once' (chạy 1 lần sau vài phút), 'Hourly' (mỗi giờ), 'Daily' (hàng ngày), 'Weekly' (hàng tuần), 'IntervalMinutes' (mỗi X phút)"" },
                        ""scheduleConfig"": { ""type"": ""STRING"", ""description"": ""Cấu hình thêm (ví dụ số phút nếu là IntervalMinutes)"" }
                      },
                      ""required"": [""taskName"", ""userPrompt"", ""scheduleType""]
                    }
                  },
                  {
                    ""name"": ""list_scheduled_tasks"",
                    ""description"": ""Xem danh sách các tác vụ tự động đã lên lịch của người dùng hiện tại"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {}
                    }
                  },
                  {
                    ""name"": ""update_task_status"",
                    ""description"": ""Thay đổi trạng thái tác vụ tự động (ví dụ: 'Paused' để tạm dừng, 'Active' để chạy lại, 'Cancelled' để hủy)"",
                    ""parameters"": {
                      ""type"": ""OBJECT"",
                      ""properties"": {
                        ""taskId"": { ""type"": ""INTEGER"", ""description"": ""ID của tác vụ cần cập nhật"" },
                        ""newStatus"": { ""type"": ""STRING"", ""description"": ""Trạng thái mới: 'Active', 'Paused', 'Cancelled'"" }
                      },
                      ""required"": [""taskId"", ""newStatus""]
                    }
                  }
                ]
              }
            ]");
        }

        private static JObject GetSystemInstruction(int currentUserId = 0)
        {
            string userContextText = currentUserId > 0
                ? $"[TRẠNG THÁI NGƯỜI DÙNG HIỆN TẠI]: Người dùng đã đăng nhập thành công với UserID: {currentUserId}.\n\n"
                : "[TRẠNG THÁI NGƯỜI DÙNG HIỆN TẠI]: Người dùng là Khách vãng lai (CHƯA ĐĂNG NHẬP). Khách chỉ được phép trò chuyện hỏi đáp và tra cứu công khai. Nếu người dùng yêu cầu đăng bài, sửa bài, xóa bài, tương tác thích/bình luận hoặc xem bài viết cá nhân, hãy thông báo lịch sự rằng tính năng này yêu cầu người dùng phải đăng nhập tài khoản StudentConnect trước.\n\n";

            return new JObject
            {
                ["parts"] = new JArray
                {
                    new JObject
                    {
                        ["text"] =
                            userContextText +
                            "Bạn là Trợ lý ảo Mr. T - Hệ thống AI Agent thông minh của Mạng xã hội Sinh viên Đại học Thủ Dầu Một (TDMU StudentConnect).\n" +
                            "Bạn có khả năng lập kế hoạch, suy luận, thực thi các công cụ nghiệp vụ (Function Calling) và chạy tác vụ tự động có kiểm soát.\n\n" +
                            "CÁC NGUYÊN TẮC BẮT BUỘC KHI XỬ LÝ DỮ LIỆU & TÁC VỤ:\n" +
                            "1. BẮT BUỘC GỌI CÔNG CỤ (TOOL CALLING):\n" +
                            "   - Khi người dùng muốn tìm kiếm, xem bài viết, tìm bài của tôi, đăng bài, sửa bài, xóa bài, thích, bình luận, follow, hay tạo lịch trình: BẮT BUỘC phải gọi đúng công cụ (function call) tương ứng để thao tác với dữ liệu thật trong cơ sở dữ liệu.\n" +
                            "   - TUYỆT ĐỐI KHÔNG tự bịa đặt danh sách bài viết, ID, số liệu hoặc báo đã thực hiện thành công khi chưa gọi công cụ.\n\n" +
                            "2. TÌM KIẾM & ĐỌC DỮ LIỆU:\n" +
                            "   - Dùng 'search_posts' để tìm bài viết bảng tin sinh viên.\n" +
                            "   - Dùng 'search_market' để tìm đồ dùng, sách, laptop, giáo trình trên Chợ sinh viên.\n" +
                            "   - Dùng 'search_connect' để tìm tin phòng trọ, ở ghép, học tập, câu lạc bộ trên mục Kết nối.\n" +
                            "   - Dùng 'search_stories' để tìm tin Story 24h.\n" +
                            "   - Dùng 'get_my_posts' khi người dùng hỏi các câu như 'Tìm bài của tôi', 'Các bài tôi đã đăng', 'Xem bài tôi đăng hôm qua', 'Bài tôi vừa đăng'.\n" +
                            "   - Khi trả về kết quả tìm kiếm: Luôn dẫn kèm ID bài viết, người đăng, thời gian và link chi tiết (URL) để người dùng có thể nhấp vào xem.\n" +
                            "   - Khi công cụ tìm kiếm trả về danh sách rỗng []: Hãy DỪNG gọi thêm công cụ và phản hồi ngay cho người dùng bằng văn bản thông báo trung thực rằng hiện chưa tìm thấy nội dung phù hợp, tuyệt đối không được bịa bài viết và hãy gợi ý người dùng đổi từ khóa.\n\n" +
                            "3. QUY TRÌNH XÓA BÀI VIẾT (3 BƯỚC AN TOÀN):\n" +
                            "   - Bước 1: Khi người dùng yêu cầu xóa bài (ví dụ 'Xóa bài tôi vừa đăng', 'Xóa bài bán sách của tôi'), hãy gọi 'get_my_posts' để lấy danh sách bài viết thực tế của tài khoản đang đăng nhập.\n" +
                            "   - Bước 2: Nếu có nhiều bài viết phù hợp hoặc chưa rõ người dùng muốn xóa bài nào, hãy liệt kê danh sách các bài tìm được (kèm ID, tiêu đề/trích dẫn, thời gian) và hỏi người dùng muốn xóa bài nào.\n" +
                            "   - Bước 3: Khi đã xác định được ID cụ thể:\n" +
                            "     + Nếu người dùng chưa nói rõ lời xác nhận: Gọi 'delete_post' với 'confirmed: false'. Hệ thống sẽ tạo Thẻ yêu cầu xác nhận duyệt trên màn hình.\n" +
                            "     + Nếu người dùng đã nói rõ lời xác nhận (ví dụ 'Tôi xác nhận xóa bài 10', 'Đồng ý xóa bài 10'): Gọi 'delete_post' với 'confirmed: true'.\n" +
                            "   - TUYỆT ĐỐI KHÔNG xóa bài của người khác. Công cụ sẽ tự động từ chối nếu bài không thuộc về người dùng hiện tại.\n\n" +
                            "4. QUYỀN HẠN & VĂN PHONG:\n" +
                            "   - Khi công cụ trả về thông báo lỗi hoặc bị chặn quyền, hãy giải thích rõ ràng và hướng dẫn người dùng.\n" +
                            "   - Trả lời bằng tiếng Việt thân thiện, văn phong sinh viên TDMU trẻ trung, nhiệt tình, lịch sự, dùng định dạng Markdown rõ ràng."
                    }
                }
            };
        }

        /// <summary>
        /// Xử lý chat tương tác trực tiếp với người dùng
        /// </summary>
        public async Task<ChatResponse> AskWithContextAsync(string message, int currentUserId)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length > 2000)
                throw new ArgumentException("Nội dung tin nhắn không hợp lệ hoặc quá dài.");

            string agentName = AgentName;
            var response = new ChatResponse
            {
                Success = true,
                AgentName = agentName
            };

            var contents = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["parts"] = new JArray
                    {
                        new JObject { ["text"] = message.Trim() }
                    }
                }
            };

            var systemInstruction = GetSystemInstruction(currentUserId);
            var tools = GetGeminiTools();

            // Kiểm tra hạn mức Gemini API toàn cục & theo người dùng
            var geminiCheck = AgentGuardService.CheckGeminiQuota(currentUserId);
            if (!geminiCheck.allowed)
            {
                response.Success = false;
                response.Error = geminiCheck.reason;
                response.Reply = "Hệ thống Trợ lý ảo tạm thời không thể xử lý do đạt giới hạn: " + geminiCheck.reason;
                return response;
            }

            // Reasoning loop: tối đa 6 vòng để giải quyết function calls
            for (int round = 0; round < 6; round++)
            {
                JObject result;
                await AgentGuardService.AcquireGeminiThrottleAsync();
                try
                {
                    result = await _ai.GenerateContentAsync(contents, systemInstruction, tools);
                    AgentGuardService.RecordGeminiCall(currentUserId);
                }
                finally
                {
                    AgentGuardService.ReleaseGeminiThrottle();
                }

                var candidate = result["candidates"]?[0];
                var contentObj = candidate?["content"] as JObject;
                if (contentObj == null)
                {
                    response.Reply = "Xin lỗi bạn, trợ lý chưa thể tạo câu trả lời lúc này. Bạn vui lòng thử lại nhé!";
                    return response;
                }

                var parts = contentObj["parts"] as JArray;
                if (parts == null || parts.Count == 0)
                {
                    response.Reply = "Xin lỗi, không nhận được nội dung từ trợ lý.";
                    return response;
                }

                // Kiểm tra xem Gemini có gọi functionCall hay không
                JObject functionCall = null;
                string textResponse = "";

                foreach (var part in parts)
                {
                    if (part["functionCall"] is JObject fc)
                    {
                        functionCall = fc;
                        break;
                    }
                    if (part["text"] != null)
                    {
                        textResponse += (string)part["text"];
                    }
                }

                // Nếu không có functionCall, trả về văn bản hoàn tất
                if (functionCall == null)
                {
                    response.Reply = !string.IsNullOrWhiteSpace(textResponse)
                        ? textResponse.Trim()
                        : "Xin lỗi, tôi chưa tạo được câu trả lời phù hợp.";
                    return response;
                }

                // Thêm model content vào lịch sử hội thoại
                contents.Add(contentObj);

                // Thực thi functionCall tương ứng với cơ chế phân quyền và kiểm toán
                string toolName = (string)functionCall["name"];
                string functionCallId = (string)functionCall["id"] ?? "";
                var args = functionCall["args"] as JObject ?? new JObject();

                JToken toolData;
                using (var db = new TDMUEcoSystemEntities())
                {
                    toolData = await ExecuteToolWithSecurityAsync(db, toolName, args, currentUserId, null, response);
                }

                // Đưa kết quả functionResponse lại cho Gemini
                var frObj = new JObject
                {
                    ["name"] = toolName,
                    ["response"] = new JObject
                    {
                        ["result"] = toolData
                    }
                };

                if (!string.IsNullOrEmpty(functionCallId))
                {
                    frObj["id"] = functionCallId;
                }

                contents.Add(new JObject
                {
                    ["role"] = "user",
                    ["parts"] = new JArray
                    {
                        new JObject { ["functionResponse"] = frObj }
                    }
                });
            }

            response.Reply = "Yêu cầu của bạn cần qua nhiều bước xử lý. Bạn hãy kiểm tra lại thông tin hoặc đặt câu hỏi cụ thể hơn nhé!";
            return response;
        }

        public async Task<string> AskAsync(string message, int currentUserId)
        {
            var res = await AskWithContextAsync(message, currentUserId);
            return res.Reply;
        }

        /// <summary>
        /// Chat có lưu lịch sử phiên hội thoại vào database.
        /// Nạp tin nhắn cũ từ phiên → gửi cùng tin mới tới Gemini → lưu kết quả vào phiên.
        /// </summary>
        public async Task<ChatResponse> AskInConversationAsync(string message, int currentUserId, long conversationId)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length > 2000)
                throw new ArgumentException("Nội dung tin nhắn không hợp lệ hoặc quá dài.");

            string agentName = AgentName;
            var response = new ChatResponse
            {
                Success = true,
                AgentName = agentName
            };

            using (var db = new TDMUEcoSystemEntities())
            {
                // Xác minh phiên thuộc quyền người dùng
                var conv = await db.AgentConversations
                    .FirstOrDefaultAsync(c => c.ConversationID == conversationId && c.UserID == currentUserId);
                if (conv == null)
                {
                    response.Success = false;
                    response.Error = "Không tìm thấy phiên hội thoại hoặc bạn không có quyền truy cập.";
                    return response;
                }

                // Lưu tin nhắn người dùng
                await _convService.SaveMessageAsync(db, conversationId, "User", message.Trim());

                // Xây dựng lịch sử Gemini từ DB (tối đa 20 tin nhắn gần nhất)
                var contents = await _convService.BuildGeminiHistoryAsync(db, conversationId, currentUserId, 20);

                var systemInstruction = GetSystemInstruction(currentUserId);
                var tools = GetGeminiTools();

                string toolCallsJson = null;
                string toolResultsJson = null;

                // Kiểm tra hạn mức Gemini API toàn cục & theo người dùng
                var geminiCheck = AgentGuardService.CheckGeminiQuota(currentUserId);
                if (!geminiCheck.allowed)
                {
                    response.Success = false;
                    response.Error = geminiCheck.reason;
                    response.Reply = "Hệ thống Trợ lý ảo tạm thời không thể xử lý do đạt giới hạn: " + geminiCheck.reason;
                    return response;
                }

                // Reasoning loop: tối đa 6 vòng
                for (int round = 0; round < 6; round++)
                {
                    JObject result;
                    await AgentGuardService.AcquireGeminiThrottleAsync();
                    try
                    {
                        result = await _ai.GenerateContentAsync(contents, systemInstruction, tools);
                        AgentGuardService.RecordGeminiCall(currentUserId);
                    }
                    finally
                    {
                        AgentGuardService.ReleaseGeminiThrottle();
                    }

                    var candidate = result["candidates"]?[0];
                    var contentObj = candidate?["content"] as JObject;
                    if (contentObj == null)
                    {
                        response.Reply = "Xin lỗi bạn, trợ lý chưa thể tạo câu trả lời lúc này. Bạn vui lòng thử lại nhé!";
                        await _convService.SaveMessageAsync(db, conversationId, "Assistant", response.Reply, null, null, "Empty content from API");
                        return response;
                    }

                    var parts = contentObj["parts"] as JArray;
                    if (parts == null || parts.Count == 0)
                    {
                        response.Reply = "Xin lỗi, không nhận được nội dung từ trợ lý.";
                        await _convService.SaveMessageAsync(db, conversationId, "Assistant", response.Reply);
                        return response;
                    }

                    JObject functionCall = null;
                    string textResponse = "";

                    foreach (var part in parts)
                    {
                        if (part["functionCall"] is JObject fc)
                        {
                            functionCall = fc;
                            break;
                        }
                        if (part["text"] != null)
                        {
                            textResponse += (string)part["text"];
                        }
                    }

                    // Nếu không có function call, lưu và trả về
                    if (functionCall == null)
                    {
                        response.Reply = !string.IsNullOrWhiteSpace(textResponse)
                            ? textResponse.Trim()
                            : "Xin lỗi, tôi chưa tạo được câu trả lời phù hợp.";

                        await _convService.SaveMessageAsync(db, conversationId, "Assistant", response.Reply, toolCallsJson, toolResultsJson);

                        // Cập nhật tiêu đề phiên nếu đây là tin đầu tiên
                        if (conv.MessageCount <= 2 && conv.Title == "Cuộc trò chuyện mới")
                        {
                            string autoTitle = message.Trim();
                            if (autoTitle.Length > 80) autoTitle = autoTitle.Substring(0, 80) + "...";
                            conv.Title = autoTitle;
                        }
                        conv.UpdatedAt = DateTime.UtcNow;
                        conv.LastMessageAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();

                        return response;
                    }

                    // Có function call → thực thi
                    contents.Add(contentObj);

                    string toolName = (string)functionCall["name"];
                    string functionCallId = (string)functionCall["id"] ?? "";
                    var args = functionCall["args"] as JObject ?? new JObject();

                    toolCallsJson = (toolCallsJson ?? "") + toolName + "(" + args.ToString(Formatting.None) + "); ";

                    JToken toolData = await ExecuteToolWithSecurityAsync(db, toolName, args, currentUserId, null, response, false, conversationId);

                    toolResultsJson = (toolResultsJson ?? "") + toolName + " → " + (toolData?.ToString(Formatting.None) ?? "null") + "; ";

                    var frObj = new JObject
                    {
                        ["name"] = toolName,
                        ["response"] = new JObject { ["result"] = toolData }
                    };

                    if (!string.IsNullOrEmpty(functionCallId))
                    {
                        frObj["id"] = functionCallId;
                    }

                    contents.Add(new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray { new JObject { ["functionResponse"] = frObj } }
                    });
                }

                response.Reply = "Yêu cầu của bạn cần qua nhiều bước xử lý. Bạn hãy kiểm tra lại thông tin hoặc đặt câu hỏi cụ thể hơn nhé!";
                await _convService.SaveMessageAsync(db, conversationId, "Assistant", response.Reply, toolCallsJson, toolResultsJson);
                conv.UpdatedAt = DateTime.UtcNow;
                conv.LastMessageAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return response;
            }
        }

        /// <summary>
        /// Thực thi tác vụ tự động chạy ngầm (AgentTasks)
        /// </summary>
        public async Task<string> ExecuteAutonomousTaskAsync(AgentTask task, long runId, bool isDryRun = false)
        {
            if (task == null) return "Task không hợp lệ.";

            int userId = task.UserID;
            string prompt = task.UserPrompt;

            var contents = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["parts"] = new JArray
                    {
                        new JObject
                        {
                            ["text"] = "[TÁC VỤ TỰ ĐỘNG CHẠY THEO LỊCH CỦA NGƯỜI DÙNG #" + userId + "]: " +
                                       (isDryRun ? "[CHẾ ĐỘ CHẠY THỬ / DRY-RUN: GIẢ LẬP KHÔNG GHI THẬT VÀO DB] " : "") +
                                       "BẮT BUỘC SỬ DỤNG FUNCTION CALL (ví dụ gọi 'create_post' nếu yêu cầu đăng bài). Yêu cầu: " + prompt
                        }
                    }
                }
            };

            var config = AgentConfigService.GetConfig();
            if (config.AgentStatus == "Disabled")
                return "Hệ thống AI Agent hiện đang bị tạm tắt bởi Quản trị viên.";
            if (config.AgentStatus == "Maintenance")
                return "Hệ thống AI Agent hiện đang trong chế độ bảo trì: " + config.StatusReason;

            var systemInstruction = GetSystemInstruction(userId);
            var tools = GetGeminiTools();
            string finalSummary = "Đã thực hiện tác vụ tự động.";
            int actionsExecutedCount = 0;
            int mutatingActionsCount = 0;
            int maxMutatingActions = config.MaxMutatingActionsPerRun > 0 ? config.MaxMutatingActionsPerRun : 5;
            string lastTextResponse = "";

            for (int round = 0; round < 4; round++)
            {
                JObject result;
                await AgentGuardService.AcquireGeminiThrottleAsync();
                try
                {
                    result = await _ai.GenerateContentAsync(contents, systemInstruction, tools);
                    AgentGuardService.RecordGeminiCall(userId);
                }
                finally
                {
                    AgentGuardService.ReleaseGeminiThrottle();
                }
                var candidate = result["candidates"]?[0];
                var contentObj = candidate?["content"] as JObject;
                if (contentObj == null) break;

                var parts = contentObj["parts"] as JArray;
                if (parts == null || parts.Count == 0) break;

                JObject functionCall = null;
                string textResponse = "";

                foreach (var part in parts)
                {
                    if (part["functionCall"] is JObject fc)
                    {
                        functionCall = fc;
                        break;
                    }
                    if (part["text"] != null)
                    {
                        textResponse += (string)part["text"];
                    }
                }

                if (!string.IsNullOrWhiteSpace(textResponse))
                {
                    lastTextResponse = textResponse.Trim();
                }

                if (functionCall == null)
                {
                    finalSummary = !string.IsNullOrWhiteSpace(textResponse) ? textResponse.Trim() : finalSummary;
                    break;
                }

                contents.Add(contentObj);

                string toolName = (string)functionCall["name"];
                string functionCallId = (string)functionCall["id"] ?? "";
                var args = functionCall["args"] as JObject ?? new JObject();

                bool isMutatingTool = toolName == "create_post" || toolName == "edit_post" || toolName == "delete_post" ||
                                      toolName == "react_to_post" || toolName == "add_comment" || toolName == "follow_user" ||
                                      toolName == "create_scheduled_task" || toolName == "update_task_status";

                if (isMutatingTool && mutatingActionsCount >= maxMutatingActions)
                {
                    var frObjLimit = new JObject
                    {
                        ["name"] = toolName,
                        ["response"] = new JObject
                        {
                            ["result"] = new JObject
                            {
                                ["error"] = $"Đã đạt giới hạn tối đa {maxMutatingActions} hành động ghi dữ liệu cho một lượt chạy tác vụ."
                            }
                        }
                    };
                    if (!string.IsNullOrEmpty(functionCallId)) frObjLimit["id"] = functionCallId;
                    contents.Add(new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray { new JObject { ["functionResponse"] = frObjLimit } }
                    });
                    continue;
                }

                JToken toolData;
                using (var db = new TDMUEcoSystemEntities())
                {
                    toolData = await ExecuteToolWithSecurityAsync(db, toolName, args, userId, runId, null, isDryRun);
                    actionsExecutedCount++;
                    if (isMutatingTool) mutatingActionsCount++;
                }

                var frObj = new JObject
                {
                    ["name"] = toolName,
                    ["response"] = new JObject { ["result"] = toolData }
                };

                if (!string.IsNullOrEmpty(functionCallId))
                {
                    frObj["id"] = functionCallId;
                }

                contents.Add(new JObject
                {
                    ["role"] = "user",
                    ["parts"] = new JArray { new JObject { ["functionResponse"] = frObj } }
                });
            }

            // FALLBACK ĐẢM BẢO: Nếu prompt yêu cầu đăng bài nhưng Gemini chỉ trả về text mà không gọi tool create_post
            bool isPostingPrompt = prompt.IndexOf("đăng", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  prompt.IndexOf("post", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  prompt.IndexOf("bài viết", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  prompt.IndexOf("chia sẻ", StringComparison.OrdinalIgnoreCase) >= 0;

            if (actionsExecutedCount == 0 && isPostingPrompt && !string.IsNullOrWhiteSpace(lastTextResponse))
            {
                if (isDryRun)
                {
                    finalSummary = "[DRY-RUN] Giả lập tự động đăng bài viết thành công. Nội dung: " + lastTextResponse;
                }
                else
                {
                    using (var db = new TDMUEcoSystemEntities())
                    {
                        var postResult = await _tools.CreatePostAsync(db, userId, lastTextResponse, "Public");
                        long newPostId = (long)(postResult["postId"] ?? 0);
                        await _auditService.LogActionAsync(
                            db, userId, "create_post", "Post", newPostId, "Succeeded",
                            "Auto-fallback post from prompt: " + prompt,
                            postResult.ToString(Formatting.None), null, runId
                        );
                        finalSummary = "Đã tự động đăng bài viết mới #" + newPostId + ": " + lastTextResponse;
                    }
                }
            }

            return finalSummary;
        }

        /// <summary>
        /// Bộ điều phối thực thi tool an toàn: Kiểm tra quyền, Ghi nhật ký kiểm toán, và Xác nhận hành động nhạy cảm
        /// </summary>
        private async Task<JToken> ExecuteToolWithSecurityAsync(
            TDMUEcoSystemEntities db,
            string toolName,
            JObject args,
            int userId,
            long? runId,
            ChatResponse chatContext,
            bool isDryRun = false,
            long? conversationId = null)
        {
            string inputSummary = args?.ToString(Formatting.None) ?? "{}";
            string targetType = "General";
            long? targetId = null;

            // 0. Kiểm tra trạng thái Agent toàn hệ thống (Kill-switch & Maintenance)
            var config = AgentConfigService.GetConfig();
            if (config.AgentStatus == "Disabled")
            {
                await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Agent toàn hệ thống đang bị vô hiệu hóa.", "KillSwitchActive", runId, conversationId);
                return new JObject { ["error"] = "Hệ thống Trợ lý ảo AI đang bị tạm dừng toàn hệ thống bởi Quản trị viên." };
            }
            if (config.AgentStatus == "Maintenance")
            {
                bool isMutating = toolName.StartsWith("create_") || toolName == "edit_post" || toolName == "delete_post" || toolName == "add_comment" || toolName == "react_to_post" || toolName == "follow_user";
                if (isMutating)
                {
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Hệ thống đang bảo trì, tạm dừng hành động ghi.", "MaintenanceActive", runId, conversationId);
                    return new JObject { ["error"] = "Hệ thống đang bảo trì. Các hành động tạo mới, cập nhật hoặc đăng bài tạm thời bị khóa." };
                }
            }

            // 1. Kiểm tra trường hợp khách vãng lai (Chưa đăng nhập)
            if (userId <= 0)
            {
                bool isPublicSearchTool = toolName == "search_posts" || toolName == "search_market" || toolName == "search_connect" || toolName == "search_stories" || toolName == "get_post_details";
                if (!isPublicSearchTool)
                {
                    return new JObject
                    {
                        ["error"] = "Tính năng này (đăng bài, tương tác hoặc lập lịch tác vụ) yêu cầu bạn phải đăng nhập vào tài khoản StudentConnect. " +
                        "Khi chưa đăng nhập, bạn có thể trò chuyện hỏi đáp kiến thức và tra cứu thông tin công khai (bài viết, Chợ sinh viên, phòng trọ)."
                    };
                }
            }
            else
            {
                // Kiểm tra quyền hạn người dùng đã đăng nhập
                var perm = await _permService.GetOrCreatePermissionsAsync(db, userId);
                if (perm == null || !perm.IsEnabled)
                {
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Trợ lý ảo đang bị tắt.", "Agent disabled", runId, conversationId);
                    return new JObject { ["error"] = "Trợ lý ảo hiện đang bị tạm tắt đối với tài khoản của bạn. Vui lòng bật lại trong Cài đặt quyền Agent." };
                }

                // Phân nhóm kiểm tra quyền
                bool isReadTool = toolName.StartsWith("search_") || toolName.StartsWith("get_");
                bool isManageTool = toolName.StartsWith("create_") || toolName == "edit_post" || toolName == "delete_post";
                bool isInteractTool = toolName == "react_to_post" || toolName == "add_comment" || toolName == "follow_user";
                bool isAutomationTool = toolName == "create_scheduled_task" || toolName == "list_scheduled_tasks" || toolName == "update_task_status";

                if (isReadTool && !perm.CanReadData)
                {
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Thiếu quyền CanReadData", "Permission denied", runId, conversationId);
                    return new JObject { ["error"] = "Bị từ chối: Bạn chưa cấp quyền 'Đọc dữ liệu' cho Trợ lý. Hãy vào Cài đặt quyền Agent để bật quyền này." };
                }

                if (isManageTool && !perm.CanManageContent)
                {
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Thiếu quyền CanManageContent", "Permission denied", runId, conversationId);
                    return new JObject { ["error"] = "Bị từ chối: Bạn chưa cấp quyền 'Quản lý nội dung' (Đăng/Sửa/Xóa bài) cho Trợ lý. Hãy vào Cài đặt quyền Agent để bật quyền này." };
                }

                if (isInteractTool && !perm.CanInteract)
                {
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Thiếu quyền CanInteract", "Permission denied", runId, conversationId);
                    return new JObject { ["error"] = "Bị từ chối: Bạn chưa cấp quyền 'Tương tác' (Thích, Bình luận, Theo dõi) cho Trợ lý. Hãy vào Cài đặt quyền Agent để bật quyền này." };
                }

                if (isAutomationTool && !perm.CanAutomation)
                {
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Thiếu quyền CanAutomation", "Permission denied", runId, conversationId);
                    return new JObject { ["error"] = "Bị từ chối: Bạn chưa cấp quyền 'Tác vụ tự động' (Automation) cho Trợ lý. Hãy vào Cài đặt quyền Agent để bật quyền này." };
                }

                // 1.1 Fencing Token Check: Ngăn chặn Worker cũ bị mất quyền (stale lease / zombie recovery) tiếp tục ghi
                bool isMutatingTool = isManageTool || isInteractTool || toolName == "create_scheduled_task";
                if (isMutatingTool && runId.HasValue && runId.Value > 0)
                {
                    bool isValidFencing = await AgentGuardService.VerifyFencingTokenAsync(db, runId.Value);
                    if (!isValidFencing)
                    {
                        await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Fencing token expired: Worker đã mất quyền sở hữu lease.", "FencingRevoked", runId, conversationId);
                        throw new InvalidOperationException($"[FENCING ERROR] Lượt chạy #{runId.Value} đã hết hạn lease hoặc bị thu hồi bởi Worker khác. Mọi thao tác ghi bị hủy ngay lập tức.");
                    }
                    // Gia hạn lease (Heartbeat) khi tác vụ còn đang chủ động hoạt động
                    await AgentGuardService.RenewTaskLeaseAsync(db, runId.Value);
                }
            }

            // 2. Chế độ DRY-RUN (Chạy thử nghiệm không ghi dữ liệu thật)
            if (isDryRun)
            {
                if (toolName == "create_post")
                {
                    string content = (string)args["content"] ?? "";
                    string privacy = (string)args["privacy"] ?? "Public";
                    await _auditService.LogActionAsync(db, userId, toolName, "Post", 0, "Succeeded", inputSummary, "[DRY-RUN] Giả lập tạo bài viết (không lưu DB): " + content, null, runId, conversationId);
                    return new JObject
                    {
                        ["success"] = true,
                        ["isDryRun"] = true,
                        ["postId"] = 0,
                        ["content"] = content,
                        ["privacy"] = privacy,
                        ["message"] = "[DRY-RUN] Giả lập tạo bài viết thành công. Nội dung xem trước: " + content
                    };
                }
                if (toolName == "edit_post" || toolName == "delete_post" || toolName == "react_to_post" || toolName == "add_comment" || toolName == "follow_user" || toolName == "update_task_status")
                {
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Succeeded", inputSummary, "[DRY-RUN] Giả lập thực thi tool " + toolName, null, runId);
                    return new JObject
                    {
                        ["success"] = true,
                        ["isDryRun"] = true,
                        ["message"] = "[DRY-RUN] Giả lập thực thi công cụ " + toolName + " thành công (không ghi DB)."
                    };
                }
            }

            // 3. Human-in-the-Loop Confirmation cho các hành động nhạy cảm (như delete_post)
            if (toolName == "delete_post")
            {
                int delPostId = (int)(args["postId"] ?? 0);
                string delItemType = (string)args["itemType"] ?? "Post";
                bool isConfirmed = (bool)(args["confirmed"] ?? false);
                targetType = delItemType;
                targetId = delPostId;

                // 1. Kiểm tra sự tồn tại và quyền sở hữu trước khi xóa hoặc tạo xác nhận
                var verify = await _tools.VerifyPostOwnershipAsync(db, delPostId, delItemType, userId);
                if (!(bool)(verify["exists"] ?? false))
                {
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Failed", inputSummary, "Không tìm thấy nội dung để xóa.", null, runId, conversationId);
                    return new JObject { ["error"] = (string)verify["error"] ?? ("Không tìm thấy bài viết ID #" + delPostId) };
                }

                if (!(bool)(verify["isOwner"] ?? false))
                {
                    string author = (string)verify["author"] ?? "người khác";
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Blocked", inputSummary, "Từ chối xóa bài của người khác: " + author, "Unauthorized deletion attempt", runId, conversationId);
                    return new JObject { ["error"] = "Bị từ chối: Bài viết ID #" + delPostId + " thuộc về " + author + ". Bạn chỉ có quyền xóa bài do chính tài khoản của bạn đăng." };
                }

                string postTitle = (string)verify["title"] ?? ("#" + delPostId);

                // 2. Nếu người dùng đã nói rõ xác nhận:
                if (isConfirmed)
                {
                    var delRes = await _tools.DeletePostAsync(db, delPostId, delItemType, userId);
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Succeeded", inputSummary, delRes.ToString(Formatting.None), null, runId, conversationId);
                    return delRes;
                }

                // 3. Nếu chưa xác nhận: tạo Pending Confirmation
                var pendingConf = await _confirmService.CreatePendingConfirmationAsync(
                    db,
                    userId,
                    "delete_post",
                    new { postId = delPostId, itemType = delItemType },
                    null,
                    15
                );

                if (chatContext != null)
                {
                    chatContext.RequiresConfirmation = true;
                    chatContext.ConfirmationId = pendingConf?.ConfirmationID;
                    chatContext.ActionName = "Xóa " + delItemType + " #" + delPostId;
                    chatContext.ActionDetails = "Xóa vĩnh viễn " + delItemType + " #" + delPostId + " (\"" + postTitle + "\") khỏi hệ thống.";
                }

                await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Pending", inputSummary, "Chờ người dùng xác nhận duyệt xóa " + delItemType + " #" + delPostId, null, runId, conversationId);

                return new JObject
                {
                    ["requires_confirmation"] = true,
                    ["confirmationId"] = pendingConf?.ConfirmationID ?? 0,
                    ["postId"] = delPostId,
                    ["itemType"] = delItemType,
                    ["title"] = postTitle,
                    ["message"] = "Tôi đã ghi nhận yêu cầu xóa " + delItemType + " ID #" + delPostId + " (\"" + postTitle + "\"). Bạn vui lòng bấm nút Xác nhận trên thẻ hoặc xác nhận trong tin nhắn để tôi xóa nhé!"
                };
            }

            // 4. Thực thi nghiệp vụ tương ứng
            try
            {
                JToken resultToken;

                switch (toolName)
                {
                    case "search_posts":
                        targetType = "Post";
                        resultToken = await _tools.SearchPostsAsync(
                            db,
                            (string)args["keyword"],
                            userId,
                            (string)args["author"],
                            (string)args["timeRange"],
                            (int)(args["limit"] ?? 5)
                        );
                        break;

                    case "search_market":
                        targetType = "MarketPost";
                        resultToken = await _tools.SearchMarketAsync(
                            db,
                            (string)args["keyword"],
                            (string)args["timeRange"],
                            (int)(args["limit"] ?? 5)
                        );
                        break;

                    case "search_connect":
                        targetType = "ConnectPost";
                        resultToken = await _tools.SearchConnectAsync(
                            db,
                            (string)args["keyword"],
                            (string)args["timeRange"],
                            (int)(args["limit"] ?? 5)
                        );
                        break;

                    case "search_stories":
                        targetType = "Story";
                        resultToken = await _tools.SearchStoriesAsync(
                            db,
                            (string)args["keyword"],
                            userId,
                            (int)(args["limit"] ?? 5)
                        );
                        break;

                    case "get_my_posts":
                        targetType = "MyPosts";
                        resultToken = await _tools.GetMyPostsAsync(
                            db,
                            userId,
                            (string)args["itemType"] ?? "all",
                            (string)args["keyword"],
                            (string)args["timeRange"],
                            (int)(args["limit"] ?? 10)
                        );
                        break;

                    case "create_market_post":
                        targetType = "MarketPost";
                        decimal? mPrice = null;
                        if (args["price"] != null)
                        {
                            decimal pVal;
                            if (decimal.TryParse(args["price"].ToString(), out pVal)) mPrice = pVal;
                        }
                        int? mCatId = args["categoryId"] != null ? (int?)args["categoryId"] : null;
                        resultToken = await _tools.CreateMarketPostAsync(
                            db,
                            userId,
                            (string)args["title"],
                            (string)args["description"],
                            mPrice,
                            (string)args["condition"] ?? "Còn dùng tốt",
                            (string)args["location"] ?? "Khuôn viên trường TDMU",
                            mCatId
                        );
                        if (resultToken["postId"] != null) targetId = (long)resultToken["postId"];
                        break;

                    case "create_connect_post":
                        targetType = "ConnectPost";
                        int? cCatId = args["categoryId"] != null ? (int?)args["categoryId"] : null;
                        resultToken = await _tools.CreateConnectPostAsync(
                            db,
                            userId,
                            (string)args["title"],
                            (string)args["description"],
                            cCatId
                        );
                        if (resultToken["postId"] != null) targetId = (long)resultToken["postId"];
                        break;

                    case "get_post_details":
                        int pId = (int)(args["postId"] ?? 0);
                        targetType = "Post";
                        targetId = pId;
                        resultToken = await _tools.GetPostDetailsAsync(db, pId, userId);
                        break;

                    case "get_recent_notifications":
                        targetType = "Notification";
                        int limit = (int)(args["limit"] ?? 5);
                        resultToken = await _tools.GetRecentNotificationsAsync(db, userId, limit);
                        break;

                    case "get_user_profile":
                        targetType = "User";
                        resultToken = await _tools.GetUserProfileAsync(db, (string)args["query"], userId);
                        break;

                    case "get_social_statistics":
                        targetType = "User";
                        targetId = userId;
                        resultToken = await _tools.GetSocialStatisticsAsync(db, userId);
                        break;

                    case "create_post":
                        targetType = "Post";
                        resultToken = await _tools.CreatePostAsync(db, userId, (string)args["content"], (string)args["privacy"] ?? "Public");
                        if (resultToken["postId"] != null) targetId = (long)resultToken["postId"];
                        break;

                    case "edit_post":
                        int editPostId = (int)(args["postId"] ?? 0);
                        targetType = "Post";
                        targetId = editPostId;
                        resultToken = await _tools.EditPostAsync(db, editPostId, userId, (string)args["newContent"], (string)args["newPrivacy"]);
                        break;

                    case "react_to_post":
                        int reactPostId = (int)(args["postId"] ?? 0);
                        targetType = "Post";
                        targetId = reactPostId;
                        resultToken = await _tools.ReactToPostAsync(db, reactPostId, userId, (string)args["reactionType"] ?? "Like");
                        break;

                    case "add_comment":
                        int cPostId = (int)(args["postId"] ?? 0);
                        targetType = "Post";
                        targetId = cPostId;
                        resultToken = await _tools.AddCommentAsync(db, cPostId, userId, (string)args["content"]);
                        break;

                    case "follow_user":
                        int fUserId = (int)(args["targetUserId"] ?? 0);
                        targetType = "User";
                        targetId = fUserId;
                        resultToken = await _tools.FollowUserAsync(db, fUserId, userId);
                        break;

                    case "create_scheduled_task":
                        targetType = "AgentTask";
                        resultToken = await _tools.CreateScheduledTaskAsync(
                            db,
                            userId,
                            (string)args["taskName"],
                            (string)args["userPrompt"],
                            (string)args["scheduleType"],
                            (string)args["scheduleConfig"]
                        );
                        if (resultToken["taskId"] != null) targetId = (long)resultToken["taskId"];
                        break;

                    case "list_scheduled_tasks":
                        targetType = "AgentTask";
                        resultToken = await _tools.ListScheduledTasksAsync(db, userId);
                        break;

                    case "update_task_status":
                        long uTaskId = (long)(args["taskId"] ?? 0);
                        targetType = "AgentTask";
                        targetId = uTaskId;
                        resultToken = await _tools.UpdateTaskStatusAsync(db, uTaskId, userId, (string)args["newStatus"]);
                        break;

                    default:
                        resultToken = new JObject { ["error"] = "Công cụ không được hỗ trợ: " + toolName };
                        break;
                }

                // Ghi audit log thành công
                await _auditService.LogActionAsync(
                    db,
                    userId,
                    toolName,
                    targetType,
                    targetId,
                    "Succeeded",
                    inputSummary,
                    resultToken?.ToString(Formatting.None),
                    null,
                    runId,
                    conversationId
                );

                return resultToken;
            }
            catch (Exception ex)
            {
                await _auditService.LogActionAsync(
                    db,
                    userId,
                    toolName,
                    targetType,
                    targetId,
                    "Failed",
                    inputSummary,
                    null,
                    ex.Message,
                    runId,
                    conversationId
                );

                return new JObject { ["error"] = "Lỗi khi thực thi công cụ: " + ex.Message };
            }
        }
    }
}
