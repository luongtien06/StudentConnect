import os

path = r"Areas\Admin\Controllers\AdminChatController.cs"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

target = "// 3. QUẢN LÝ BÁO CÁO VI PHẠM (VIOLATION REPORTS)"
if target not in content:
    print("Target section 3 header not found!")
    exit(1)

# Find the header banner right before section 3
header_banner = "// ════════════════════════════════════════════════════════════════"
pos = content.rfind(header_banner, 0, content.find(target))
if pos == -1:
    pos = content.find(target)

new_method = """        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult AdminDeleteMultipleMessages(List<int> ids, string reason = "Xóa hàng loạt bởi Quản trị viên")
        {
            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "Không có tin nhắn nào được chọn." });
            }

            var validIds = ids.Where(id => id > 0).Distinct().Take(200).ToList();
            if (!validIds.Any())
            {
                return Json(new { success = false, message = "Danh sách ID tin nhắn không hợp lệ." });
            }

            try
            {
                var messages = db.ChatMessages.Where(m => validIds.Contains(m.MessageID)).ToList();
                if (!messages.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy tin nhắn hợp lệ cần xóa (có thể đã bị xóa trước đó)." });
                }

                var roomMessageMap = messages
                    .Where(m => m.RoomID.HasValue)
                    .GroupBy(m => m.RoomID.Value)
                    .ToDictionary(g => g.Key, g => g.Select(m => m.MessageID).ToList());

                // 1. Dọn dẹp file vật lý
                foreach (var msg in messages)
                {
                    if (!string.IsNullOrEmpty(msg.ImageUrl))
                    {
                        try
                        {
                            string fullPath = Server.MapPath("~" + msg.ImageUrl);
                            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(msg.Content) && msg.Content.Contains("[ATTACHMENTS_PAYLOAD]:"))
                    {
                        try
                        {
                            var parsed = ParseMessageContent(msg.Content);
                            if (parsed.attachments != null)
                            {
                                foreach (var att in parsed.attachments)
                                {
                                    if (!string.IsNullOrEmpty(att.Url) && att.Url.StartsWith("/"))
                                    {
                                        string p = Server.MapPath("~" + att.Url);
                                        if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                // 2. Thực hiện xóa trong Transaction
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        var foundIds = messages.Select(m => m.MessageID).ToList();
                        var relatedReports = db.ChatReports.Where(r => r.MessageID.HasValue && foundIds.Contains(r.MessageID.Value)).ToList();
                        foreach (var rep in relatedReports)
                        {
                            var targetMsg = messages.FirstOrDefault(m => m.MessageID == rep.MessageID.Value);
                            if (string.IsNullOrEmpty(rep.EvidenceSnippet))
                            {
                                rep.EvidenceSnippet = "[Tin nhắn gốc đã bị BQT xóa hàng loạt] " + (targetMsg?.Content ?? "");
                            }
                            rep.MessageID = null;
                        }

                        db.ChatMessages.RemoveRange(messages);
                        db.SaveChanges();
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }

                // 3. Thông báo SignalR thời gian thực
                try
                {
                    var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                    foreach (var kvp in roomMessageMap)
                    {
                        int roomId = kvp.Key;
                        foreach (int msgId in kvp.Value)
                        {
                            hubContext.Clients.Group(roomId.ToString()).onMessageDeleted(msgId);
                        }
                    }
                }
                catch { }

                LogAuditAction("AdminDeleteMultipleMessages", "ChatMessage", null, $"Đã xóa hàng loạt {messages.Count} tin nhắn vi phạm. Lý do: {reason}");

                return Json(new
                {
                    success = true,
                    count = messages.Count,
                    message = $"Đã xóa vĩnh viễn {messages.Count} tin nhắn thành công!"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("AdminDeleteMultipleMessages failed: " + ex.Message);
                return Json(new { success = false, message = "Lỗi khi xóa hàng loạt tin nhắn: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        """

updated_content = content[:pos] + new_method + content[pos:]
with open(path, "w", encoding="utf-8") as f:
    f.write(updated_content)

print("Successfully patched AdminChatController.cs!")
