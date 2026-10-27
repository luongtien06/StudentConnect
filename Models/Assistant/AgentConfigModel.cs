using System;
using System.Collections.Generic;

namespace StudentConnect.Models.Assistant
{
    public class ToolSettingItem
    {
        public string ToolName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool RequireConfirmation { get; set; } = false;
        public bool IsSensitive { get; set; } = false;
    }

    public class AgentConfigModel
    {
        public string AgentName { get; set; } = "Trợ lý ảo Mr. T";
        public string PrimaryModel { get; set; } = "gemini-3.5-flash-lite";
        public List<string> FallbackModels { get; set; } = new List<string>
        {
            "gemini-flash-lite-latest",
            "gemini-3.1-flash-lite",
            "gemini-3.5-flash",
            "gemini-3.8-flash"
        };

        public double Temperature { get; set; } = 0.7;
        public int MaxOutputTokens { get; set; } = 2048;
        public double TopP { get; set; } = 0.95;
        public int TopK { get; set; } = 40;

        public string AgentStatus { get; set; } = "Active"; // Active | Maintenance | Disabled
        public string StatusReason { get; set; } = "";

        public string CustomSystemPrompt { get; set; } = "";

        public bool SchedulerEnabled { get; set; } = true;
        public int SchedulerIntervalSeconds { get; set; } = 30;

        // =========================================================================
        // HẠN MỨC TÁC VỤ & BẢO VỆ TÀI NGUYÊN (TASK QUOTAS & RESOURCE PROTECTION)
        // =========================================================================
        public int MaxActiveTasksPerUser { get; set; } = 5;          // Mặc định tối đa 5 tác vụ đang hoạt động / user
        public int MaxDailyTaskRunsPerUser { get; set; } = 10;       // Mặc định tối đa 10 lần thực thi / ngày / user
        public int MaxConcurrentRunsPerUser { get; set; } = 1;       // Mỗi người dùng chỉ có tối đa 1 tác vụ chạy đồng thời
        public int MinIntervalBetweenRunsMinutes { get; set; } = 5;  // Tối thiểu 5 phút giữa hai lần chạy cùng tác vụ
        public int MaxMutatingActionsPerRun { get; set; } = 5;       // Mỗi lượt chạy không vượt quá 5 hành động ghi dữ liệu
        public int MaxTaskRetryAttempts { get; set; } = 3;           // Tối đa 3 lần retry cho tác vụ lỗi
        public int TaskTimeoutSeconds { get; set; } = 90;            // Timeout tối đa cho 1 lượt chạy tác vụ (90 giây)

        // =========================================================================
        // HẠN MỨC HÀNH ĐỘNG MẠNG XÃ HỘI (ANTI-SPAM SOCIAL ACTIONS)
        // =========================================================================
        public int MaxDailyPostsPerUser { get; set; } = 5;           // Tối đa đăng 5 bài / ngày / user qua Agent
        public int MaxDailyCommentsPerUser { get; set; } = 20;       // Tối đa 20 bình luận / ngày / user qua Agent
        public int MaxDailyReactionsPerUser { get; set; } = 30;      // Tối đa 30 lượt thích / ngày / user qua Agent
        public int MaxDailyFollowsPerUser { get; set; } = 10;        // Tối đa 10 lượt follow / ngày / user qua Agent
        public int DuplicateCheckWindowHours { get; set; } = 2;      // Khoảng thời gian kiểm tra nội dung trùng lặp (2 giờ)
        public double DuplicateSimilarityThreshold { get; set; } = 0.85; // Ngưỡng tương đồng phát hiện trùng lặp (85%)

        // =========================================================================
        // HẠN MỨC GEMINI API & BẢO VỆ CHI PHÍ (GEMINI RATE LIMITS)
        // =========================================================================
        public int MaxDailyGeminiRequestsPerUser { get; set; } = 50;  // Tối đa 50 lượt gọi Gemini / ngày / user
        public int MaxDailyGeminiRequestsGlobal { get; set; } = 1000; // Tối đa 1000 lượt gọi Gemini / ngày toàn hệ thống
        public int MaxConcurrentGeminiRequestsGlobal { get; set; } = 5; // Tối đa 5 request Gemini chạy đồng thời toàn hệ thống

        public Dictionary<string, ToolSettingItem> Tools { get; set; } = new Dictionary<string, ToolSettingItem>();

        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
        public string LastUpdatedBy { get; set; } = "System";
    }

    public class ApiTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int LatencyMs { get; set; }
        public string ModelUsed { get; set; }
        public DateTime TestedAt { get; set; } = DateTime.Now;
    }
}
