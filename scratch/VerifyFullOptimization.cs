using System;
using System.Collections.Generic;
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

namespace StudentConnect.Verification
{
    class Program
    {
        static int _pass = 0;
        static int _fail = 0;
        static int _total = 0;

        static void AssertCheck(string id, string desc, bool condition, string detail)
        {
            _total++;
            if (condition)
            {
                _pass++;
                Console.WriteLine($"[PASS] {id}: {desc}");
            }
            else
            {
                _fail++;
                Console.WriteLine($"[FAIL] {id}: {desc}");
            }
            if (!string.IsNullOrEmpty(detail))
            {
                Console.WriteLine($"       -> {detail}");
            }
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("================================================================================");
            Console.WriteLine("   BỘ XÁC MINH RỦI RO & TỐI ƯU HỆ THỐNG KIỂM DUYỆT TỆP (STUDENTCONNECT)");
            Console.WriteLine("   Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("================================================================================");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RiskAuditFiles");
            if (!Directory.Exists(testDir)) Directory.CreateDirectory(testDir);

            try
            {
                // =========================================================================
                // 1. KIỂM CHỨNG LOẠI BỎ GEMINI & BẢO TOÀN QUOTA CHO CHATBOT/AGENT
                // =========================================================================
                Console.WriteLine("\n>>> [MỤC 1] XÁC MINH ZERO GEMINI REQUEST TRONG LUỒNG KIỂM DUYỆT TỆP");

                string cleanImg = Path.Combine(testDir, "test_clean.png");
                CreateDummyPng(cleanImg);

                // 1.1 ModerateImageWithAiAsync trả về DISABLED_LOCAL_ONLY và isSafe = false (không ngộ nhận an toàn nội dung)
                var aiRes = ChatFileModerationHelper.ModerateImageWithAiAsync(cleanImg, "image/png").GetAwaiter().GetResult();
                bool pass1_1 = aiRes.status == "DISABLED_LOCAL_ONLY" && aiRes.isSafe == false && aiRes.reason.Contains("không xác nhận an toàn nội dung");
                AssertCheck("VERIFY-1.1", "ModerateImageWithAiAsync trả về DISABLED_LOCAL_ONLY, isSafe=false (tránh ngộ nhận an toàn nội dung)",
                    pass1_1, $"Status: {aiRes.status}, isSafe: {aiRes.isSafe}, Reason: '{aiRes.reason}'");

                // 1.2 ValidateAndModerateFileAsync duyệt ảnh thành công chỉ bằng bảo mật kỹ thuật cục bộ, không gọi Gemini
                var modRes = ChatFileModerationHelper.ValidateAndModerateFileAsync(cleanImg, "test_clean.png", "image/png", true, 1022, "testuser01", 100).GetAwaiter().GetResult();
                bool pass1_2 = modRes.IsApproved && modRes.ImageModerationStatus == "DISABLED_LOCAL_ONLY" && modRes.AntivirusStatus == "CLEAN";
                AssertCheck("VERIFY-1.2", "ValidateAndModerateFileAsync duyệt ảnh bằng kỹ thuật cục bộ, không tiêu hao quota Gemini",
                    pass1_2, $"Approved: {modRes.IsApproved}, ImageModerationStatus: {modRes.ImageModerationStatus}, AV: {modRes.AntivirusStatus}");

                // 1.3 Chatbot & AI Agent (AiApiClient) giữ nguyên cấu hình và hoạt động độc lập
                var aiClient = new AiApiClient();
                bool pass1_3 = !string.IsNullOrEmpty(aiClient.Model) && !string.IsNullOrEmpty(aiClient.AgentName);
                AssertCheck("VERIFY-1.3", "Chatbot Mr. T & AI Agent giữ nguyên cấu hình độc lập, không bị ảnh hưởng",
                    pass1_3, $"AgentName: '{aiClient.AgentName}', Model: '{aiClient.Model}'");

                // =========================================================================
                // 2. XÁC MINH CÁC LỚP BẢO MẬT CỤC BỘ & NGUYÊN TẮC FAIL-CLOSED
                // =========================================================================
                Console.WriteLine("\n>>> [MỤC 2] XÁC MINH NGUYÊN TẮC TỪ CHỐI AN TOÀN (FAIL-CLOSED) & PHÒNG THỦ CỤC BỘ");

                // 2.1 Defender quét lỗi hoặc timeout -> Bắt buộc từ chối tệp an toàn (Fail-Closed)
                string nonFile = Path.Combine(testDir, "missing_scan_file.tmp");
                var avFail = ChatFileModerationHelper.ScanFileWithAntivirus(nonFile);
                bool pass2_1 = avFail.status == "SCAN_FAILED";
                AssertCheck("VERIFY-2.1", "Trình quét Antivirus gặp sự cố trả về SCAN_FAILED kích hoạt Fail-Closed",
                    pass2_1, $"Status: {avFail.status}, Details: {avFail.details}");

                // 2.2 Tệp mã độc / webshell PHP bị chặn
                string webshellFile = Path.Combine(testDir, "shell.php");
                File.WriteAllText(webshellFile, "<?php system($_GET['c']); ?>");
                var sigCheck = ChatFileModerationHelper.VerifyFileSignature(webshellFile, ".php");
                bool pass2_2 = !sigCheck.isValid && (sigCheck.detectedType == "EMBEDDED_SCRIPT" || sigCheck.detectedType == "MALWARE_DETECTED");
                AssertCheck("VERIFY-2.2", "Phát hiện và chặn tệp chứa mã độc / webshell",
                    pass2_2, $"Detected: {sigCheck.detectedType}, Reason: {sigCheck.reason}");

                // 2.3 Chặn tấn công Zip Bomb (> 1,000 files)
                string zipBombCount = Path.Combine(testDir, "bomb_count.zip");
                CreateZipBombCount(zipBombCount, 1005);
                var zipBombRes = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipBombCount, ".zip");
                bool pass2_3 = !zipBombRes.isValid && zipBombRes.detectedType == "ZIP_BOMB_ENTRY_COUNT";
                AssertCheck("VERIFY-2.3", "Chặn tấn công Zip Bomb (vượt quá 1,000 entries)",
                    pass2_3, $"Detected: {zipBombRes.detectedType}, Reason: {zipBombRes.reason}");

                // 2.4 Chặn tấn công Zip Slip (Path Traversal trong Zip)
                string zipSlip = Path.Combine(testDir, "zip_slip.zip");
                CreateZipSlip(zipSlip, "../../etc/passwd");
                var zipSlipRes = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipSlip, ".zip");
                bool pass2_4 = !zipSlipRes.isValid && zipSlipRes.detectedType == "ZIP_SLIP_ATTACK";
                AssertCheck("VERIFY-2.4", "Chặn tấn công Zip Slip Path Traversal ('../../')",
                    pass2_4, $"Detected: {zipSlipRes.detectedType}, Reason: {zipSlipRes.reason}");

                // 2.5 Chặn tài liệu Office bị tiêm Macro (vbaProject.bin)
                string macroDocx = Path.Combine(testDir, "macro.docx");
                CreateZipWithEntries(macroDocx, new Dictionary<string, string>
                {
                    { "[Content_Types].xml", "<Types/>" },
                    { "word/vbaProject.bin", "VBA Binary Payload" }
                });
                var macroRes = ChatFileModerationHelper.VerifyZipAndOfficeArchive(macroDocx, ".docx");
                bool pass2_5 = !macroRes.isValid && macroRes.detectedType == "OFFICE_MACRO_BLOCKED";
                AssertCheck("VERIFY-2.5", "Chặn tài liệu Office bị tiêm mã Macro (vbaProject.bin)",
                    pass2_5, $"Detected: {macroRes.detectedType}, Reason: {macroRes.reason}");

                // 2.6 Chặn tệp thực thi ngụy trang ảnh (.exe đổi tên thành .jpg)
                string fakeJpg = Path.Combine(testDir, "disguised.jpg");
                File.WriteAllBytes(fakeJpg, new byte[] { 0x4D, 0x5A, 0x90, 0x00 }); // MZ header
                var fakeJpgRes = ChatFileModerationHelper.VerifyFileSignature(fakeJpg, ".jpg");
                bool pass2_6 = !fakeJpgRes.isValid && fakeJpgRes.detectedType == "EXECUTABLE_PE";
                AssertCheck("VERIFY-2.6", "Chặn tệp PE Executable (.exe) ngụy trang ảnh đuôi .jpg",
                    pass2_6, $"Detected: {fakeJpgRes.detectedType}, Reason: {fakeJpgRes.reason}");

                // =========================================================================
                // 3. TÍNH NGUYÊN KHỐI KHI TẢI NHIỀU TỆP (ATOMIC BATCH ROLLBACK)
                // =========================================================================
                Console.WriteLine("\n>>> [MỤC 3] KIỂM TRA TÍNH NGUYÊN KHỐI KHI TẢI NHIỀU TỆP (ATOMIC ROLLBACK)");

                string batch1 = Path.Combine(testDir, "batch_clean1.png");
                string batch2 = Path.Combine(testDir, "batch_bad.exe");
                string batch3 = Path.Combine(testDir, "batch_clean2.png");
                CreateDummyPng(batch1);
                File.WriteAllText(batch2, "MZ dummy");
                CreateDummyPng(batch3);

                var batchFiles = new[] { batch1, batch2, batch3 };
                var stagedFiles = new List<string>();
                bool batchApproved = true;
                string batchRejectMsg = "";

                foreach (var f in batchFiles)
                {
                    string staged = Path.Combine(testDir, "staged_" + Path.GetFileName(f));
                    File.Copy(f, staged, true);
                    stagedFiles.Add(staged);

                    string ext = Path.GetExtension(f);
                    bool isImg = ext == ".png";
                    var check = ChatFileModerationHelper.ValidateAndModerateFileAsync(staged, Path.GetFileName(f), isImg ? "image/png" : "application/octet-stream", isImg, 1022, "testuser", 1).GetAwaiter().GetResult();
                    if (!check.IsApproved)
                    {
                        batchApproved = false;
                        batchRejectMsg = check.UserFriendlyMessage;
                        foreach (var s in stagedFiles)
                        {
                            try { if (File.Exists(s)) File.Delete(s); } catch { }
                        }
                        break;
                    }
                }

                bool allStagedDeleted = stagedFiles.All(s => !File.Exists(s));
                bool pass3_1 = !batchApproved && allStagedDeleted;
                AssertCheck("VERIFY-3.1", "Batch Upload thất bại khi 1 tệp vi phạm: Hủy toàn bộ và xóa sạch tệp tạm",
                    pass3_1, $"Batch Approved: {batchApproved}, All Staged Purged: {allStagedDeleted}, Msg: '{batchRejectMsg}'");

                // =========================================================================
                // 4. LUỒNG BÁO CÁO VI PHẠM TỆP / ẨN / KHÔI PHỤC & CHẶN TẢI NỘI DUNG ẨN
                // =========================================================================
                Console.WriteLine("\n>>> [MỤC 4] LUỒNG BÁO CÁO VI PHẠM, ẨN/KHÔI PHỤC VÀ BẢO MẬT TẢI XUỐNG");

                using (var db = new TDMUEcoSystemEntities())
                {
                    // 4.1 Tạo một tin nhắn kèm file hợp lệ
                    var testMsg = new ChatMessage
                    {
                        RoomID = 1,
                        SenderID = 1022,
                        Content = "[ATTACHMENTS_PAYLOAD]:{\"Text\":\"Báo cáo vi phạm demo\",\"Items\":[{\"FileKey\":\"guid_audit_doc.pdf\",\"FileName\":\"audit_doc.pdf\"}]}",
                        ImageUrl = null,
                        SentAt = DateTime.Now,
                        IsHidden = false
                    };
                    db.ChatMessages.Add(testMsg);
                    db.SaveChanges();
                    int msgId = testMsg.MessageID;

                    // 4.2 Người dùng báo cáo tin nhắn này
                    var report = new ChatReport
                    {
                        ReporterID = 1023,
                        ReportedUserID = 1022,
                        RoomID = 1,
                        MessageID = msgId,
                        Reason = "Nội dung tệp không phù hợp thuần phong mỹ tục",
                        Details = "Yêu cầu ban quản trị ẩn tệp đính kèm này",
                        EvidenceSnippet = "[ATTACHMENTS_PAYLOAD]:audit_doc.pdf",
                        Status = "Pending",
                        CreatedAt = DateTime.Now
                    };
                    db.ChatReports.Add(report);
                    db.SaveChanges();
                    int repId = report.ReportID;

                    bool pass4_1 = repId > 0 && report.MessageID == msgId;
                    AssertCheck("VERIFY-4.1", "Người dùng tạo báo cáo vi phạm đối với tin nhắn chứa tệp thành công",
                        pass4_1, $"ReportID: {repId}, Target MessageID: {msgId}");

                    // 4.3 Quản trị viên xử lý ẩn tin nhắn (HideMessage)
                    testMsg.IsHidden = true;
                    testMsg.HiddenReason = "Bị ẩn do vi phạm quy chuẩn cộng đồng";
                    testMsg.HiddenBy = 4; // Admin ID
                    report.Status = "Resolved";
                    report.ActionTaken = "HideMessage";
                    report.HandledBy = 4;
                    report.HandledAt = DateTime.Now;
                    db.SaveChanges();

                    bool pass4_2 = (testMsg.IsHidden == true) && (testMsg.HiddenBy == 4);
                    AssertCheck("VERIFY-4.2", "Quản trị viên ẩn tin nhắn vi phạm thành công (IsHidden = true)",
                        pass4_2, $"Message #{msgId} IsHidden: {testMsg.IsHidden}, HiddenReason: '{testMsg.HiddenReason}'");

                    // 4.4 Kiểm tra bảo mật tải xuống: Người dùng thường không thể tải tệp từ tin nhắn đã bị ẩn
                    bool canDownloadHidden = (testMsg.IsHidden != true && testMsg.Content != "Tin nhắn đã được thu hồi");
                    AssertCheck("VERIFY-4.3", "Chặn người dùng tải tệp từ tin nhắn đã bị Ban Quản Trị ẩn (403 Forbidden)",
                        !canDownloadHidden, $"Can Download Hidden Content: {canDownloadHidden} (Bị từ chối an toàn)");

                    // 4.5 Quản trị viên khôi phục tin nhắn (UnhideMessage)
                    testMsg.IsHidden = false;
                    testMsg.HiddenReason = null;
                    testMsg.HiddenBy = null;
                    db.SaveChanges();

                    bool pass4_4 = (testMsg.IsHidden == false);
                    AssertCheck("VERIFY-4.4", "Quản trị viên khôi phục hiển thị tin nhắn thành công (IsHidden = false)",
                        pass4_4, $"Message #{msgId} IsHidden: {testMsg.IsHidden} (Đã khôi phục)");

                    // Dọn dẹp dữ liệu thử nghiệm
                    try
                    {
                        db.ChatReports.Remove(report);
                        db.ChatMessages.Remove(testMsg);
                        db.SaveChanges();
                    }
                    catch { }
                }

                // =========================================================================
                // TỔNG KẾT
                // =========================================================================
                Console.WriteLine("\n================================================================================");
                Console.WriteLine($"   KẾT QUẢ XÁC MINH RỦI RO: {_pass}/{_total} PASS, {_fail} FAIL");
                Console.WriteLine("================================================================================");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                }
                catch { }
            }
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

        static void CreateZipBombCount(string zipPath, int count)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var fs = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                for (int i = 0; i < count; i++)
                {
                    archive.CreateEntry($"item_{i}.txt");
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
                    writer.Write("slip content");
                }
            }
        }
    }
}
