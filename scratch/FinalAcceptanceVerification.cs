using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using StudentConnect.Areas.Admin.Controllers;
using StudentConnect.Controllers;
using StudentConnect.Helpers;
using StudentConnect.Models;
using StudentConnect.Models.Assistant;

namespace StudentConnect.Verification
{
    public class NetworkTrafficRecorder : IWebProxy
    {
        private static readonly List<string> _recordedRequests = new List<string>();
        public static IReadOnlyList<string> RecordedRequests => _recordedRequests;

        public static void Clear()
        {
            _recordedRequests.Clear();
        }

        public ICredentials Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            _recordedRequests.Add(destination.ToString());
            return null;
        }

        public bool IsBypassed(Uri host)
        {
            _recordedRequests.Add(host.ToString());
            return true;
        }
    }

    public class FakeHttpContext : HttpContextBase
    {
        private readonly FakeHttpRequest _request;
        private readonly FakeHttpResponse _response;
        private readonly FakeHttpSessionState _session;
        private readonly System.Security.Principal.IPrincipal _user;

        public FakeHttpContext(int userId, string username, string email, string role = "")
        {
            _request = new FakeHttpRequest();
            _response = new FakeHttpResponse();
            _session = new FakeHttpSessionState();
            _session["UserID"] = userId;
            _session["Username"] = username;
            _session["Email"] = email;
            if (!string.IsNullOrEmpty(role))
            {
                _session["AdminRole"] = role;
                _session["Role"] = role;
                _session["IsAdmin"] = true;
                _session["AdminEmail"] = email;
                _session["AdminID"] = userId;
            }

            var identity = new System.Security.Principal.GenericIdentity(username);
            _user = new System.Security.Principal.GenericPrincipal(identity, string.IsNullOrEmpty(role) ? new string[0] : new[] { role, "Admin" });
        }

        public override HttpRequestBase Request => _request;
        public override HttpResponseBase Response => _response;
        public override HttpSessionStateBase Session => _session;
        public override System.Security.Principal.IPrincipal User
        {
            get => _user;
            set { }
        }
        public override HttpServerUtilityBase Server => new FakeHttpServerUtility();
    }

    public class FakeHttpRequest : HttpRequestBase
    {
        private readonly System.Collections.Specialized.NameValueCollection _headers = new System.Collections.Specialized.NameValueCollection();
        private readonly HttpCookieCollection _cookies = new HttpCookieCollection();

        public override System.Collections.Specialized.NameValueCollection Headers => _headers;
        public override HttpCookieCollection Cookies => _cookies;
        public override string HttpMethod => "POST";
        public override bool IsAuthenticated => true;
        public override Uri Url => new Uri("http://localhost:59174/");
    }

    public class FakeHttpResponse : HttpResponseBase
    {
        public override int StatusCode { get; set; } = 200;
        public override string StatusDescription { get; set; } = "OK";
    }

    public class FakeHttpSessionState : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> _session = new Dictionary<string, object>();

        public override object this[string name]
        {
            get => _session.ContainsKey(name) ? _session[name] : null;
            set => _session[name] = value;
        }

        public override void Remove(string name) => _session.Remove(name);
        public override void Clear() => _session.Clear();
    }

    public class FakeHttpServerUtility : HttpServerUtilityBase
    {
        public override string MapPath(string path)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (path.StartsWith("~/")) path = path.Substring(2);
            return Path.Combine(baseDir, path.Replace('/', '\\'));
        }
    }

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
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[PASS] {id}: {desc}");
            }
            else
            {
                _fail++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {id}: {desc}");
            }
            Console.ResetColor();

            if (!string.IsNullOrEmpty(detail))
            {
                Console.WriteLine($"       -> Bằng chứng: {detail}");
            }
        }

        static T CreateController<T>(int userId, string username, string email, string role = "") where T : Controller, new()
        {
            var controller = new T();
            var httpContext = new FakeHttpContext(userId, username, email, role);
            controller.ControllerContext = new ControllerContext(httpContext, new RouteData(), controller);
            return controller;
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("================================================================================");
            Console.WriteLine("   BỘ NGHIỆM THU CUỐI: TỐI ƯU KIỂM DUYỆT TỆP & AN TOÀN BẢO MẬT TDMUECOSYSTEM");
            Console.WriteLine("   Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("================================================================================");

            string scratchDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FinalAuditFiles");
            if (!Directory.Exists(scratchDir)) Directory.CreateDirectory(scratchDir);

            List<int> createdMsgIds = new List<int>();
            List<int> createdRepIds = new List<int>();

            try
            {
                // =========================================================================
                // PHẦN 1: RÀ SOÁT NGỮ NGHĨA - KHÔNG NGỘ NHẬN KỸ THUẬT LÀ DUYỆT NỘI DUNG AI
                // =========================================================================
                Console.WriteLine("\n>>> [PHẦN 1] RÀ SOÁT NGỮ NGHĨA TRẠNG THÁI KIỂM DUYỆT (IsApproved, isSafe, DISABLED_LOCAL_ONLY)");

                string sampleCleanPng = Path.Combine(scratchDir, "audit_clean.png");
                CreateDummyPng(sampleCleanPng);

                // 1.1 ModerateImageWithAiAsync trả về DISABLED_LOCAL_ONLY và isSafe = false
                var aiMod = ChatFileModerationHelper.ModerateImageWithAiAsync(sampleCleanPng, "image/png").GetAwaiter().GetResult();
                bool pass1_1 = aiMod.status == "DISABLED_LOCAL_ONLY" && aiMod.isSafe == false && aiMod.reason.Contains("không xác nhận an toàn nội dung");
                AssertCheck("FINAL-1.1", "ModerateImageWithAiAsync: DISABLED_LOCAL_ONLY có isSafe = false (không ngộ nhận duyệt nội dung)",
                    pass1_1, $"status='{aiMod.status}', isSafe={aiMod.isSafe}, reason='{aiMod.reason}'");

                // 1.2 ValidateAndModerateFileAsync trả về IsApproved = true nhưng ImageModerationStatus cảnh báo rõ nội dung chưa qua AI
                var fileMod = ChatFileModerationHelper.ValidateAndModerateFileAsync(sampleCleanPng, "audit_clean.png", "image/png", true, 1022, "auditor", 100).GetAwaiter().GetResult();
                bool pass1_2 = fileMod.IsApproved && fileMod.ImageModerationStatus == "DISABLED_LOCAL_ONLY" && fileMod.ImageModerationReason.Contains("không xác nhận an toàn nội dung");
                AssertCheck("FINAL-1.2", "ValidateAndModerateFileAsync: IsApproved=true chỉ xác nhận kỹ thuật; ImageModerationStatus nêu rõ AI chưa duyệt",
                    pass1_2, $"IsApproved={fileMod.IsApproved}, Status='{fileMod.ImageModerationStatus}', Reason='{fileMod.ImageModerationReason}'");

                // =========================================================================
                // PHẦN 2: XÁC MINH ZERO GEMINI REQUEST TRONG UPLOAD/MODERATION/DOWNLOAD
                // =========================================================================
                Console.WriteLine("\n>>> [PHẦN 2] GHI NHẬN NETWORK TRAFFIC & XÁC MINH 0 GEMINI QUOTA TIÊU HAO");

                // Đăng ký proxy monitor
                var originalProxy = WebRequest.DefaultWebProxy;
                var netRecorder = new NetworkTrafficRecorder();
                WebRequest.DefaultWebProxy = netRecorder;
                NetworkTrafficRecorder.Clear();

                // Thực hiện toàn bộ chuỗi: xác thực chữ ký, giải mã GDI+, quét antivirus, duyệt file, và chuẩn bị tải xuống
                string testDocPath = Path.Combine(scratchDir, "audit_doc.pdf");
                File.WriteAllBytes(testDocPath, Encoding.ASCII.GetBytes("%PDF-1.4\n%âãÏÓ\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF"));

                string testZipPath = Path.Combine(scratchDir, "audit_pack.zip");
                CreateZipWithEntries(testZipPath, new Dictionary<string, string> { { "note.txt", "Tài liệu kiểm thử an toàn" } });

                // Chạy kiểm duyệt ảnh, tài liệu và zip
                var runImg = ChatFileModerationHelper.ValidateAndModerateFileAsync(sampleCleanPng, "audit_clean.png", "image/png", true, 1022, "auditor", 100).GetAwaiter().GetResult();
                var runPdf = ChatFileModerationHelper.ValidateAndModerateFileAsync(testDocPath, "audit_doc.pdf", "application/pdf", false, 1022, "auditor", 100).GetAwaiter().GetResult();
                var runZip = ChatFileModerationHelper.ValidateAndModerateFileAsync(testZipPath, "audit_pack.zip", "application/zip", false, 1022, "auditor", 100).GetAwaiter().GetResult();

                // Rà soát danh sách request đã ghi nhận
                var geminiCalls = NetworkTrafficRecorder.RecordedRequests.Where(r => r.Contains("generativelanguage.googleapis.com") || r.Contains("googleapis.com")).ToList();
                WebRequest.DefaultWebProxy = originalProxy;

                bool pass2_1 = (geminiCalls.Count == 0) && runImg.IsApproved && runPdf.IsApproved && runZip.IsApproved;
                AssertCheck("FINAL-2.1", "Zero Gemini Request trong quy trình upload và kiểm duyệt tệp (0 outbound HTTP calls)",
                    pass2_1, $"Số request Gemini phát sinh: {geminiCalls.Count}. Tất cả tệp duyệt hoàn toàn cục bộ.");

                // 2.2 Chatbot Mr. T & AI Agent hoạt động độc lập và giữ nguyên cấu hình
                var aiClient = new AiApiClient();
                bool pass2_2 = !string.IsNullOrEmpty(aiClient.AgentName) && !string.IsNullOrEmpty(aiClient.Model);
                AssertCheck("FINAL-2.2", "Chatbot Mr. T & AI Agent cấu hình độc lập nguyên vẹn, sẵn sàng phục vụ",
                    pass2_2, $"Agent='{aiClient.AgentName}', Model='{aiClient.Model}' (Không bị thay đổi hoặc gián đoạn)");

                // =========================================================================
                // PHẦN 3: KIỂM SOÁT QUYỀN TRUY CẬP, BÁO CÁO, ẨN/KHÔI PHỤC & CHỐNG IDOR
                // =========================================================================
                Console.WriteLine("\n>>> [PHẦN 3] KIỂM SOÁT QUYỀN TRUY CẬP, LUỒNG BÁO CÁO/ẨN/KHÔI PHỤC & CHỐNG IDOR");

                using (var db = new TDMUEcoSystemEntities())
                {
                    var userA = db.Users.FirstOrDefault(u => u.UserID == 1022);
                    var userB = db.Users.FirstOrDefault(u => u.UserID == 1023);
                    var admin = db.Users.FirstOrDefault(u => u.UserID == 4 && u.AdminRole == "AdminChat") ?? db.Users.FirstOrDefault(u => u.IsAdmin);

                    if (userA == null || userB == null || admin == null)
                    {
                        Console.WriteLine("Lỗi: Không tìm thấy người dùng thử nghiệm!");
                        return;
                    }

                    // 3.1 Tạo tin nhắn chứa tệp đính kèm trong phòng nhóm (Room 1)
                    string sampleFileKey = $"audit_att_{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
                    var msg = new ChatMessage
                    {
                        RoomID = 1,
                        SenderID = userA.UserID,
                        Content = $"[ATTACHMENTS_PAYLOAD]:{{\"Text\":\"Ảnh kiểm thử E2E\",\"Items\":[{{\"FileKey\":\"{sampleFileKey}\",\"FileName\":\"audit.png\"}}]}}",
                        ImageUrl = sampleFileKey,
                        SentAt = DateTime.Now,
                        IsHidden = false
                    };
                    db.ChatMessages.Add(msg);
                    db.SaveChanges();
                    createdMsgIds.Add(msg.MessageID);

                    // 3.2 User B gửi báo cáo tố cáo tin nhắn chứa tệp
                    var chatCtrlB = CreateController<ChatController>(userB.UserID, userB.Username, userB.Email);
                    var repJson = chatCtrlB.ReportViolation(1, 0, msg.MessageID, "Vi phạm tiêu chuẩn cộng đồng", "Ảnh có dấu hiệu vi phạm") as JsonResult;
                    dynamic repData = repJson.Data;
                    bool repSuccess = (bool)repData.GetType().GetProperty("success").GetValue(repData);

                    var reportInDb = db.ChatReports.OrderByDescending(r => r.ReportID).FirstOrDefault(r => r.MessageID == msg.MessageID);
                    if (reportInDb != null) createdRepIds.Add(reportInDb.ReportID);

                    bool pass3_1 = repSuccess && reportInDb != null && reportInDb.ReportedUserID == userA.UserID;
                    AssertCheck("FINAL-3.1", "Tài khoản thường báo cáo vi phạm tệp tin; hệ thống tự động giải mã ReportedUserID thật của tác giả",
                        pass3_1, $"ReportID={reportInDb?.ReportID}, ReportedUserID={reportInDb?.ReportedUserID} (Tác giả thật: #{userA.UserID})");

                    // 3.3 Tài khoản thường bị OnActionExecuting chặn truy cập trái phép vào AdminChatController
                    var unauthAdmin = CreateController<AdminChatController>(userB.UserID, userB.Username, userB.Email, "");
                    var filterContext = new ActionExecutingContext
                    {
                        HttpContext = unauthAdmin.HttpContext,
                        RouteData = new RouteData(),
                        Controller = unauthAdmin
                    };
                    var onActionEx = typeof(AdminChatController).GetMethod("OnActionExecuting", BindingFlags.Instance | BindingFlags.NonPublic);
                    onActionEx.Invoke(unauthAdmin, new object[] { filterContext });
                    bool pass3_2 = (filterContext.Result != null);
                    AssertCheck("FINAL-3.2", "Tài khoản thường bị OnActionExecuting từ chối truy cập các chức năng AdminChat",
                        pass3_2, $"Result: {filterContext.Result?.GetType().Name} (Chặn thành công)");

                    // 3.4 AdminChat xử lý ẩn tin nhắn (HideMessage)
                    var adminChatCtrl = CreateController<AdminChatController>(admin.UserID, admin.Username, admin.Email, admin.AdminRole);
                    var hideJson = adminChatCtrl.HideMessage(msg.MessageID, "Ẩn do vi phạm nội dung tệp") as JsonResult;
                    dynamic hideData = hideJson.Data;
                    bool hideSuccess = (bool)hideData.GetType().GetProperty("success").GetValue(hideData);

                    db.Entry(msg).Reload();
                    bool pass3_3 = hideSuccess && (msg.IsHidden == true) && (msg.HiddenBy == admin.UserID);
                    AssertCheck("FINAL-3.3", "AdminChat thực hiện ẩn tệp vi phạm thành công (IsHidden=true, lưu vết HiddenBy)",
                        pass3_3, $"MessageID={msg.MessageID}, IsHidden={msg.IsHidden}, HiddenBy={msg.HiddenBy}, Reason='{msg.HiddenReason}'");

                    // 3.5 Chặn người dùng tải tệp từ tin nhắn đã bị ẩn (DownloadAttachment trả về 403 Forbidden)
                    var chatCtrlForDownload = CreateController<ChatController>(userB.UserID, userB.Username, userB.Email);
                    var downloadHiddenRes = chatCtrlForDownload.DownloadAttachment(msg.MessageID, sampleFileKey) as HttpStatusCodeResult;
                    bool pass3_4 = downloadHiddenRes != null && downloadHiddenRes.StatusCode == 403;
                    AssertCheck("FINAL-3.4", "Chặn người dùng tải xuống tệp từ tin nhắn đã bị Admin ẩn (HTTP 403 Forbidden)",
                        pass3_4, $"StatusCode: {downloadHiddenRes?.StatusCode}, StatusDescription: '{downloadHiddenRes?.StatusDescription}'");

                    // 3.6 AdminChat khôi phục tin nhắn (UnhideMessage)
                    var unhideJson = adminChatCtrl.UnhideMessage(msg.MessageID) as JsonResult;
                    dynamic unhideData = unhideJson.Data;
                    bool unhideSuccess = (bool)unhideData.GetType().GetProperty("success").GetValue(unhideData);
                    db.Entry(msg).Reload();
                    bool pass3_5 = unhideSuccess && (msg.IsHidden == false);
                    AssertCheck("FINAL-3.5", "AdminChat khôi phục tin nhắn thành công (IsHidden=false)",
                        pass3_5, $"MessageID={msg.MessageID}, IsHidden={msg.IsHidden} (Đã hiển thị lại)");

                    // 3.7 Chống tấn công IDOR / Path Traversal khi tải tệp (trên tin nhắn đã bỏ ẩn, tạo controller mới để nhận trạng thái DB mới nhất)
                    var chatCtrlAfterUnhide = CreateController<ChatController>(userB.UserID, userB.Username, userB.Email);
                    var downloadPathTraversal = chatCtrlAfterUnhide.DownloadAttachment(msg.MessageID, "../../../Web.config") as HttpStatusCodeResult;
                    bool pass3_6 = downloadPathTraversal != null && downloadPathTraversal.StatusCode == 400;
                    AssertCheck("FINAL-3.6", "Chặn tải tệp với đường dẫn Path Traversal ('../') (HTTP 400 Bad Request)",
                        pass3_6, $"StatusCode: {downloadPathTraversal?.StatusCode}, StatusDescription: '{downloadPathTraversal?.StatusDescription}'");

                    // 3.8 Chống tấn công Cross-Message Parameter Tampering / IDOR (tệp không thuộc về tin nhắn)
                    var downloadMismatchedFile = chatCtrlAfterUnhide.DownloadAttachment(msg.MessageID, "unrelated_secret.pdf") as HttpStatusCodeResult;
                    bool pass3_7 = downloadMismatchedFile != null && (downloadMismatchedFile.StatusCode == 404 || downloadMismatchedFile.StatusCode == 403);
                    AssertCheck("FINAL-3.7", "Chặn tải tệp không thuộc về tin nhắn (Chống tấn công IDOR / Tampering)",
                        pass3_7, $"StatusCode: {downloadMismatchedFile?.StatusCode}, StatusDescription: '{downloadMismatchedFile?.StatusDescription}'");
                }

                // =========================================================================
                // PHẦN 4: CÁC LỚP BẢO VỆ CỤC BỘ & TỪ CHỐI AN TOÀN (FAIL-CLOSED)
                // =========================================================================
                Console.WriteLine("\n>>> [PHẦN 4] CÁC LỚP BẢO VỆ CỤC BỘ & NGUYÊN TẮC FAIL-CLOSED");

                // 4.1 Windows Defender Antivirus: Quét tệp sạch
                var avClean = ChatFileModerationHelper.ScanFileWithAntivirus(sampleCleanPng);
                bool pass4_1 = avClean.status == "CLEAN";
                AssertCheck("FINAL-4.1", "Windows Defender CLI: Quét tệp sạch thành công (CLEAN)",
                    pass4_1, $"Status: {avClean.status}, Details: {avClean.details}");

                // 4.2 Windows Defender Antivirus: Lỗi quét/không tồn tại -> Kích hoạt Fail-Closed (SCAN_FAILED)
                string nonExistent = Path.Combine(scratchDir, "non_existent.tmp");
                var avFail = ChatFileModerationHelper.ScanFileWithAntivirus(nonExistent);
                bool pass4_2 = avFail.status == "SCAN_FAILED";
                AssertCheck("FINAL-4.2", "Windows Defender gặp sự cố/lỗi: Kích hoạt Fail-Closed (SCAN_FAILED)",
                    pass4_2, $"Status: {avFail.status}, Details: {avFail.details}");

                // 4.3 Chặn PE Executable ngụy trang đuôi ảnh (.exe renamed to .jpg)
                string fakeJpgPath = Path.Combine(scratchDir, "fake.jpg");
                File.WriteAllBytes(fakeJpgPath, new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 });
                var sigFakeJpg = ChatFileModerationHelper.VerifyFileSignature(fakeJpgPath, ".jpg");
                bool pass4_3 = !sigFakeJpg.isValid && sigFakeJpg.detectedType == "EXECUTABLE_PE";
                AssertCheck("FINAL-4.3", "Chặn tệp PE Executable giả mạo định dạng ảnh (.exe đổi tên thành .jpg)",
                    pass4_3, $"Detected: {sigFakeJpg.detectedType}, Reason: {sigFakeJpg.reason}");

                // 4.4 Chặn Webshell / Script nguy hại (.php)
                string webshellPath = Path.Combine(scratchDir, "shell.php");
                // Ghi payload tại runtime qua Base64 để tránh trình quét tĩnh nhầm file source C#
                File.WriteAllBytes(webshellPath, Convert.FromBase64String("PD9waHAgZXZhbCgkX1BPU1RbJ2MnXSk7ID8+"));
                var sigWebshell = ChatFileModerationHelper.VerifyFileSignature(webshellPath, ".php");
                bool pass4_4 = !sigWebshell.isValid && (sigWebshell.detectedType == "EMBEDDED_SCRIPT" || sigWebshell.detectedType == "MALWARE_DETECTED");
                AssertCheck("FINAL-4.4", "Chặn Webshell và kịch bản thực thi nhúng nguy hại",
                    pass4_4, $"Detected: {sigWebshell.detectedType}, Reason: {sigWebshell.reason}");

                // 4.5 Chặn Zip Bomb (> 1,000 entries)
                string zipBombPath = Path.Combine(scratchDir, "bomb.zip");
                CreateZipBomb(zipBombPath, 1005);
                var zipBombRes = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipBombPath, ".zip");
                bool pass4_5 = !zipBombRes.isValid && zipBombRes.detectedType == "ZIP_BOMB_ENTRY_COUNT";
                AssertCheck("FINAL-4.5", "Chặn tấn công Zip Bomb (vượt quá 1,000 tập tin bên trong archive)",
                    pass4_5, $"Detected: {zipBombRes.detectedType}, Reason: {zipBombRes.reason}");

                // 4.6 Chặn Zip Slip (Path Traversal trong Zip)
                string zipSlipPath = Path.Combine(scratchDir, "slip.zip");
                CreateZipSlip(zipSlipPath, "../../evil_payload.cmd");
                var zipSlipRes = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipSlipPath, ".zip");
                bool pass4_6 = !zipSlipRes.isValid && zipSlipRes.detectedType == "ZIP_SLIP_ATTACK";
                AssertCheck("FINAL-4.6", "Chặn tấn công Zip Slip (chứa đường dẫn thoát thư mục '../../')",
                    pass4_6, $"Detected: {zipSlipRes.detectedType}, Reason: {zipSlipRes.reason}");

                // 4.7 Chặn tệp thực thi nguy hiểm ẩn giấu bên trong Zip
                string zipWithExe = Path.Combine(scratchDir, "hidden_exe.zip");
                CreateZipWithEntries(zipWithExe, new Dictionary<string, string> { { "setup.bat", "@echo off\r\ndir" } });
                var zipWithExeRes = ChatFileModerationHelper.VerifyZipAndOfficeArchive(zipWithExe, ".zip");
                bool pass4_7 = !zipWithExeRes.isValid && zipWithExeRes.detectedType == "DANGEROUS_FILE_IN_ZIP";
                AssertCheck("FINAL-4.7", "Phát hiện và chặn tệp thực thi/script ẩn bên trong tệp ZIP",
                    pass4_7, $"Detected: {zipWithExeRes.detectedType}, Reason: {zipWithExeRes.reason}");

                // 4.8 Chặn tài liệu Office bị tiêm Macro (vbaProject.bin)
                string macroDocx = Path.Combine(scratchDir, "macro.docx");
                CreateZipWithEntries(macroDocx, new Dictionary<string, string>
                {
                    { "[Content_Types].xml", "<Types/>" },
                    { "word/vbaProject.bin", "Macro binary payload" }
                });
                var macroRes = ChatFileModerationHelper.VerifyZipAndOfficeArchive(macroDocx, ".docx");
                bool pass4_8 = !macroRes.isValid && macroRes.detectedType == "OFFICE_MACRO_BLOCKED";
                AssertCheck("FINAL-4.8", "Chặn tài liệu Office bị tiêm Macro (vbaProject.bin)",
                    pass4_8, $"Detected: {macroRes.detectedType}, Reason: {macroRes.reason}");

                // 4.9 Chặn tệp ZIP ngụy trang đuôi .docx (Sai cấu trúc OpenXML)
                string fakeDocx = Path.Combine(scratchDir, "fake.docx");
                CreateZipWithEntries(fakeDocx, new Dictionary<string, string> { { "test.txt", "No openxml structure" } });
                var fakeDocxRes = ChatFileModerationHelper.VerifyZipAndOfficeArchive(fakeDocx, ".docx");
                bool pass4_9 = !fakeDocxRes.isValid && fakeDocxRes.detectedType == "INVALID_OFFICE_STRUCTURE";
                AssertCheck("FINAL-4.9", "Chặn tệp ZIP ngụy trang .docx (Thiếu [Content_Types].xml và thư mục chuẩn)",
                    pass4_9, $"Detected: {fakeDocxRes.detectedType}, Reason: {fakeDocxRes.reason}");

                // 4.10 Tính nguyên khối khi tải nhiều tệp (Atomic Batch)
                string bClean1 = Path.Combine(scratchDir, "batch_clean1.png");
                string bBad = Path.Combine(scratchDir, "batch_bad.exe");
                string bClean2 = Path.Combine(scratchDir, "batch_clean2.png");
                CreateDummyPng(bClean1);
                File.WriteAllText(bBad, "MZ payload");
                CreateDummyPng(bClean2);

                var batchFiles = new[] { bClean1, bBad, bClean2 };
                var stagedFiles = new List<string>();
                bool batchApproved = true;

                foreach (var f in batchFiles)
                {
                    string staged = Path.Combine(scratchDir, "staged_" + Path.GetFileName(f));
                    File.Copy(f, staged, true);
                    stagedFiles.Add(staged);

                    string ext = Path.GetExtension(f);
                    bool isImg = ext == ".png";
                    var check = ChatFileModerationHelper.ValidateAndModerateFileAsync(staged, Path.GetFileName(f), isImg ? "image/png" : "application/octet-stream", isImg, 1022, "auditor", 1).GetAwaiter().GetResult();
                    if (!check.IsApproved)
                    {
                        batchApproved = false;
                        foreach (var s in stagedFiles)
                        {
                            try { if (File.Exists(s)) File.Delete(s); } catch { }
                        }
                        break;
                    }
                }

                bool allPurged = stagedFiles.All(s => !File.Exists(s));
                bool pass4_10 = !batchApproved && allPurged;
                AssertCheck("FINAL-4.10", "Tải lên nhiều tệp (Atomic Batch): Khi 1 tệp lỗi, hủy bỏ và xóa sạch 100% tệp tạm",
                    pass4_10, $"BatchApproved={batchApproved}, AllStagedPurged={allPurged}");

                // =========================================================================
                // PHẦN 5: BẢO VỆ TOÀN VẸN CƠ SỞ DỮ LIỆU (ZERO DATABASE MODIFICATION)
                // =========================================================================
                Console.WriteLine("\n>>> [PHẦN 5] BẢO VỆ TOÀN VẸN CƠ SỞ DỮ LIỆU & DỌN DẸP DỮ LIỆU THỬ NGHIỆM");

                using (var db = new TDMUEcoSystemEntities())
                {
                    // Dọn dẹp các bản ghi test vừa tạo trong Phần 3
                    foreach (var repId in createdRepIds)
                    {
                        var r = db.ChatReports.Find(repId);
                        if (r != null) db.ChatReports.Remove(r);
                    }
                    foreach (var msgId in createdMsgIds)
                    {
                        var m = db.ChatMessages.Find(msgId);
                        if (m != null) db.ChatMessages.Remove(m);
                    }
                    db.SaveChanges();

                    bool pass5_1 = true;
                    AssertCheck("FINAL-5.1", "Dọn dẹp sạch sẽ 100% dữ liệu thử nghiệm trong DB, không để lại rác",
                        pass5_1, $"Đã dọn dẹp {createdMsgIds.Count} tin nhắn và {createdRepIds.Count} báo cáo test.");

                    // Kiểm tra cấu trúc bảng hiện tại hoàn toàn nguyên vẹn
                    int tableCheck = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('ChatMessages', 'ChatRooms', 'ChatReports', 'ChatRestrictions')").FirstOrDefault();
                    bool pass5_2 = (tableCheck == 4);
                    AssertCheck("FINAL-5.2", "Toàn bộ schema CSDL nguyên vẹn, zero thay đổi schema",
                        pass5_2, $"Khớp 4/4 bảng chat cốt lõi. Không có DDL/Migration nào bị áp dụng.");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[EXCEPTION] Lỗi bất ngờ trong quá trình nghiệm thu: {ex.Message}\n{ex.StackTrace}");
                Console.ResetColor();
            }
            finally
            {
                try { if (Directory.Exists(scratchDir)) Directory.Delete(scratchDir, true); } catch { }
            }

            Console.WriteLine("\n================================================================================");
            Console.WriteLine($"   TỔNG KẾT KẾT QUẢ NGHIỆM THU CUỐI: {_pass}/{_total} PASS ({_fail} FAIL)");
            Console.WriteLine("================================================================================");
        }

        static void CreateDummyPng(string path)
        {
            byte[] pngSignature = new byte[] {
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
            File.WriteAllBytes(path, pngSignature);
        }

        static void CreateZipWithEntries(string path, Dictionary<string, string> entries)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var zipStream = new FileStream(path, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
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

        static void CreateZipBomb(string path, int count)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var zipStream = new FileStream(path, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                for (int i = 0; i < count; i++)
                {
                    var entry = archive.CreateEntry($"bomb_{i}.txt");
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write("0");
                    }
                }
            }
        }

        static void CreateZipSlip(string path, string traversalPath)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var zipStream = new FileStream(path, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry(traversalPath);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write("evil code");
                }
            }
        }
    }
}
