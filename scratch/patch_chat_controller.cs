using System;
using System.IO;

class Program {
    static void Main() {
        string path = "Controllers\\ChatController.cs";
        string content = File.ReadAllText(path);
        
        string target = "private bool IsUserChatRestricted(int userId, out string blockMessage, out string restrictionType)";
        int idx = content.IndexOf(target);
        if (idx < 0) {
            Console.WriteLine("Target not found!");
            return;
        }
        
        // Find closing brace of IsUserChatRestricted
        int bodyStart = content.IndexOf('{', idx);
        int depth = 1;
        int bodyEnd = -1;
        for (int i = bodyStart + 1; i < content.Length; i++) {
            if (content[i] == '{') depth++;
            else if (content[i] == '}') {
                depth--;
                if (depth == 0) {
                    bodyEnd = i;
                    break;
                }
            }
        }
        
        if (bodyEnd < 0) {
            Console.WriteLine("Could not find matching closing brace!");
            return;
        }
        
        string methodCode = content.Substring(idx, bodyEnd - idx + 1);
        Console.WriteLine("Found method from index " + idx + " to " + bodyEnd + " (length: " + methodCode.Length + ")");
        
        string replacement = @"private bool IsUserChatRestricted(int userId, out string blockMessage, out string restrictionType)
        {
            blockMessage = null;
            restrictionType = null;
            if (userId <= 0) return false;

            try
            {
                var now = DateTime.Now;
                var activeRestriction = db.ChatRestrictions
                    .Where(r => r.UserID == userId && r.IsActive)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();

                if (activeRestriction == null) return false;

                if (activeRestriction.ExpiresAt.HasValue && activeRestriction.ExpiresAt.Value <= now)
                {
                    activeRestriction.IsActive = false;
                    activeRestriction.RevokedAt = now;
                    activeRestriction.RevokeReason = ""Tự động mở cấm do đã hết thời hạn phạt"";
                    db.SaveChanges();
                    return false;
                }

                restrictionType = activeRestriction.RestrictionType;
                string timeInfo = activeRestriction.ExpiresAt.HasValue
                    ? $""đến {activeRestriction.ExpiresAt.Value:HH:mm dd/MM/yyyy}""
                    : ""vĩnh viễn"";

                string typeName = restrictionType == ""Mute"" ? ""tạm khóa tính năng gửi tin nhắn"" : ""cấm tham gia trò chuyện"";
                string reasonInfo = string.IsNullOrWhiteSpace(activeRestriction.Reason) ? ""Vi phạm quy tắc cộng đồng"" : activeRestriction.Reason;

                blockMessage = $""Tài khoản của bạn đang bị {typeName} ({timeInfo}). Lý do: {reasonInfo}."";
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(" + "\"[IsUserChatRestricted] Error checking restriction: \" + ex.Message);" + @"
                return false;
            }
        }";
        
        string newContent = content.Substring(0, idx) + replacement + content.Substring(bodyEnd + 1);
        File.WriteAllText(path, newContent, System.Text.Encoding.UTF8);
        Console.WriteLine("Successfully replaced IsUserChatRestricted with defensive try-catch!");
    }
}
