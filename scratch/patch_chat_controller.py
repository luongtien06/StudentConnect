path = r"E:\LTWEB\StudentConnect\Controllers\ChatController.cs"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

content = content.replace("\r\n", "\n")

# 1. Update SendImage with CleanOrphanedTempFiles call
old_sendimage_start = '''            string tempInspectionDir = Server.MapPath("~/App_Data/TempFileInspection");
            if (!Directory.Exists(tempInspectionDir))
                Directory.CreateDirectory(tempInspectionDir);'''

new_sendimage_start = '''            string tempInspectionDir = Server.MapPath("~/App_Data/TempFileInspection");
            if (!Directory.Exists(tempInspectionDir))
                Directory.CreateDirectory(tempInspectionDir);
            ChatFileModerationHelper.CleanOrphanedTempFiles(tempInspectionDir);'''

content = content.replace(old_sendimage_start, new_sendimage_start)

# 2. Update SendMessageWithFiles staging and CleanOrphanedTempFiles
old_sendfiles_staging = '''            var stagedPaths = new List<string>();
            var attachmentItems = new List<ChatAttachmentItem>();

            try
            {
                // Bước 1: Lưu tạm và kiểm duyệt nghiêm ngặt từng tệp
                foreach (var file in validFiles)
                {
                    string safeBaseName = ChatFileModerationHelper.SanitizeFileName(file.FileName);
                    string tempFileName = $"{Guid.NewGuid():N}_{safeBaseName}";
                    string tempFilePath = Path.Combine(tempInspectionDir, tempFileName);

                    file.SaveAs(tempFilePath);
                    stagedPaths.Add(tempFilePath);

                    string ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
                    bool isImage = ChatFileModerationHelper.AllowedImageExtensions.Contains(ext);

                    var modResult = await ChatFileModerationHelper.ValidateAndModerateFileAsync(
                        tempFilePath,
                        file.FileName,
                        file.ContentType,
                        isImage,
                        myId,
                        myName,
                        roomId
                    );

                    if (!modResult.IsApproved)
                    {
                        // Hủy bỏ và dọn dẹp sạch toàn bộ các tệp đã upload trong lượt này
                        foreach (var path in stagedPaths)
                        {
                            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }
                        }
                        return Json(new { success = false, message = modResult.UserFriendlyMessage });
                    }

                    // Tệp đã được duyệt an toàn -> chuyển sang thư mục đích
                    string uniqueKey = $"{Guid.NewGuid():N}_{safeBaseName}";
                    if (isImage)
                    {
                        string permPath = Path.Combine(chatUploadsDir, uniqueKey);
                        System.IO.File.Move(tempFilePath, permPath);

                        attachmentItems.Add(new ChatAttachmentItem
                        {
                            FileName = file.FileName,
                            Extension = ext,
                            FileSize = file.ContentLength,
                            FormattedSize = FormatFileSize(file.ContentLength),
                            IsImage = true,
                            Url = "/Content/Uploads/Chat/" + uniqueKey,
                            FileKey = uniqueKey,
                            ContentType = file.ContentType
                        });
                    }
                    else
                    {
                        string permPath = Path.Combine(secureAttachmentsDir, uniqueKey);
                        System.IO.File.Move(tempFilePath, permPath);

                        attachmentItems.Add(new ChatAttachmentItem
                        {
                            FileName = file.FileName,
                            Extension = ext,
                            FileSize = file.ContentLength,
                            FormattedSize = FormatFileSize(file.ContentLength),
                            IsImage = false,
                            Url = null,
                            FileKey = uniqueKey,
                            ContentType = file.ContentType
                        });
                    }
                }'''

new_sendfiles_staging = '''            ChatFileModerationHelper.CleanOrphanedTempFiles(tempInspectionDir);

            var stagedPaths = new List<string>();
            var stagedItems = new List<Tuple<HttpPostedFileBase, string, string, bool, string>>();
            var attachmentItems = new List<ChatAttachmentItem>();

            try
            {
                // Bước 1: Lưu tạm và kiểm duyệt nghiêm ngặt từng tệp
                // Nguyên tắc nguyên khối (Atomic Transaction): Nếu BẤT KỲ tệp nào vi phạm, hủy bỏ toàn bộ và xóa sạch
                foreach (var file in validFiles)
                {
                    string safeBaseName = ChatFileModerationHelper.SanitizeFileName(file.FileName);
                    string tempFileName = $"{Guid.NewGuid():N}_{safeBaseName}";
                    string tempFilePath = Path.Combine(tempInspectionDir, tempFileName);

                    file.SaveAs(tempFilePath);
                    stagedPaths.Add(tempFilePath);

                    string ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
                    bool isImage = ChatFileModerationHelper.AllowedImageExtensions.Contains(ext);

                    var modResult = await ChatFileModerationHelper.ValidateAndModerateFileAsync(
                        tempFilePath,
                        file.FileName,
                        file.ContentType,
                        isImage,
                        myId,
                        myName,
                        roomId
                    );

                    if (!modResult.IsApproved)
                    {
                        // Hủy bỏ và dọn dẹp sạch toàn bộ các tệp đã upload trong lượt này
                        foreach (var path in stagedPaths)
                        {
                            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }
                        }
                        return Json(new { success = false, message = modResult.UserFriendlyMessage });
                    }

                    stagedItems.Add(Tuple.Create(file, tempFilePath, ext, isImage, safeBaseName));
                }

                // Bước 2: TẤT CẢ các tệp trong lượt đều được duyệt an toàn -> chuyển sang thư mục đích vĩnh viễn
                foreach (var item in stagedItems)
                {
                    var file = item.Item1;
                    var tempFilePath = item.Item2;
                    var ext = item.Item3;
                    var isImage = item.Item4;
                    var safeBaseName = item.Item5;

                    string uniqueKey = $"{Guid.NewGuid():N}_{safeBaseName}";
                    if (isImage)
                    {
                        string permPath = Path.Combine(chatUploadsDir, uniqueKey);
                        System.IO.File.Move(tempFilePath, permPath);

                        attachmentItems.Add(new ChatAttachmentItem
                        {
                            FileName = file.FileName,
                            Extension = ext,
                            FileSize = file.ContentLength,
                            FormattedSize = FormatFileSize(file.ContentLength),
                            IsImage = true,
                            Url = "/Content/Uploads/Chat/" + uniqueKey,
                            FileKey = uniqueKey,
                            ContentType = file.ContentType
                        });
                    }
                    else
                    {
                        string permPath = Path.Combine(secureAttachmentsDir, uniqueKey);
                        System.IO.File.Move(tempFilePath, permPath);

                        attachmentItems.Add(new ChatAttachmentItem
                        {
                            FileName = file.FileName,
                            Extension = ext,
                            FileSize = file.ContentLength,
                            FormattedSize = FormatFileSize(file.ContentLength),
                            IsImage = false,
                            Url = null,
                            FileKey = uniqueKey,
                            ContentType = file.ContentType
                        });
                    }
                }'''

content = content.replace(old_sendfiles_staging, new_sendfiles_staging)

# 3. Update DownloadAttachment to enforce cross-message fileKey ownership (Anti-IDOR)
old_download_check = '''            string downloadFileName = System.IO.Path.GetFileName(fullPath);
            try
            {
                string rawContent = msg.Content ?? "";
                if (rawContent.StartsWith("[SHOW_ID]:")) rawContent = rawContent.Substring(10);
                if (rawContent.StartsWith("[ATTACHMENTS_PAYLOAD]:"))
                {
                    var json = rawContent.Substring("[ATTACHMENTS_PAYLOAD]:".Length);
                    var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatAttachmentsPayload>(json);
                    var item = payload?.Items?.FirstOrDefault(i => i.FileKey == fileKey);
                    if (item != null && !string.IsNullOrEmpty(item.FileName))
                    {
                        downloadFileName = item.FileName;
                    }
                }
            }
            catch { }'''

new_download_check = '''            // Xác thực nghiêm ngặt quyền sở hữu tệp đối với tin nhắn cụ thể (Chống tấn công IDOR / Cross-Message Parameter Tampering)
            bool fileBelongsToMessage = false;
            string downloadFileName = System.IO.Path.GetFileName(fullPath);

            if (!string.IsNullOrEmpty(msg.ImageUrl))
            {
                string imgFileName = System.IO.Path.GetFileName(msg.ImageUrl);
                if (string.Equals(imgFileName, fileKey, StringComparison.OrdinalIgnoreCase) ||
                    msg.ImageUrl.EndsWith("/" + fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    fileBelongsToMessage = true;
                }
            }

            try
            {
                string rawContent = msg.Content ?? "";
                if (rawContent.StartsWith("[SHOW_ID]:")) rawContent = rawContent.Substring(10);
                if (rawContent.StartsWith("[ATTACHMENTS_PAYLOAD]:"))
                {
                    var json = rawContent.Substring("[ATTACHMENTS_PAYLOAD]:".Length);
                    var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatAttachmentsPayload>(json);
                    var item = payload?.Items?.FirstOrDefault(i => string.Equals(i.FileKey, fileKey, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        fileBelongsToMessage = true;
                        if (!string.IsNullOrEmpty(item.FileName))
                        {
                            downloadFileName = item.FileName;
                        }
                    }
                }
            }
            catch { }

            if (!fileBelongsToMessage)
            {
                return new HttpStatusCodeResult(403, "Tệp đính kèm không thuộc về tin nhắn này.");
            }'''

content = content.replace(old_download_check, new_download_check)

content = content.replace("\n", "\r\n")

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("Updated Controllers/ChatController.cs successfully")
