namespace StudentConnect.Models
{
    using System;
    using System.Collections.Generic;

    public partial class AgentConversation
    {
        public AgentConversation()
        {
            this.AgentMessages = new HashSet<AgentMessage>();
        }

        public long ConversationID { get; set; }
        public int UserID { get; set; }
        public string Title { get; set; }
        public Nullable<long> TaskID { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public int MessageCount { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public System.DateTime UpdatedAt { get; set; }
        public System.DateTime LastMessageAt { get; set; }

        public virtual ICollection<AgentMessage> AgentMessages { get; set; }
    }
}
