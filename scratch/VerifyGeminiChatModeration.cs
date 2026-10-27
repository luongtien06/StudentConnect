using Newtonsoft.Json.Linq;
using StudentConnect.Controllers;
using StudentConnect.Helpers;
using StudentConnect.Models.Assistant;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentConnect.Verification
{
    public class TestSuiteRunner
    {
        private static int _passCount = 0;
        private static int _failCount = 0;

        public static void AssertCheck(string testId, string testName, bool condition, string detail)
        {
            if (condition)
            {
                _passCount++;
                Console.WriteLine($"[PASS] {testId}: {testName} - {detail}");
            }
            else
            {
                _failCount++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testId}: {testName} - {detail}");
                Console.ResetColor();
            }
        }

        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("KIỂM THỬ KHÔI PHỤC VÀ TỐI ƯU HÓA GEMINI API KIỂM DUYỆT CHAT TDMUECOSYSTEM");
            Console.WriteLine("Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("===============================================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_ChatModeration_Workspace");
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
            Directory.CreateDirectory(testDir);

            try
            {
                // =================================================================
                // PHẦN 1: KIỂM SOÁT TẦNG BẢO VỆ CỤC BỘ (LOCAL_REJECTED)
                // =================================================================
                Console.WriteLine(">>> [PHẦN 1] KIỂM TRA BẢO VỆ CỤC BỘ (Chữ ký, Chống mã độc, Bomb, Macro)");

                // 1.1 Tệp giả mạo JPEG bằng header MZ thực thi (PE executable)
                string fakeJpgPath = Path.Combine(testDir, "malware.jpg");
                File.WriteAllBytes(fakeJpgPath, new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 });
                var res1_1 = await ChatFileModerationHelper.ValidateAndModerateFileAsync(fakeJpgPath, "malware.jpg", "image/jpeg", true, 999, "Tester", 1);
                AssertCheck("TEST-1.1", "Chặn PE header ngụy trang đuôi .jpg",
                    !res1_1.IsApproved && res1_1.ImageModerationStatus == ModerationStatuses.LocalRejected,
                    $"IsApproved={res1_1.IsApproved}, Status={res1_1.ImageModerationStatus}, ErrorCode={res1_1.ErrorCode}");

                // 1.2 Tệp phần mở rộng cấm (.exe)
                string exePath = Path.Combine(testDir, "script.exe");
                File.WriteAllBytes(exePath, new byte[] { 0x4D, 0x5A, 0x00, 0x00 });
                var res1_2 = await ChatFileModerationHelper.ValidateAndModerateFileAsync(exePath, "script.exe", "application/x-msdownload", false, 999, "Tester", 1);
                AssertCheck("TEST-1.2", "Chặn phần mở rộng nguy hiểm trong Blacklist",
                    !res1_2.IsApproved && res1_2.ImageModerationStatus == ModerationStatuses.LocalRejected,
                    $"IsApproved={res1_2.IsApproved}, Status={res1_2.ImageModerationStatus}, ErrorCode={res1_2.ErrorCode}");

                // 1.3 Phần mở rộng kép (Double Extension: document.pdf.exe)
                string dblPath = Path.Combine(testDir, "document.pdf.exe");
                File.WriteAllBytes(dblPath, new byte[] { 0x25, 0x50, 0x44, 0x46 });
                var res1_3 = await ChatFileModerationHelper.ValidateAndModerateFileAsync(dblPath, "document.pdf.exe", "application/pdf", false, 999, "Tester", 1);
                AssertCheck("TEST-1.3", "Chặn tấn công phần mở rộng kép (.pdf.exe)",
                    !res1_3.IsApproved && res1_3.ImageModerationStatus == ModerationStatuses.LocalRejected,
                    $"IsApproved={res1_3.IsApproved}, Status={res1_3.ImageModerationStatus}, ErrorCode={res1_3.ErrorCode}");

                // 1.4 Zip Bomb (Tỉ lệ nén bất thường)
                string zipBombPath = Path.Combine(testDir, "bomb.zip");
                using (var fs = new FileStream(zipBombPath, FileMode.Create))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    var entry = zip.CreateEntry("huge.txt", CompressionLevel.Optimal);
                    using (var es = entry.Open())
                    {
                        byte[] zeroChunk = new byte[1024 * 1024];
                        for (int i = 0; i < 5; i++) es.Write(zeroChunk, 0, zeroChunk.Length);
                    }
                }
                var sigZipBomb = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipBombPath, ".zip");
                AssertCheck("TEST-1.4", "Phát hiện Zip Bomb tỉ lệ nén cao",
                    !sigZipBomb.isValid && sigZipBomb.detectedType == "ZIP_BOMB_RATIO",
                    $"isValid={sigZipBomb.isValid}, Type={sigZipBomb.detectedType}");

                // 1.5 Zip Slip (Path traversal)
                string zipSlipPath = Path.Combine(testDir, "slip.zip");
                using (var fs = new FileStream(zipSlipPath, FileMode.Create))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    zip.CreateEntry("../../../evil.txt");
                }
                var sigZipSlip = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipSlipPath, ".zip");
                AssertCheck("TEST-1.5", "Phát hiện Zip Slip (Path Traversal trong ZIP)",
                    !sigZipSlip.isValid && sigZipSlip.detectedType == "ZIP_SLIP_ATTACK",
                    $"isValid={sigZipSlip.isValid}, Type={sigZipSlip.detectedType}");

                // 1.6 Macro trong tài liệu Word (vbaProject.bin)
                string macroDocx = Path.Combine(testDir, "macro.docx");
                using (var fs = new FileStream(macroDocx, FileMode.Create))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    zip.CreateEntry("[Content_Types].xml");
                    zip.CreateEntry("_rels/.rels");
                    zip.CreateEntry("word/vbaProject.bin");
                }
                var sigMacro = ChatFileModerationHelper.VerifyZipAndOfficeArchive(macroDocx, ".docx");
                AssertCheck("TEST-1.6", "Chặn tài liệu Office chứa Macro nhúng",
                    !sigMacro.isValid && sigMacro.detectedType == "OFFICE_MACRO_BLOCKED",
                    $"isValid={sigMacro.isValid}, Type={sigMacro.detectedType}");

                // 1.7 Ảnh bị hỏng cấu trúc (Corrupted Image)
                string corruptJpg = Path.Combine(testDir, "corrupted.jpg");
                File.WriteAllBytes(corruptJpg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x00, 0x00 });
                var resCorrupt = await ChatFileModerationHelper.ValidateAndModerateFileAsync(corruptJpg, "corrupted.jpg", "image/jpeg", true, 999, "Tester", 1);
                AssertCheck("TEST-1.7", "Chặn ảnh hỏng không thể giải mã GDI+",
                    !resCorrupt.IsApproved && resCorrupt.ImageModerationStatus == ModerationStatuses.LocalRejected,
                    $"IsApproved={resCorrupt.IsApproved}, Status={resCorrupt.ImageModerationStatus}, ErrorCode={resCorrupt.ErrorCode}");

                // =================================================================
                // PHẦN 2: KIỂM DUYỆT HÌNH ẢNH HỢP LỆ VỚI GEMINI FLASH-LITE (AI_APPROVED)
                // =================================================================
                Console.WriteLine("\n>>> [PHẦN 2] KIỂM DUYỆT ẢNH HỢP LỆ & CACHE SHA-256");

                // Tạo 1 ảnh PNG hợp lệ (100x100 màu xanh lá)
                string validPngPath = Path.Combine(testDir, "valid_green.png");
                using (var bmp = new Bitmap(100, 100))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.ForestGreen);
                    }
                    bmp.Save(validPngPath, ImageFormat.Png);
                }

                // Xóa cache và reset circuit breaker để test clean
                ChatFileModerationHelper.ClearModerationCache();
                ChatFileModerationHelper.ResetCircuitBreaker();

                var aiMod1 = await ChatFileModerationHelper.ModerateImageWithAiAsync(validPngPath, "image/png");
                AssertCheck("TEST-2.1", "Gemini API kiểm duyệt ảnh hợp lệ thành công",
                    aiMod1.status == ModerationStatuses.AiApproved && aiMod1.isSafe,
                    $"Status={aiMod1.status}, isSafe={aiMod1.isSafe}, Reason={aiMod1.reason}");

                // Test 2.2: Kiểm tra đệm SHA-256 (Cache)
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var aiModCached = await ChatFileModerationHelper.ModerateImageWithAiAsync(validPngPath, "image/png");
                sw.Stop();
                AssertCheck("TEST-2.2", "Cache SHA-256 phản hồi tức thì mà không gọi lại mạng",
                    aiModCached.status == ModerationStatuses.AiApproved && aiModCached.reason.Contains("bộ nhớ đệm") && sw.ElapsedMilliseconds < 50,
                    $"Elapsed={sw.ElapsedMilliseconds}ms, Reason={aiModCached.reason}");

                // Test 2.3: ValidateAndModerateFileAsync cho ảnh hợp lệ
                string validImgStaged = Path.Combine(testDir, "staged_valid.png");
                File.Copy(validPngPath, validImgStaged);
                var fullModApproved = await ChatFileModerationHelper.ValidateAndModerateFileAsync(validImgStaged, "valid.png", "image/png", true, 999, "Tester", 1);
                AssertCheck("TEST-2.3", "Toàn bộ chu trình duyệt ảnh hợp lệ đạt IsApproved=true & AI_APPROVED",
                    fullModApproved.IsApproved && fullModApproved.ImageModerationStatus == ModerationStatuses.AiApproved,
                    $"IsApproved={fullModApproved.IsApproved}, Status={fullModApproved.ImageModerationStatus}");

                // =================================================================
                // PHẦN 3: KIỂM DUYỆT NỘI DUNG VI PHẠM (AI_REJECTED)
                // =================================================================
                Console.WriteLine("\n>>> [PHẦN 3] KIỂM DUYỆT NỘI DUNG VI PHẠM & DỌN TỆP TẠM");

                // Tạo tệp văn bản chứa nội dung đe dọa bạo lực cực đoan
                string violTextPath = Path.Combine(testDir, "threat_text.txt");
                File.WriteAllText(violTextPath, "Tao sẽ mang súng và dao đến trường giết chết cả lớp mày hôm nay!", Encoding.UTF8);

                var textModViol = await ChatFileModerationHelper.ModerateDocumentTextWithAiAsync(violTextPath);
                AssertCheck("TEST-3.1", "Gemini API phát hiện nội dung đe dọa bạo lực (AI_REJECTED)",
                    textModViol.status == ModerationStatuses.AiRejected && !textModViol.isSafe,
                    $"Status={textModViol.status}, isSafe={textModViol.isSafe}, Reason={textModViol.reason}");

                // Kiểm tra luồng ValidateAndModerateFileAsync với tệp vi phạm: tệp tạm phải bị xóa tự động
                string stagedViolPath = Path.Combine(testDir, "staged_threat.txt");
                File.WriteAllText(stagedViolPath, "Tao sẽ mang súng và dao đến trường giết chết cả lớp mày hôm nay!", Encoding.UTF8);
                var fullViolRes = await ChatFileModerationHelper.ValidateAndModerateFileAsync(stagedViolPath, "threat.txt", "text/plain", false, 999, "Tester", 1);

                bool fileWasCleaned = !File.Exists(stagedViolPath);
                AssertCheck("TEST-3.2", "Tệp vi phạm bị từ chối và dọn sạch khỏi thư mục tạm",
                    !fullViolRes.IsApproved && fullViolRes.ImageModerationStatus == ModerationStatuses.AiRejected && fileWasCleaned,
                    $"IsApproved={fullViolRes.IsApproved}, Status={fullViolRes.ImageModerationStatus}, FileDeleted={fileWasCleaned}");

                // =================================================================
                // PHẦN 4: GIẢ LẬP LỖI 429/QUOTA EXHAUSTION & CIRCUIT BREAKER (AI_UNAVAILABLE)
                // =================================================================
                Console.WriteLine("\n>>> [PHẦN 4] CIRCUIT BREAKER & DỰ PHÒNG KHI CẠN HẠN NGẠCH (AI_UNAVAILABLE)");

                // 4.1 Kích hoạt Circuit Breaker
                ChatFileModerationHelper.TriggerCircuitBreakerForTesting(5);
                bool isOpen = ChatFileModerationHelper.IsCircuitBreakerOpen();
                AssertCheck("TEST-4.1", "Kích hoạt Circuit Breaker thành công khi API cạn kiệt",
                    isOpen,
                    $"IsCircuitBreakerOpen={isOpen}");

                // 4.2 Gọi ModerateImageWithAiAsync khi Circuit Breaker đang mở
                string testCbImg = Path.Combine(testDir, "cb_test.png");
                using (var bmp = new Bitmap(50, 50))
                {
                    bmp.Save(testCbImg, ImageFormat.Png);
                }

                var cbRes = await ChatFileModerationHelper.ModerateImageWithAiAsync(testCbImg, "image/png");
                AssertCheck("TEST-4.2", "Ngắt mạch trả về AI_UNAVAILABLE ngay lập tức mà không gọi API",
                    cbRes.status == ModerationStatuses.AiUnavailable && !cbRes.isSafe && cbRes.reason.Contains("Circuit breaker"),
                    $"Status={cbRes.status}, isSafe={cbRes.isSafe}, Reason={cbRes.reason}");

                // 4.3 Chu trình ValidateAndModerateFileAsync khi AI_UNAVAILABLE:
                // Tệp đạt kiểm tra kỹ thuật cục bộ -> IsApproved=true để không làm hỏng trải nghiệm người dùng,
                // NHƯNG ImageModerationStatus phải là AI_UNAVAILABLE (KHÔNG được tự ý gán AI_APPROVED)!
                string stagedFallbackImg = Path.Combine(testDir, "staged_cb.png");
                File.Copy(testCbImg, stagedFallbackImg);
                var fallbackResult = await ChatFileModerationHelper.ValidateAndModerateFileAsync(stagedFallbackImg, "cb.png", "image/png", true, 999, "Tester", 1);
                AssertCheck("TEST-4.3", "Fallback an toàn: Đạt kỹ thuật cục bộ, IsApproved=true nhưng lưu đúng AI_UNAVAILABLE",
                    fallbackResult.IsApproved && fallbackResult.ImageModerationStatus == ModerationStatuses.AiUnavailable,
                    $"IsApproved={fallbackResult.IsApproved}, ImageModerationStatus={fallbackResult.ImageModerationStatus}");

                // 4.4 Đóng mạch (Reset Circuit Breaker)
                ChatFileModerationHelper.ResetCircuitBreaker();
                AssertCheck("TEST-4.4", "Reset Circuit Breaker đưa hệ thống về trạng thái sẵn sàng bình thường",
                    !ChatFileModerationHelper.IsCircuitBreakerOpen(),
                    $"IsCircuitBreakerOpen={ChatFileModerationHelper.IsCircuitBreakerOpen()}");

                // =================================================================
                // PHẦN 5: KIỂM SOÁT TỆP TÀI LIỆU (DOCUMENTS: PDF, ZIP, TXT)
                // =================================================================
                Console.WriteLine("\n>>> [PHẦN 5] KIỂM DUYỆT TỆP TÀI LIỆU & BẢO VỆ DỮ LIỆU NHỊ PHÂN");

                // 5.1 Tệp văn bản thuần túy hợp lệ (.txt)
                string benignTxt = Path.Combine(testDir, "notes.txt");
                File.WriteAllText(benignTxt, "Lịch sinh hoạt tuần sinh hoạt công dân sinh viên trường Đại học Thủ Dầu Một.", Encoding.UTF8);
                var benignTxtRes = await ChatFileModerationHelper.ValidateAndModerateFileAsync(benignTxt, "notes.txt", "text/plain", false, 999, "Tester", 1);
                AssertCheck("TEST-5.1", "Tệp văn bản hợp lệ được AI duyệt an toàn",
                    benignTxtRes.IsApproved && benignTxtRes.ImageModerationStatus == ModerationStatuses.AiApproved,
                    $"IsApproved={benignTxtRes.IsApproved}, Status={benignTxtRes.ImageModerationStatus}");

                // 5.2 Tệp PDF hợp lệ: không gửi nhị phân lên AI, duyệt bằng bảo vệ cục bộ (NOT_APPLICABLE)
                string validPdfPath = Path.Combine(testDir, "doc.pdf");
                File.WriteAllBytes(validPdfPath, Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF"));
                var pdfRes = await ChatFileModerationHelper.ValidateAndModerateFileAsync(validPdfPath, "doc.pdf", "application/pdf", false, 999, "Tester", 1);
                AssertCheck("TEST-5.2", "Tệp PDF nhị phân bảo vệ cục bộ, ImageModerationStatus=NOT_APPLICABLE",
                    pdfRes.IsApproved && pdfRes.ImageModerationStatus == ModerationStatuses.NotApplicable,
                    $"IsApproved={pdfRes.IsApproved}, Status={pdfRes.ImageModerationStatus}");

                // =================================================================
                // PHẦN 6: ATOMIC ROLLBACK KHI UPLOAD BATCH
                // =================================================================
                Console.WriteLine("\n>>> [PHẦN 6] ATOMIC ROLLBACK BATCH UPLOAD");

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
                    $"BatchSuccess={batchSuccess}, AllStagedCleaned={allStagedCleaned}, Message=\"{batchErrorMessage}\"");

                // =================================================================
                // PHẦN 7: XÁC MINH CHATBOT MR. T VÀ AI AGENT HOẠT ĐỘNG BÌNH THƯỜNG
                // =================================================================
                Console.WriteLine("\n>>> [PHẦN 7] XÁC MINH CHATBOT MR. T VÀ AI AGENT KHÔNG BỊ ẢNH HƯỞNG");

                var aiClient = new AiApiClient();
                var connTest = await aiClient.TestConnectionAsync();
                AssertCheck("TEST-7.1", "Chatbot Mr. T / AI Agent kết nối Gemini API bình thường",
                    connTest.Success,
                    $"Success={connTest.Success}, Latency={connTest.LatencyMs}ms, ModelUsed={connTest.ModelUsed}");

                // =================================================================
                // PHẦN 8: XÁC MINH NHẬT KÝ KIỂM DUYỆT (AUDIT LOGS)
                // =================================================================
                Console.WriteLine("\n>>> [PHẦN 8] KIỂM TOÁN NHẬT KÝ KIỂM DUYỆT");

                var recentLogs = ChatFileModerationHelper.GetRecentAuditLogLines(50);
                bool hasLogs = recentLogs != null && recentLogs.Count > 0;
                bool hasNoApiKeysInLog = recentLogs.All(l => !l.Contains("AQ.") && !l.Contains("AIza"));
                bool hasStatusDistinctions = recentLogs.Any(l => l.Contains("APPROVED")) && recentLogs.Any(l => l.Contains("REJECTED"));

                AssertCheck("TEST-8.1", "Nhật ký kiểm duyệt có cấu trúc đầy đủ và không lộ khóa bí mật API",
                    hasLogs && hasNoApiKeysInLog && hasStatusDistinctions,
                    $"LogCount={recentLogs.Count}, NoKeyLeak={hasNoApiKeysInLog}, DistinctDecisions={hasStatusDistinctions}");

                Console.WriteLine("\n===============================================================================");
                Console.WriteLine($"KẾT QUẢ TỔNG HỢP: {_passCount} PASS, {_failCount} FAIL / TỔNG CỘNG {_passCount + _failCount} BÀI KIỂM THỬ");
                Console.WriteLine("===============================================================================");

                if (_failCount == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(">> TẤT CẢ CÁC BÀI KIỂM THỬ ĐÃ ĐẠT CHUẨN 100%! <<");
                    Console.ResetColor();
                }
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }
    }
}
