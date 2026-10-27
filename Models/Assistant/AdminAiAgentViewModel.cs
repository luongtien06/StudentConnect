using StudentConnect.Models;
using System;
using System.Collections.Generic;

namespace StudentConnect.Models.Assistant
{
    public class OverviewStats
    {
        public int TotalTasks { get; set; }
        public int ActiveTasks { get; set; }
        public int PausedTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int CancelledTasks { get; set; }

        public int TotalRuns { get; set; }
        public int SucceededRuns { get; set; }
        public int FailedRuns { get; set; }
        public double SuccessRate { get; set; }

        public int TotalLogs { get; set; }
        public int TodayLogs { get; set; }

        public bool IsSchedulerRunning { get; set; }
        public DateTime? LastSchedulerTick { get; set; }
        public WorkerHeartbeatInfo WorkerHeartbeat { get; set; }

        public List<AgentTaskRun> RecentFailedRuns { get; set; } = new List<AgentTaskRun>();
        public List<AgentActionLog> RecentErrorLogs { get; set; } = new List<AgentActionLog>();
    }

    public class SubAdminPermissionItem
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string AdminRole { get; set; }
        public string Avatar { get; set; }

        public bool IsEnabled { get; set; }
        public bool CanAutomation { get; set; }
        public bool CanManageContent { get; set; }
        public bool CanInteract { get; set; }
        public bool CanReadData { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminAiAgentViewModel
    {
        // Phân quyền của người đang đăng nhập
        public bool IsSuperAdmin { get; set; }
        public bool CanAutomation { get; set; }
        public bool CanManageContent { get; set; }
        public bool CanInteract { get; set; }
        public bool CanReadData { get; set; }
        public bool CanDeleteLogs { get; set; }

        // Tab 0: Tổng quan
        public OverviewStats Stats { get; set; } = new OverviewStats();

        // Tab 1: Quản lý tác vụ & lịch chạy
        public List<AgentTask> Tasks { get; set; } = new List<AgentTask>();

        // Tab 2: Nhật ký hoạt động
        public List<AgentActionLog> Logs { get; set; } = new List<AgentActionLog>();

        // Tab 3: Cấu hình Agent
        public AgentConfigModel Config { get; set; } = new AgentConfigModel();

        // Tab 4: API & Phân quyền
        public string MaskedApiKey { get; set; }
        public bool HasApiKey { get; set; }
        public List<SubAdminPermissionItem> SubAdmins { get; set; } = new List<SubAdminPermissionItem>();
    }
}
