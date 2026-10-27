using System;
using System.Collections.Generic;

namespace StudentConnect.Models
{
    public class ChatAttachmentItem
    {
        public string FileName { get; set; }
        public string Extension { get; set; }
        public long FileSize { get; set; }
        public string FormattedSize { get; set; }
        public bool IsImage { get; set; }
        public string Url { get; set; }
        public string FileKey { get; set; }
        public string ContentType { get; set; }
    }

    public class ChatAttachmentsPayload
    {
        public string Text { get; set; }
        public List<ChatAttachmentItem> Items { get; set; } = new List<ChatAttachmentItem>();
    }
}
