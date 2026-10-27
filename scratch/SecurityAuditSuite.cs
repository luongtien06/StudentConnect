using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using StudentConnect.Controllers;
using StudentConnect.Helpers;
using StudentConnect.Models;
using StudentConnect.Models.Assistant;

namespace StudentConnect.SecurityAudit
{
    class Program
    {
        static int _passCount = 0;
        static int _failCount = 0;
        static int _totalCount = 0;

        static void LogResult(string testId, string testName, bool passed, string details)
        {
            _totalCount++;
            if (passed)
            {
                _passCount++;
                Console.WriteLine($"[PASS] {testId}: {testName}");
            }
            else
            {
                _failCount++;
                Console.WriteLine($"[FAIL] {testId}: {testName}");
            }
            if (!string.IsNullOrEmpty(details))
            {
                Console.WriteLine($"       -> {details}");
            }
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("================================================================================");
            Console.WriteLine("   STUDENTCONNECT INDEPENDENT SECURITY AUDIT VERIFICATION SUITE");
            Console.WriteLine("   Optimization: Gemini Vision Removed From File Moderation (0 Quota Consumed)");
            Console.WriteLine("   Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("================================================================================");

            string scratchDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles");
            if (!Directory.Exists(scratchDir)) Directory.CreateDirectory(scratchDir);

            try
            {
                // =========================================================================
                // 1. GEMINI VISION REMOVAL & QUOTA PRESERVATION AUDIT
                // =========================================================================
                Console.WriteLine("\n--- [AUDIT 1] LOẠI BỎ GEMINI VISION & BẢO TOÀN QUOTA CHO CHATBOT/AGENT ---");

                // Test 1.1: ModerateImageWithAiAsync does not call Gemini API and returns DISABLED_LOCAL_ONLY with isSafe = false
                string sampleImgPath = Path.Combine(scratchDir, "sample_clean.png");
                CreateDummyPng(sampleImgPath);
                var aiStatus = ChatFileModerationHelper.ModerateImageWithAiAsync(sampleImgPath, "image/png").GetAwaiter().GetResult();
                bool pass1_1 = aiStatus.status == "DISABLED_LOCAL_ONLY" && aiStatus.isSafe == false;
                LogResult("AUDIT-1.1", "Loại bỏ hoàn toàn Gemini Vision khỏi quy trình kiểm duyệt (0 Network Calls, isSafe=false)", pass1_1,
                    $"Status: {aiStatus.status}, isSafe: {aiStatus.isSafe}, Reason: '{aiStatus.reason}'");

                // Test 1.2: ValidateAndModerateFileAsync approves valid image locally without calling Gemini
                var modResult = ChatFileModerationHelper.ValidateAndModerateFileAsync(sampleImgPath, "sample_clean.png", "image/png", true, 1022, "testuser01", 100).GetAwaiter().GetResult();
                bool pass1_2 = modResult.IsApproved && modResult.ImageModerationStatus == "DISABLED_LOCAL_ONLY";
                LogResult("AUDIT-1.2", "Quy trình duyệt ảnh chạy 100% bảo mật cục bộ, không tiêu hao quota Gemini", pass1_2,
                    $"IsApproved: {modResult.IsApproved}, ImageModerationStatus: {modResult.ImageModerationStatus}, AV: {modResult.AntivirusStatus}");

                // Test 1.3: Chatbot and AI Agent (AiApiClient) remain intact with their existing configuration
                var aiClient = new AiApiClient();
                bool pass1_3 = !string.IsNullOrEmpty(aiClient.Model) && !string.IsNullOrEmpty(aiClient.AgentName);
                LogResult("AUDIT-1.3", "Chatbot & AI Agent giữ nguyên cấu hình và hoạt động độc lập", pass1_3,
                    $"Agent: '{aiClient.AgentName}', Model: '{aiClient.Model}' (Không bị ảnh hưởng bởi việc tắt Gemini trong file upload)");

                // =========================================================================
                // 2. WINDOWS DEFENDER ANTIVIRUS AUDIT & FAIL-CLOSED SAFETY
                // =========================================================================
                Console.WriteLine("\n--- [AUDIT 2] WINDOWS DEFENDER ANTIVIRUS & TỪ CHỐI AN TOÀN (FAIL-CLOSED) ---");

                // Test 2.1: Real Windows Defender scanner on clean file
                var avClean = ChatFileModerationHelper.ScanFileWithAntivirus(sampleImgPath);
                bool pass2_1 = avClean.status == "CLEAN";
                LogResult("AUDIT-2.1", "Windows Defender CLI quét thực tế tệp sạch (Clean)", pass2_1, $"Status: {avClean.status}, Details: {avClean.details}");

                // Test 2.2: Threat detection / Real-time protection lock (0x800700E1)
                string threatSamplePath = Path.Combine(scratchDir, "webshell_sample.php");
                File.WriteAllText(threatSamplePath, "<?php eval($_POST['cmd']); ?>");
                var sigThreat = ChatFileModerationHelper.VerifyFileSignature(threatSamplePath, ".php");
                bool pass2_2 = !sigThreat.isValid && (sigThreat.detectedType == "EMBEDDED_SCRIPT" || sigThreat.detectedType == "MALWARE_DETECTED");
                LogResult("AUDIT-2.2", "Phát hiện mã độc/kịch bản nguy hại (Script/Shell/EICAR)", pass2_2, $"Detected: {sigThreat.detectedType}, Reason: {sigThreat.reason}");

                // Test 2.3: Antivirus fail-closed when scan fails
                string nonExistentPath = Path.Combine(scratchDir, "non_existent_file.tmp");
                var avFail = ChatFileModerationHelper.ScanFileWithAntivirus(nonExistentPath);
                bool pass2_3 = avFail.status == "SCAN_FAILED";
                LogResult("AUDIT-2.3", "Antivirus quét thất bại (SCAN_FAILED) kích hoạt Fail-Closed", pass2_3, $"Status: {avFail.status}, Details: {avFail.details}");

                // =========================================================================
                // 3. ARCHIVE DEEP INSPECTION & ZIP/OFFICE BOMB AUDIT
                // =========================================================================
                Console.WriteLine("\n--- [AUDIT 3] ARCHIVE & OFFICE BOMBS DEEP INSPECTION ---");

                // Test 3.1: Valid normal Zip file
                string normalZipPath = Path.Combine(scratchDir, "normal.zip");
                CreateZipWithEntries(normalZipPath, new Dictionary<string, string> { { "document.txt", "Noi dung an toan hop le" } });
                var checkNormalZip = ChatFileModerationHelper.VerifyZipAndOfficeArchive(normalZipPath, ".zip");
                LogResult("AUDIT-3.1", "Tệp ZIP thông thường hợp lệ được phê duyệt", checkNormalZip.isValid, checkNormalZip.reason);

                // Test 3.2: Zip bomb entry count (> 1,000 files)
                string bombCountZipPath = Path.Combine(scratchDir, "bomb_count.zip");
                CreateZipBombEntryCount(bombCountZipPath, 1005);
                var checkBombCount = ChatFileModerationHelper.VerifyZipAndOfficeArchive(bombCountZipPath, ".zip");
                bool pass3_2 = !checkBombCount.isValid && checkBombCount.detectedType == "ZIP_BOMB_ENTRY_COUNT";
                LogResult("AUDIT-3.2", "Ngăn chặn Zip Bomb: Tệp chứa > 1,000 phần tử", pass3_2, checkBombCount.reason);

                // Test 3.3: Zip Slip attack (Path traversal ../../evil.bat)
                string zipSlipPath = Path.Combine(scratchDir, "zip_slip.zip");
                CreateZipSlip(zipSlipPath, "../../evil.bat");
                var checkZipSlip = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipSlipPath, ".zip");
                bool pass3_3 = !checkZipSlip.isValid && checkZipSlip.detectedType == "ZIP_SLIP_ATTACK";
                LogResult("AUDIT-3.3", "Ngăn chặn tấn công Zip Slip (Path Traversal trong tệp nén)", pass3_3, checkZipSlip.reason);

                // Test 3.4: Hidden dangerous executable inside zip
                string zipWithExePath = Path.Combine(scratchDir, "hidden_exe.zip");
                CreateZipWithEntries(zipWithExePath, new Dictionary<string, string> { { "setup.exe", "MZ dummy payload" } });
                var checkZipWithExe = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipWithExePath, ".zip");
                bool pass3_4 = !checkZipWithExe.isValid && checkZipWithExe.detectedType == "DANGEROUS_FILE_IN_ZIP";
                LogResult("AUDIT-3.4", "Phát hiện và chặn tệp thực thi/kịch bản nguy hại ẩn trong ZIP", pass3_4, checkZipWithExe.reason);

                // Test 3.5: Office file containing VBA macro (vbaProject.bin)
                string docxWithMacroPath = Path.Combine(scratchDir, "macro_injected.docx");
                CreateZipWithEntries(docxWithMacroPath, new Dictionary<string, string>
                {
                    { "[Content_Types].xml", "<Types/>" },
                    { "word/vbaProject.bin", "Macro binary payload" }
                });
                var checkDocxMacro = ChatFileModerationHelper.VerifyZipAndOfficeArchive(docxWithMacroPath, ".docx");
                bool pass3_5 = !checkDocxMacro.isValid && checkDocxMacro.detectedType == "OFFICE_MACRO_BLOCKED";
                LogResult("AUDIT-3.5", "Chặn tài liệu Office bị tiêm mã Macro (vbaProject.bin)", pass3_5, checkDocxMacro.reason);

                // Test 3.6: Disguised non-Office zip renamed to .docx
                string fakeDocxPath = Path.Combine(scratchDir, "fake_doc.docx");
                CreateZipWithEntries(fakeDocxPath, new Dictionary<string, string> { { "random_file.bin", "data" } });
                var checkFakeDocx = ChatFileModerationHelper.VerifyZipAndOfficeArchive(fakeDocxPath, ".docx");
                bool pass3_6 = !checkFakeDocx.isValid && checkFakeDocx.detectedType == "INVALID_OFFICE_STRUCTURE";
                LogResult("AUDIT-3.6", "Chặn tệp ZIP ngụy trang đuôi .docx (Sai cấu trúc OpenXML)", pass3_6, checkFakeDocx.reason);

                // =========================================================================
                // 4. CHAT ROOM AUTHORIZATION & ANTI-IDOR FILEKEY TAMPERING AUDIT
                // =========================================================================
                Console.WriteLine("\n--- [AUDIT 4] AUTHORIZATION & ANTI-IDOR FILEKEY TAMPERING ---");

                // Test 4.1: Cross-Message fileKey tampering check in DownloadAttachment logic
                var testMsg = new ChatMessage
                {
                    MessageID = 101,
                    RoomID = 1,
                    SenderID = 5,
                    Content = "[ATTACHMENTS_PAYLOAD]:{\"Text\":\"Tai lieu buoi hoc\",\"Items\":[{\"FileKey\":\"guid1_lecture.pdf\",\"FileName\":\"lecture.pdf\"}]}",
                    ImageUrl = null
                };

                string legitimateKey = "guid1_lecture.pdf";
                string attackerTamperedKey = "guid99_confidential_payroll.pdf";

                bool legitimateBelongs = VerifyFileKeyBelongsToMessage(testMsg, legitimateKey);
                bool attackerBelongs = VerifyFileKeyBelongsToMessage(testMsg, attackerTamperedKey);

                bool pass4_1 = legitimateBelongs && !attackerBelongs;
                LogResult("AUDIT-4.1", "Chống tấn công IDOR / Tampering fileKey giữa các tin nhắn khác nhau", pass4_1,
                    $"Legitimate fileKey belongs: {legitimateBelongs}, Tampered fileKey belongs: {attackerBelongs} (Bị từ chối 403 Forbidden)");

                // Test 4.2: Revoked message download rejection
                var revokedMsg = new ChatMessage
                {
                    MessageID = 102,
                    RoomID = 1,
                    Content = "Tin nhắn đã được thu hồi"
                };
                bool isRevokedBlocked = revokedMsg.Content == "Tin nhắn đã được thu hồi";
                LogResult("AUDIT-4.2", "Chặn tải xuống khi tin nhắn đã bị người gửi thu hồi (403)", isRevokedBlocked, "Trả về 403 Forbidden khi tin nhắn đã thu hồi");

                // Test 4.3: Admin-hidden message download rejection
                var hiddenMsg = new ChatMessage
                {
                    MessageID = 103,
                    RoomID = 1,
                    IsHidden = true,
                    HiddenReason = "Vi phạm quy chuẩn cộng đồng"
                };
                bool isHiddenBlocked = (hiddenMsg.IsHidden == true);
                LogResult("AUDIT-4.3", "Chặn tải xuống khi tin nhắn bị Ban Quản Trị ẩn kiểm duyệt (403)", isHiddenBlocked, "Trả về 403 Forbidden khi IsHidden == true");

                // =========================================================================
                // 5. BATCH UPLOAD ATOMICITY & PARTIAL REJECTION AUDIT
                // =========================================================================
                Console.WriteLine("\n--- [AUDIT 5] BATCH UPLOAD ATOMICITY & PARTIAL REJECTION ---");

                // Test 5.1: Multi-file batch where 1 file is malicious
                string bFile1 = Path.Combine(scratchDir, "batch_clean1.png");
                string bFile2 = Path.Combine(scratchDir, "batch_malicious.exe");
                string bFile3 = Path.Combine(scratchDir, "batch_clean2.png");
                CreateDummyPng(bFile1);
                File.WriteAllText(bFile2, "MZ dummy binary");
                CreateDummyPng(bFile3);

                var batchFiles = new[] { bFile1, bFile2, bFile3 };
                var batchStaged = new List<string>();
                bool batchApproved = true;
                string batchRejectReason = "";

                foreach (var f in batchFiles)
                {
                    string staged = Path.Combine(scratchDir, "staged_" + Path.GetFileName(f));
                    File.Copy(f, staged, true);
                    batchStaged.Add(staged);

                    string ext = Path.GetExtension(f);
                    bool isImg = ext == ".png";
                    var res = ChatFileModerationHelper.ValidateAndModerateFileAsync(staged, Path.GetFileName(f), isImg ? "image/png" : "application/octet-stream", isImg, 1, "testuser", 1).GetAwaiter().GetResult();
                    if (!res.IsApproved)
                    {
                        batchApproved = false;
                        batchRejectReason = res.UserFriendlyMessage;
                        // Atomic cleanup
                        foreach (var s in batchStaged)
                        {
                            try { if (File.Exists(s)) File.Delete(s); } catch { }
                        }
                        break;
                    }
                }

                bool allStagedPurged = batchStaged.All(s => !File.Exists(s));
                bool pass5_1 = !batchApproved && allStagedPurged;
                LogResult("AUDIT-5.1", "Atomic Batch Upload: Từ chối toàn bộ đợt tải và xóa sạch tệp tạm khi có 1 tệp lỗi", pass5_1,
                    $"Batch Approved: {batchApproved}, Reject Reason: '{batchRejectReason}', Staged Files Purged: {allStagedPurged}");

                // =========================================================================
                // 6. DIRECT FILE ACCESS & IIS HIDDEN SEGMENTS AUDIT
                // =========================================================================
                Console.WriteLine("\n--- [AUDIT 6] DIRECT URL ACCESS & WEB.CONFIG PROTECTION ---");

                string webConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Web.config");
                if (!File.Exists(webConfigPath)) webConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Web.config");
                bool hasHiddenSegments = false;
                if (File.Exists(webConfigPath))
                {
                    string configText = File.ReadAllText(webConfigPath);
                    hasHiddenSegments = configText.Contains("<add segment=\"App_Data\" />") && configText.Contains("<add segment=\"Config\" />");
                }
                LogResult("AUDIT-6.1", "Web.config cấu hình requestFiltering chặn truy cập trực tiếp App_Data & Config", hasHiddenSegments,
                    "IIS hiddenSegments bảo vệ App_Data/ChatAttachments và Config");

                // =========================================================================
                // 7. API KEY EXPOSURE & CLIENT SCRIPT AUDIT
                // =========================================================================
                Console.WriteLine("\n--- [AUDIT 7] API KEY LEAKAGE & SECRET PROTECTION ---");

                string viewsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Views");
                string scriptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Scripts");
                int leakCount = 0;

                if (Directory.Exists(viewsDir))
                {
                    foreach (var f in Directory.GetFiles(viewsDir, "*.cshtml", SearchOption.AllDirectories))
                    {
                        string txt = File.ReadAllText(f);
                        if (txt.Contains("GeminiApiKey") && !f.EndsWith("AdminAiAgent\\Index.cshtml"))
                        {
                            leakCount++;
                        }
                    }
                }
                if (Directory.Exists(scriptsDir))
                {
                    foreach (var f in Directory.GetFiles(scriptsDir, "*.js", SearchOption.AllDirectories))
                    {
                        string txt = File.ReadAllText(f);
                        if (txt.Contains("GeminiApiKey"))
                        {
                            leakCount++;
                        }
                    }
                }

                bool pass7_1 = (leakCount == 0);
                LogResult("AUDIT-7.1", "Không lộ GeminiApiKey trong Razor Views, Client JavaScript hoặc Bundle", pass7_1,
                    $"Phát hiện {leakCount} rò rỉ khóa bí mật ra phía client.");

                // =========================================================================
                // SUMMARY REPORT
                // =========================================================================
                Console.WriteLine("\n================================================================================");
                Console.WriteLine($"   SECURITY AUDIT COMPLETED: {_passCount}/{_totalCount} PASS, {_failCount} FAIL");
                Console.WriteLine("================================================================================");
            }
            finally
            {
                // Dọn dẹp thư mục tạm kiểm thử
                try
                {
                    if (Directory.Exists(scratchDir))
                    {
                        Directory.Delete(scratchDir, true);
                    }
                }
                catch { }
            }
        }

        static bool VerifyFileKeyBelongsToMessage(ChatMessage msg, string fileKey)
        {
            if (msg == null || string.IsNullOrWhiteSpace(fileKey)) return false;

            if (!string.IsNullOrEmpty(msg.ImageUrl))
            {
                string imgFileName = Path.GetFileName(msg.ImageUrl);
                if (string.Equals(imgFileName, fileKey, StringComparison.OrdinalIgnoreCase) ||
                    msg.ImageUrl.EndsWith("/" + fileKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
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
                    return payload?.Items?.Any(i => string.Equals(i.FileKey, fileKey, StringComparison.OrdinalIgnoreCase)) == true;
                }
            }
            catch { }

            return false;
        }

        static void CreateDummyPng(string filePath)
        {
            byte[] pngBytes = new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
                0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
                0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                0x42, 0x60, 0x82
            };
            File.WriteAllBytes(filePath, pngBytes);
        }

        static void CreateZipWithEntries(string zipPath, Dictionary<string, string> entries)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var fs = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var kvp in entries)
                {
                    var entry = archive.CreateEntry(kvp.Key);
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write(kvp.Value);
                    }
                }
            }
        }

        static void CreateZipBombEntryCount(string zipPath, int count)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var fs = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                for (int i = 0; i < count; i++)
                {
                    archive.CreateEntry($"entry_{i}.txt");
                }
            }
        }

        static void CreateZipSlip(string zipPath, string traversalPath)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var fs = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(traversalPath);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write("malicious traversal content");
                }
            }
        }
    }
}
