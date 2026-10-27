namespace StudentConnect.Models
{
    using System;

    public partial class AgentMessage
    {
        public long MessageID { get; set; }
        public long ConversationID { get; set; }
        public string SenderType { get; set; }
        public string Content { get; set; }
        public string ToolCallDetails { get; set; }
        public string ToolResultDetails { get; set; }
        public string ErrorMessage { get; set; }
        public System.DateTime CreatedAt { get; set; }

        public virtual AgentConversation AgentConversation { get; set; }
    }
}
