using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Web.Hosting;

namespace StudentConnect.Models.Assistant
{
    public static class AgentConfigService
    {
        private static readonly object _syncLock = new object();
        private static AgentConfigModel _cachedConfig;
        private static string _configFilePath;

        private static string ConfigFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_configFilePath))
                {
                    string appData = HostingEnvironment.MapPath("~/App_Data") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
                    if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
                    _configFilePath = Path.Combine(appData, "agent_config.json");
                }
                return _configFilePath;
            }
        }

        public static AgentConfigModel GetConfig()
        {
            if (_cachedConfig != null) return _cachedConfig;

            lock (_syncLock)
            {
                if (_cachedConfig != null) return _cachedConfig;

                try
                {
                    if (File.Exists(ConfigFilePath))
                    {
                        string json = File.ReadAllText(ConfigFilePath);
                        _cachedConfig = JsonConvert.DeserializeObject<AgentConfigModel>(json);
                    }
                }
                catch { }

                if (_cachedConfig == null)
                {
                    _cachedConfig = CreateDefaultConfig();
                    SaveConfigInternal(_cachedConfig);
                }

                SanitizeConfig(_cachedConfig);
                EnsureDefaultTools(_cachedConfig);
                return _cachedConfig;
            }
        }

        public static void SaveConfig(AgentConfigModel model, string updatedBy = "Admin")
        {
            if (model == null) return;

            lock (_syncLock)
            {
                SanitizeConfig(model);
                model.LastUpdatedUtc = DateTime.UtcNow;
                model.LastUpdatedBy = updatedBy;
                EnsureDefaultTools(model);
                _cachedConfig = model;
                SaveConfigInternal(model);
            }
        }

        public static void SanitizeConfig(AgentConfigModel c)
        {
            if (c == null) return;

            // Đảm bảo trạng thái Agent hợp lệ
            if (c.AgentStatus != "Active" && c.AgentStatus != "Maintenance" && c.AgentStatus != "Disabled")
            {
                c.AgentStatus = "Active";
            }

            // Hạn mức tác vụ & an toàn tài nguyên
            c.MaxActiveTasksPerUser = Math.Max(1, Math.Min(c.MaxActiveTasksPerUser, 50));
            c.MaxDailyTaskRunsPerUser = Math.Max(1, Math.Min(c.MaxDailyTaskRunsPerUser, 500));
            c.MaxConcurrentRunsPerUser = Math.Max(1, Math.Min(c.MaxConcurrentRunsPerUser, 10));
            c.MinIntervalBetweenRunsMinutes = Math.Max(1, Math.Min(c.MinIntervalBetweenRunsMinutes, 1440));
            c.MaxMutatingActionsPerRun = Math.Max(1, Math.Min(c.MaxMutatingActionsPerRun, 50));
            if (c.MaxTaskRetryAttempts <= 0) c.MaxTaskRetryAttempts = 3;
            else c.MaxTaskRetryAttempts = Math.Min(c.MaxTaskRetryAttempts, 10);
            c.TaskTimeoutSeconds = Math.Max(10, Math.Min(c.TaskTimeoutSeconds, 600));

            // Hạn mức mạng xã hội & chống spam
            c.MaxDailyPostsPerUser = Math.Max(0, Math.Min(c.MaxDailyPostsPerUser, 100));
            c.MaxDailyCommentsPerUser = Math.Max(0, Math.Min(c.MaxDailyCommentsPerUser, 500));
            c.MaxDailyReactionsPerUser = Math.Max(0, Math.Min(c.MaxDailyReactionsPerUser, 1000));
            c.MaxDailyFollowsPerUser = Math.Max(0, Math.Min(c.MaxDailyFollowsPerUser, 500));
            c.DuplicateCheckWindowHours = Math.Max(1, Math.Min(c.DuplicateCheckWindowHours, 72));
            if (c.DuplicateSimilarityThreshold <= 0.0 || c.DuplicateSimilarityThreshold > 1.0)
            {
                c.DuplicateSimilarityThreshold = 0.85;
            }

            // Hạn mức Gemini API & tài nguyên
            c.MaxDailyGeminiRequestsPerUser = Math.Max(1, Math.Min(c.MaxDailyGeminiRequestsPerUser, 10000));
            c.MaxDailyGeminiRequestsGlobal = Math.Max(10, Math.Min(c.MaxDailyGeminiRequestsGlobal, 100000));
            c.MaxConcurrentGeminiRequestsGlobal = Math.Max(1, Math.Min(c.MaxConcurrentGeminiRequestsGlobal, 50));
        }

        private static void SaveConfigInternal(AgentConfigModel model)
        {
            try
            {
                string json = JsonConvert.SerializeObject(model, Formatting.Indented);
                string tempPath = ConfigFilePath + ".tmp_" + Guid.NewGuid().ToString("N");
                File.WriteAllText(tempPath, json);
                if (File.Exists(ConfigFilePath))
                {
                    File.Delete(ConfigFilePath);
                }
                File.Move(tempPath, ConfigFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AgentConfigService] Save error: " + ex.Message);
            }
        }

        private static AgentConfigModel CreateDefaultConfig()
        {
            var config = new AgentConfigModel
            {
                AgentName = ConfigurationManager.AppSettings["GeminiAgentName"] ?? "Trợ lý ảo Mr. T",
                PrimaryModel = ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-3.5-flash-lite",
                Temperature = 0.7,
                MaxOutputTokens = 2048,
                AgentStatus = "Active",
                SchedulerEnabled = true,
                SchedulerIntervalSeconds = 30
            };

            EnsureDefaultTools(config);
            return config;
        }

        private static void EnsureDefaultTools(AgentConfigModel config)
        {
            if (config.Tools == null) config.Tools = new Dictionary<string, ToolSettingItem>();

            var defaultList = new[]
            {
                new ToolSettingItem { ToolName = "search_posts", DisplayName = "Tìm kiếm bài viết", Description = "Tra cứu bài đăng sinh viên trên Feed", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "search_market", DisplayName = "Tìm kiếm chợ sinh viên", Description = "Tra cứu sách vở, laptop, đồ dùng thanh lý", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "search_connect", DisplayName = "Tìm kiếm kết nối", Description = "Tìm phòng trọ, ở ghép, học nhóm, CLB", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "search_stories", DisplayName = "Tìm kiếm Story", Description = "Tìm tin 24h của sinh viên", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "get_my_posts", DisplayName = "Xem bài của tôi", Description = "Lấy danh sách bài cá nhân", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "create_post", DisplayName = "Đăng bài viết mới", Description = "Tạo bài đăng Feed theo yêu cầu", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "like_post", DisplayName = "Thích bài viết", Description = "Thả tim/thích bài đăng", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "add_comment", DisplayName = "Bình luận bài viết", Description = "Gửi lời bình luận vào bài", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "follow_user", DisplayName = "Theo dõi sinh viên", Description = "Follow hoặc hủy follow người dùng", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "delete_post", DisplayName = "Xóa bài viết", Description = "Xóa bài viết đã đăng (Thao tác nhạy cảm)", IsEnabled = true, RequireConfirmation = true, IsSensitive = true },
                new ToolSettingItem { ToolName = "create_scheduled_task", DisplayName = "Tạo tác vụ tự động", Description = "Lập lịch công việc chạy định kỳ", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "list_scheduled_tasks", DisplayName = "Xem danh sách tác vụ", Description = "Liệt kê lịch trình của tôi", IsEnabled = true, RequireConfirmation = false, IsSensitive = false },
                new ToolSettingItem { ToolName = "update_task_status", DisplayName = "Đổi trạng thái tác vụ", Description = "Tạm dừng, kích hoạt, hủy tác vụ", IsEnabled = true, RequireConfirmation = false, IsSensitive = false }
            };

            foreach (var item in defaultList)
            {
                if (!config.Tools.ContainsKey(item.ToolName))
                {
                    config.Tools[item.ToolName] = item;
                }
            }
        }
    }
}
