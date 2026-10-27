using System;

namespace StudentConnect.Models.Assistant
{
    public class ChatResponse
    {
        public bool Success { get; set; }
        public string AgentName { get; set; } = "Trợ lý ảo Mr. T";
        public string Reply { get; set; }
        public string Error { get; set; }

        // Trường phục vụ Human-in-the-Loop Confirmation
        public bool RequiresConfirmation { get; set; }
        public long? ConfirmationId { get; set; }
        public string ActionName { get; set; }
        public string ActionDetails { get; set; }

        // Trường phục vụ quản lý phiên hội thoại
        public long? ConversationId { get; set; }
    }
}