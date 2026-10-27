with open(r'E:\LTWEB\StudentConnect\scratch\VerifyGeminiChatModeration.cs', 'r', encoding='utf-8') as f:
    text = f.read()

old_p6 = """                // =================================================================
                // PHẦN 6: ATOMIC ROLLBACK KHI UPLOAD BATCH
                // =================================================================
                Console.WriteLine("\\n>>> [PHẦN 6] ATOMIC ROLLBACK BATCH UPLOAD");

                // Giả lập logic batch upload của ChatController:
                // Upload 3 tệp: [file1 hợp lệ, file2 vi phạm bạo lực, file3 hợp lệ]
                // Nếu file2 bị từ chối, toàn bộ file đã lưu tạm phải bị hủy và dọn dẹp sạch sẽ
                var batchStagedPaths = new List<string>();
                string bFile1 = Path.Combine(testDir, "batch_1.png");
                string bFile2 = Path.Combine(testDir, "batch_2.txt");
                string bFile3 = Path.Combine(testDir, "batch_3.png");

                File.Copy(validPngPath, bFile1);
                File.WriteAllText(bFile2, "Tao sẽ mang súng và dao đến trường giết chết cả lớp mày hôm nay!", Encoding.UTF8);
                File.Copy(validPngPath, bFile3);

                var batchFiles = new[]
                {
                    new { Path = bFile1, Name = "b1.png", Mime = "image/png", IsImg = true },
                    new { Path = bFile2, Name = "b2.txt", Mime = "text/plain", IsImg = false },
                    new { Path = bFile3, Name = "b3.png", Mime = "image/png", IsImg = true }
                };

                bool batchSuccess = true;
                string batchErrorMessage = "";

                foreach (var item in batchFiles)
                {
                    batchStagedPaths.Add(item.Path);
                    var mod = await ChatFileModerationHelper.ValidateAndModerateFileAsync(item.Path, item.Name, item.Mime, item.IsImg, 999, "Tester", 1);
                    if (!mod.IsApproved)
                    {
                        batchSuccess = false;
                        batchErrorMessage = mod.UserFriendlyMessage;
                        // Atomic Rollback
                        foreach (var path in batchStagedPaths)
                        {
                            try { if (File.Exists(path)) File.Delete(path); } catch { }
                        }
                        break;
                    }
                }

                bool allBatchCleaned = !File.Exists(bFile1) && !File.Exists(bFile2) && !File.Exists(bFile3);
                AssertCheck("TEST-6.1", "Atomic Rollback: Batch có 1 tệp vi phạm thì hủy toàn bộ và dọn sạch",
                    !batchSuccess && allBatchCleaned,
                    $"BatchSuccess={batchSuccess}, AllCleaned={allBatchCleaned}, Message=\"{batchErrorMessage}\"");"""

new_p6 = """                // =================================================================
                // PHẦN 6: ATOMIC ROLLBACK KHI UPLOAD BATCH
                // =================================================================
                Console.WriteLine("\\n>>> [PHẦN 6] ATOMIC ROLLBACK BATCH UPLOAD");

                // Giả lập logic batch upload thực tế trong ChatController:
                // Người dùng gửi 3 tệp. Khi lặp qua từng tệp: tệp 1 lưu tạm -> đạt;
                // tệp 2 lưu tạm -> vi phạm -> kích hoạt Atomic Rollback xóa sạch toàn bộ tệp tạm của batch.
                var batchSources = new[]
                {
                    new { Name = "b1.png", Mime = "image/png", IsImg = true, Content = "" },
                    new { Name = "b2.txt", Mime = "text/plain", IsImg = false, Content = "Tao sẽ mang súng và dao đến trường giết chết cả lớp mày hôm nay!" },
                    new { Name = "b3.png", Mime = "image/png", IsImg = true, Content = "" }
                };

                var batchStagedPaths = new List<string>();
                bool batchSuccess = true;
                string batchErrorMessage = "";

                for (int i = 0; i < batchSources.Length; i++)
                {
                    string staged = Path.Combine(testDir, $"batch_staged_{i}_{batchSources[i].Name}");
                    if (batchSources[i].IsImg)
                        File.Copy(validPngPath, staged);
                    else
                        File.WriteAllText(staged, batchSources[i].Content, Encoding.UTF8);

                    batchStagedPaths.Add(staged);

                    var mod = await ChatFileModerationHelper.ValidateAndModerateFileAsync(staged, batchSources[i].Name, batchSources[i].Mime, batchSources[i].IsImg, 999, "Tester", 1);
                    if (!mod.IsApproved)
                    {
                        batchSuccess = false;
                        batchErrorMessage = mod.UserFriendlyMessage;
                        // Atomic Rollback (dọn sạch các tệp đã lưu tạm trong batch)
                        foreach (var path in batchStagedPaths)
                        {
                            try { if (File.Exists(path)) File.Delete(path); } catch { }
                        }
                        break;
                    }
                }

                bool allStagedCleaned = batchStagedPaths.All(p => !File.Exists(p));
                AssertCheck("TEST-6.1", "Atomic Rollback: Batch có 1 tệp vi phạm thì hủy toàn bộ và dọn sạch",
                    !batchSuccess && allStagedCleaned,
                    $"BatchSuccess={batchSuccess}, AllStagedCleaned={allStagedCleaned}, Message=\"{batchErrorMessage}\"");"""

assert old_p6 in text, 'old_p6 not found'
text = text.replace(old_p6, new_p6)

with open(r'E:\LTWEB\StudentConnect\scratch\VerifyGeminiChatModeration.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print('Updated TEST-6.1 successfully!')
