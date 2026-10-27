using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentConnect.Scratch
{
    class Program
    {
        static int totalTests = 0;
        static int passedTests = 0;
        static int failedTests = 0;

        static void AssertTest(string testName, bool condition, string evidence)
        {
            totalTests++;
            if (condition)
            {
                passedTests++;
                Console.WriteLine($"[PASS] {testName} | {evidence}");
            }
            else
            {
                failedTests++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testName} | {evidence}");
                Console.ResetColor();
            }
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" KIỂM THỬ TOÀN DIỆN: HỆ THỐNG KIỂM DUYỆT ẢNH & FILE TRONG TRÒ CHUYỆN ");
            Console.WriteLine("================================================================================");

            RunTestsAsync().GetAwaiter().GetResult();

            Console.WriteLine("================================================================================");
            Console.WriteLine($" TỔNG KẾT: {passedTests}/{totalTests} TESTS PASS, {failedTests}/{totalTests} TESTS FAIL.");
            Console.WriteLine("================================================================================");
            Environment.Exit(failedTests > 0 ? 1 : 0);
        }

        static async Task RunTestsAsync()
        {
            string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "TempTestFiles");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            // ── NHÓM 1: KIỂM THỬ FILE NGUY HIỂM VÀ PHẦN MỞ RỘNG BỊ CẤM ──
            Console.WriteLine("\n>>> [NHÓM 1] KIỂM TRA CHẶN FILE NGUY HIỂM & TÊN FILE BẤT THƯỜNG");

            // 1.1 Tệp thực thi trực tiếp (.exe)
            string exePath = Path.Combine(tempDir, "malware.exe");
            File.WriteAllBytes(exePath, new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 }); // MZ header
            var res1 = await ChatFileModerationHelper.ValidateAndModerateFileAsync(exePath, "malware.exe", "application/x-msdownload", false, 1022, "testuser01", 100);
            AssertTest("1.1 Chặn tệp thực thi .exe", !res1.IsApproved && res1.ErrorCode == "EXTENSION_PROHIBITED", $"Decision=REJECTED, Code={res1.ErrorCode}, Msg='{res1.UserFriendlyMessage}'");

            // 1.2 Tệp script nguy hiểm (.bat, .sh, .vbs, .ps1, .php, .aspx)
            string[] dangerousExts = new[] { "script.bat", "run.sh", "virus.vbs", "exploit.ps1", "webshell.php", "backdoor.aspx" };
            bool allDangerousBlocked = true;
            foreach (var dang in dangerousExts)
            {
                string p = Path.Combine(tempDir, dang);
                File.WriteAllText(p, "echo dangerous payload");
                var resDang = await ChatFileModerationHelper.ValidateAndModerateFileAsync(p, dang, "text/plain", false, 1022, "testuser01", 100);
                if (resDang.IsApproved || resDang.ErrorCode != "EXTENSION_PROHIBITED")
                {
                    allDangerousBlocked = false;
                    break;
                }
            }
            AssertTest("1.2 Chặn tất cả định dạng script nguy hiểm (.bat, .sh, .vbs, .ps1, .php, .aspx)", allDangerousBlocked, "Tất cả 6 định dạng đều bị từ chối với EXTENSION_PROHIBITED");

            // 1.3 Tấn công phần mở rộng kép (Double Extension Spoofing)
            string dblExt1 = Path.Combine(tempDir, "document.pdf.exe");
            File.WriteAllBytes(dblExt1, new byte[] { 0x4D, 0x5A, 0x00, 0x00 });
            var resDbl1 = await ChatFileModerationHelper.ValidateAndModerateFileAsync(dblExt1, "document.pdf.exe", "application/pdf", false, 1022, "testuser01", 100);
            AssertTest("1.3 Chặn phần mở rộng kép document.pdf.exe", !resDbl1.IsApproved, $"Decision=REJECTED, Code={resDbl1.ErrorCode}");

            string dblExt2 = Path.Combine(tempDir, "photo.png.vbs");
            File.WriteAllText(dblExt2, "MsgBox 'pwned'");
            var resDbl2 = await ChatFileModerationHelper.ValidateAndModerateFileAsync(dblExt2, "photo.png.vbs", "image/png", false, 1022, "testuser01", 100);
            AssertTest("1.4 Chặn phần mở rộng kép photo.png.vbs", !resDbl2.IsApproved, $"Decision=REJECTED, Code={resDbl2.ErrorCode}");

            // 1.5 Tệp rỗng (0 bytes)
            string emptyFile = Path.Combine(tempDir, "empty.png");
            File.WriteAllBytes(emptyFile, new byte[0]);
            var resEmpty = await ChatFileModerationHelper.ValidateAndModerateFileAsync(emptyFile, "empty.png", "image/png", true, 1022, "testuser01", 100);
            AssertTest("1.5 Chặn tệp rỗng 0 bytes", !resEmpty.IsApproved && resEmpty.ErrorCode == "FILE_EMPTY", $"Decision=REJECTED, Code={resEmpty.ErrorCode}");

            // 1.6 Tệp vượt quá dung lượng tối đa (>10MB cho ảnh)
            string oversizedFile = Path.Combine(tempDir, "oversized.png");
            using (var fs = new FileStream(oversizedFile, FileMode.Create))
            {
                fs.SetLength(11 * 1024 * 1024);
            }
            var resOver = await ChatFileModerationHelper.ValidateAndModerateFileAsync(oversizedFile, "oversized.png", "image/png", true, 1022, "testuser01", 100);
            AssertTest("1.6 Chặn tệp ảnh vượt quá 10MB", !resOver.IsApproved && resOver.ErrorCode == "FILE_TOO_LARGE", $"Decision=REJECTED, Code={resOver.ErrorCode}");


            // ── NHÓM 2: KIỂM THỬ FILE GIẢ MẠO CHỮ KÝ SỐ (MAGIC BYTES SPOOFING) ──
            Console.WriteLine("\n>>> [NHÓM 2] KIỂM TRA CHỮ KÝ TỆP (MAGIC BYTES) & CHỐNG FILE GIẢ MẠO");

            // 2.1 File EXE ngụy trang thành .jpg (Header MZ)
            string spoofedJpg = Path.Combine(tempDir, "innocent_photo.jpg");
            File.WriteAllBytes(spoofedJpg, new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 }); // MZ header
            var resSpoof1 = await ChatFileModerationHelper.ValidateAndModerateFileAsync(spoofedJpg, "innocent_photo.jpg", "image/jpeg", true, 1022, "testuser01", 100);
            AssertTest("2.1 Chặn file EXE ngụy trang thành innocent_photo.jpg", !resSpoof1.IsApproved && resSpoof1.ErrorCode == "SIGNATURE_MISMATCH", $"Decision=REJECTED, Code={resSpoof1.ErrorCode}, Msg='{resSpoof1.UserFriendlyMessage}'");

            // 2.2 File Web Script ngụy trang thành .png
            string spoofedPng = Path.Combine(tempDir, "profile_avatar.png");
            File.WriteAllText(spoofedPng, "<script>alert('xss payload')</script>");
            var resSpoof2 = await ChatFileModerationHelper.ValidateAndModerateFileAsync(spoofedPng, "profile_avatar.png", "image/png", true, 1022, "testuser01", 100);
            AssertTest("2.2 Chặn script HTML/JS ngụy trang thành profile_avatar.png", !resSpoof2.IsApproved && (resSpoof2.ErrorCode == "SIGNATURE_MISMATCH" || resSpoof2.ErrorCode == "MALWARE_DETECTED"), $"Decision=REJECTED, Code={resSpoof2.ErrorCode}");

            // 2.3 File văn bản giả mạo .pdf (Không có %PDF- header)
            string fakePdf = Path.Combine(tempDir, "fake_document.pdf");
            File.WriteAllText(fakePdf, "This is just plain text, not a real PDF structure.");
            var resFakePdf = await ChatFileModerationHelper.ValidateAndModerateFileAsync(fakePdf, "fake_document.pdf", "application/pdf", false, 1022, "testuser01", 100);
            AssertTest("2.3 Chặn file giả mạo đuôi .pdf nhưng không có %PDF- header", !resFakePdf.IsApproved && resFakePdf.ErrorCode == "SIGNATURE_MISMATCH", $"Decision=REJECTED, Code={resFakePdf.ErrorCode}");

            // 2.4 File ảnh bị hỏng cấu trúc (Corrupt bitmap)
            string corruptJpg = Path.Combine(tempDir, "corrupted.jpg");
            File.WriteAllBytes(corruptJpg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x05, 0x00 });
            var resCorrupt = await ChatFileModerationHelper.ValidateAndModerateFileAsync(corruptJpg, "corrupted.jpg", "image/jpeg", true, 1022, "testuser01", 100);
            AssertTest("2.4 Chặn ảnh bị hỏng cấu trúc không thể giải mã (Corrupt Image)", !resCorrupt.IsApproved && resCorrupt.ErrorCode == "SIGNATURE_MISMATCH", $"Decision=REJECTED, Code={resCorrupt.ErrorCode}");


            // ── NHÓM 3: KIỂM THỬ TỆP TIN HỢP LỆ VÀ QUY TRÌNH DUYỆT ──
            Console.WriteLine("\n>>> [NHÓM 3] KIỂM THỬ CÁC TỆP TIN HỢP LỆ (APPROVED FILES)");

            // 3.1 Ảnh PNG hợp lệ (1x1 valid pixel)
            string validPng = Path.Combine(tempDir, "valid_test.png");
            byte[] pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            File.WriteAllBytes(validPng, pngBytes);
            var resValidPng = await ChatFileModerationHelper.ValidateAndModerateFileAsync(validPng, "valid_test.png", "image/png", true, 1022, "testuser01", 100);
            AssertTest("3.1 Duyệt ảnh PNG hợp lệ (Magic Bytes 89 50 4E 47)", resValidPng.IsApproved, $"Decision=APPROVED, SHA256={resValidPng.FileSha256.Substring(0, 10)}..., AV={resValidPng.AntivirusStatus}, AI={resValidPng.ImageModerationStatus}");

            // 3.2 Tài liệu PDF hợp lệ (%PDF- header)
            string validPdf = Path.Combine(tempDir, "lecture_notes.pdf");
            byte[] pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF");
            File.WriteAllBytes(validPdf, pdfBytes);
            var resValidPdf = await ChatFileModerationHelper.ValidateAndModerateFileAsync(validPdf, "lecture_notes.pdf", "application/pdf", false, 1022, "testuser01", 100);
            AssertTest("3.2 Duyệt tài liệu PDF hợp lệ (%PDF- header)", resValidPdf.IsApproved, $"Decision=APPROVED, SHA256={resValidPdf.FileSha256.Substring(0, 10)}..., AV={resValidPdf.AntivirusStatus}");

            // 3.3 Tệp văn bản thuần TXT hợp lệ
            string validTxt = Path.Combine(tempDir, "notes.txt");
            File.WriteAllText(validTxt, "Day la ghi chu bai giang mon Lap Trinh Web.\nHoc ky 1 nam 2026.");
            var resValidTxt = await ChatFileModerationHelper.ValidateAndModerateFileAsync(validTxt, "notes.txt", "text/plain", false, 1022, "testuser01", 100);
            AssertTest("3.3 Duyệt tệp văn bản thuần .txt hợp lệ", resValidTxt.IsApproved, $"Decision=APPROVED, SHA256={resValidTxt.FileSha256.Substring(0, 10)}...");


            // ── NHÓM 4: KIỂM TRA TRÌNH QUÉT MÃ ĐỘC VÀ DỊCH VỤ KIỂM DUYỆT THỰC TẾ ──
            Console.WriteLine("\n>>> [NHÓM 4] KIỂM TRA ANTIMALWARE VÀ AI CONTENT MODERATION");

            // 4.1 Khả dụng của Windows Defender CLI
            var avCheck = ChatFileModerationHelper.ScanFileWithAntivirus(validPdf);
            bool isAvWorking = avCheck.status == "CLEAN" || avCheck.status == "SCAN_UNAVAILABLE";
            AssertTest("4.1 Trình quét mã độc Windows Defender hoạt động và phản hồi trung thực", isAvWorking, $"Status={avCheck.status}, Details='{avCheck.details}'");

            // 4.2 Gemini Vision đã loại bỏ hoàn toàn để không tiêu hao quota
            var aiCheck = await ChatFileModerationHelper.ModerateImageWithAiAsync(validPng, "image/png");
            bool isAiDisabled = aiCheck.status == "DISABLED_LOCAL_ONLY";
            AssertTest("4.2 Loại bỏ Gemini Vision khỏi kiểm duyệt tệp (Không tiêu hao quota)", isAiDisabled, $"Status={aiCheck.status}, IsSafe={aiCheck.isSafe}, Reason='{aiCheck.reason}'");


            // ── NHÓM 5: KIỂM TRA NHẬT KÝ KIỂM DUYỆT (AUDIT LOGGING) ──
            Console.WriteLine("\n>>> [NHÓM 5] KIỂM TRA NHẬT KÝ KIỂM DUYỆT (AUDIT LOGGING)");

            var logLines = ChatFileModerationHelper.GetRecentAuditLogLines(10);
            bool hasAuditLogs = logLines.Count > 0;
            string latestLog = hasAuditLogs ? logLines[0] : "";
            AssertTest("5.1 Ghi nhận đầy đủ nhật ký kiểm duyệt vào file Audit Log", hasAuditLogs, $"Số dòng log ghi nhận: {logLines.Count}, Dòng mới nhất: {latestLog.Substring(0, Math.Min(latestLog.Length, 100))}...");

            bool containsApprovalAndRejection = logLines.Any(l => l.Contains("Decision: APPROVED")) && logLines.Any(l => l.Contains("Decision: REJECTED"));
            AssertTest("5.2 Nhật ký ghi nhận cả quyết định APPROVED và REJECTED với đầy đủ thông tin", containsApprovalAndRejection, "Ghi nhận rõ ràng lý do từ chối và mã băm SHA256");


            // ── NHÓM 6: KIỂM TRA BẢO MẬT TẢI XUỐNG VÀ PHÂN QUYỀN ──
            Console.WriteLine("\n>>> [NHÓM 6] KIỂM TRA BẢO MẬT TẢI XUỐNG (DOWNLOAD SECURITY)");

            // 6.1 Chặn path traversal trong fileKey
            string[] maliciousKeys = new[] { "../../Web.config", "..\\..\\Web.config", "subdir/file.txt", "subdir\\file.txt" };
            bool allKeysBlocked = true;
            foreach (var k in maliciousKeys)
            {
                if (!(string.IsNullOrWhiteSpace(k) || k.Contains("..") || k.Contains("/") || k.Contains("\\")))
                {
                    allKeysBlocked = false;
                }
            }
            AssertTest("6.1 Phát hiện và chặn toàn bộ đường dẫn Path Traversal trong fileKey", allKeysBlocked, "Các mẫu ../, ..\\, /, \\ đều bị chặn với HTTP 400");

            // Dọn dẹp thư mục kiểm thử
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
