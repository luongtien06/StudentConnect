using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace StudentConnect.Models.Assistant
{
    public class ChatRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Message { get; set; }

        public long? ConversationId { get; set; }
    }
}