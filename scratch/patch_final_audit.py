with open(r'E:\LTWEB\StudentConnect\scratch\FinalAuditGeminiModeration.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Patch AUDIT-4.1
old_4_1 = """                // 4.1: RecordRateLimitFailure (429 / RESOURCE_EXHAUSTED) lập tức ngắt mạch
                ChatFileModerationHelper.RecordRateLimitFailure();
                bool cbOpenOnRateLimit = ChatFileModerationHelper.IsCircuitBreakerOpen();
                var swRateLimit = System.Diagnostics.Stopwatch.StartNew();
                var fastFailRes = await ChatFileModerationHelper.ModerateImageWithAiAsync(validImg, "image/png");
                swRateLimit.Stop();"""

new_4_1 = """                // 4.1: RecordRateLimitFailure (429 / RESOURCE_EXHAUSTED) lập tức ngắt mạch
                ChatFileModerationHelper.ClearModerationCache();
                ChatFileModerationHelper.RecordRateLimitFailure();
                bool cbOpenOnRateLimit = ChatFileModerationHelper.IsCircuitBreakerOpen();
                var swRateLimit = System.Diagnostics.Stopwatch.StartNew();
                var fastFailRes = await ChatFileModerationHelper.ModerateImageWithAiAsync(validImg, "image/png");
                swRateLimit.Stop();"""

assert old_4_1 in text, "old_4_1 not found"
text = text.replace(old_4_1, new_4_1, 1)

# 2. Patch AUDIT-6.1
old_6_1 = """                var stagedBatchList = new List<string>();
                string bFileA = Path.Combine(testDir, "batch_fileA.png");
                string bFileB = Path.Combine(testDir, "batch_fileB.txt"); // vi phạm
                string bFileC = Path.Combine(testDir, "batch_fileC.png");

                File.Copy(validImg, bFileA);
                File.WriteAllText(bFileB, "Tao sẽ mang dao và súng đến trường giết cả lớp mày!", Encoding.UTF8);
                File.Copy(validImg, bFileC);

                var batchItems = new[]
                {
                    new { Path = bFileA, Name = "fA.png", Mime = "image/png", IsImg = true },
                    new { Path = bFileB, Name = "fB.txt", Mime = "text/plain", IsImg = false },
                    new { Path = bFileC, Name = "fC.png", Mime = "image/png", IsImg = true }
                };

                bool batchApproved = true;
                foreach (var item in batchItems)
                {
                    stagedBatchList.Add(item.Path);
                    var res = await ChatFileModerationHelper.ValidateAndModerateFileAsync(item.Path, item.Name, item.Mime, item.IsImg, 101, "UserA", 1);
                    if (!res.IsApproved)
                    {
                        batchApproved = false;
                        // Rollback toàn bộ
                        foreach (var p in stagedBatchList)
                        {
                            try { if (File.Exists(p)) File.Delete(p); } catch { }
                        }
                        break;
                    }
                }

                bool allStagedDeleted = !File.Exists(bFileA) && !File.Exists(bFileB) && !File.Exists(bFileC);
                AssertCheck("AUDIT-6.1", "Batch Upload: 1 tệp vi phạm hủy toàn bộ batch và xóa sạch tệp tạm",
                    !batchApproved && allStagedDeleted,
                    $"BatchApproved={batchApproved}, AllStagedDeleted={allStagedDeleted}");"""

new_6_1 = """                var batchSources = new[]
                {
                    new { Name = "fA.png", Mime = "image/png", IsImg = true, Content = "" },
                    new { Name = "fB.txt", Mime = "text/plain", IsImg = false, Content = "Tao sẽ mang dao và súng đến trường giết cả lớp mày!" },
                    new { Name = "fC.png", Mime = "image/png", IsImg = true, Content = "" }
                };

                var stagedBatchList = new List<string>();
                bool batchApproved = true;

                for (int i = 0; i < batchSources.Length; i++)
                {
                    string staged = Path.Combine(testDir, $"staged_{i}_{batchSources[i].Name}");
                    if (batchSources[i].IsImg)
                        File.Copy(validImg, staged);
                    else
                        File.WriteAllText(staged, batchSources[i].Content, Encoding.UTF8);

                    stagedBatchList.Add(staged);
                    var res = await ChatFileModerationHelper.ValidateAndModerateFileAsync(staged, batchSources[i].Name, batchSources[i].Mime, batchSources[i].IsImg, 101, "UserA", 1);
                    if (!res.IsApproved)
                    {
                        batchApproved = false;
                        // Rollback toàn bộ tệp tạm đã lưu trong batch
                        foreach (var p in stagedBatchList)
                        {
                            try { if (File.Exists(p)) File.Delete(p); } catch { }
                        }
                        break;
                    }
                }

                bool allStagedDeleted = stagedBatchList.All(p => !File.Exists(p));
                AssertCheck("AUDIT-6.1", "Batch Upload: 1 tệp vi phạm hủy toàn bộ batch và xóa sạch tệp tạm",
                    !batchApproved && allStagedDeleted,
                    $"BatchApproved={batchApproved}, AllStagedDeleted={allStagedDeleted}");"""

assert old_6_1 in text, "old_6_1 not found"
text = text.replace(old_6_1, new_6_1, 1)

# 3. Patch AUDIT-7.1
old_7_1 = """                        // 7.1: Path Traversal trong fileKey
                        var ptRes = chatCtrl.DownloadAttachment(1, "../../../secret.txt") as HttpStatusCodeResult;
                        bool passPt = ptRes != null && ptRes.StatusCode == 400;
                        AssertCheck("AUDIT-7.1", "Chặn Path Traversal trong tham số fileKey (HTTP 400)",
                            passPt, $"StatusCode: {ptRes?.StatusCode}");"""

new_7_1 = """                        // 7.1: Path Traversal trong fileKey
                        var existingMsg = db.ChatMessages.FirstOrDefault();
                        int targetMsgId = existingMsg != null ? existingMsg.MessageID : 1;
                        var ptRes = chatCtrl.DownloadAttachment(targetMsgId, "../../../secret.txt") as HttpStatusCodeResult;
                        bool passPt = ptRes != null && ptRes.StatusCode == 400;
                        AssertCheck("AUDIT-7.1", "Chặn Path Traversal trong tham số fileKey (HTTP 400)",
                            passPt, $"TargetMsgId={targetMsgId}, StatusCode: {ptRes?.StatusCode}");"""

assert old_7_1 in text, "old_7_1 not found"
text = text.replace(old_7_1, new_7_1, 1)

with open(r'E:\LTWEB\StudentConnect\scratch\FinalAuditGeminiModeration.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("FinalAuditGeminiModeration.cs patched successfully!")
