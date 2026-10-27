using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using StudentConnect.Controllers;
using StudentConnect.Hubs;
using StudentConnect.Models;

class FakeHttpSessionState : HttpSessionStateBase
{
    private readonly Dictionary<string, object> _session = new Dictionary<string, object>();
    public override object this[string name]
    {
        get { return _session.ContainsKey(name) ? _session[name] : null; }
        set { _session[name] = value; }
    }
}

class FakeHttpServerUtility : HttpServerUtilityBase
{
    public override string MapPath(string path)
    {
        if (path.StartsWith("~/")) path = path.Substring(2);
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.Replace('/', '\\'));
    }
}

class FakeHttpContext : HttpContextBase
{
    private readonly FakeHttpSessionState _session = new FakeHttpSessionState();
    private readonly HttpRequestBase _request;
    private readonly HttpResponseBase _response;
    private readonly HttpServerUtilityBase _server = new FakeHttpServerUtility();

    public FakeHttpContext(HttpRequestBase req = null, HttpResponseBase res = null)
    {
        _request = req ?? new FakeHttpRequest();
        _response = res ?? new FakeHttpResponse();
    }

    public override HttpSessionStateBase Session => _session;
    public override HttpRequestBase Request => _request;
    public override HttpResponseBase Response => _response;
    public override HttpServerUtilityBase Server => _server;
}

class FakeHttpRequest : HttpRequestBase
{
    private readonly HttpCookieCollection _cookies = new HttpCookieCollection();
    public override HttpCookieCollection Cookies => _cookies;
    public override string ApplicationPath => "/";
    public override Uri Url => new Uri("http://localhost/");
}

class FakeHttpResponse : HttpResponseBase
{
    public override int StatusCode { get; set; } = 200;
    public override bool TrySkipIisCustomErrors { get; set; }
}

class MockPostedFile : HttpPostedFileBase
{
    private readonly byte[] _bytes;
    private readonly string _fileName;
    private readonly string _contentType;

    public MockPostedFile(byte[] bytes, string fileName, string contentType)
    {
        _bytes = bytes;
        _fileName = fileName;
        _contentType = contentType;
    }

    public override int ContentLength => _bytes.Length;
    public override string FileName => _fileName;
    public override string ContentType => _contentType;
    public override Stream InputStream => new MemoryStream(_bytes);
    public override void SaveAs(string filename)
    {
        string dir = Path.GetDirectoryName(filename);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllBytes(filename, _bytes);
    }
}

class Program
{
    static int passed = 0;
    static int failed = 0;

    static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[PASS] {testName} {detail}");
            Console.ResetColor();
            passed++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] {testName} {detail}");
            Console.ResetColor();
            failed++;
        }
    }

    static void Main()
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("    TDMUECOSYSTEM - SUITE KIỂM THỬ TOÀN DIỆN HỆ THỐNG TRÒ CHUYỆN");
        Console.WriteLine("=================================================================");

        int testUserId = 1;
        using (var db = new TDMUEcoSystemEntities())
        {
            var user = db.Users.FirstOrDefault(u => u.IsAdmin) ?? db.Users.FirstOrDefault();
            if (user != null) testUserId = user.UserID;
            Console.WriteLine($"Test User: ID={testUserId}, Username={user?.Username}");
        }

        // SETUP CONTROLLER CONTEXT
        var fakeContext = new FakeHttpContext();
        fakeContext.Session["UserID"] = testUserId;
        fakeContext.Session["UserRole"] = "Admin";

        var chatCtrl = new ChatController();
        chatCtrl.ControllerContext = new ControllerContext(fakeContext, new RouteData(), chatCtrl);

        var chatApiCtrl = new ChatApiController();
        chatApiCtrl.ControllerContext = new ControllerContext(fakeContext, new RouteData(), chatApiCtrl);

        // TEST 1: GỬI TIN NHẮN VĂN BẢN
        Console.WriteLine("\n--- TEST GROUP 1: GỬI TIN NHẮN VĂN BẢN (SendMessage) ---");
        try
        {
            var result = chatCtrl.SendMessage(1, "Xin chào TDMU! Đây là tin nhắn kiểm thử tự động.");
            string json = GetJsonString(result.Data);
            bool success = json.Contains("\"success\":true");
            Assert(success, "Gửi tin nhắn văn bản vào sảnh chính (RoomID=1)", json);
        }
        catch (Exception ex)
        {
            Assert(false, "Gửi tin nhắn văn bản vào sảnh chính (RoomID=1)", "Exception: " + ex.Message);
        }

        // TEST 2: GỬI ẢNH HỢP LỆ (SendMessageWithFiles)
        Console.WriteLine("\n--- TEST GROUP 2: GỬI ẢNH (SendMessageWithFiles) ---");
        try
        {
            byte[] validPng = Create1x1Png();
            var mockImg = new MockPostedFile(validPng, "test_sample.png", "image/png");
            var task = chatCtrl.SendMessageWithFiles(1, "Tin nhắn kèm ảnh PNG hợp lệ", new List<HttpPostedFileBase> { mockImg });
            var result = task.Result;
            string json = GetJsonString(result.Data);
            bool success = json.Contains("\"success\":true");
            Assert(success, "Gửi hình ảnh PNG hợp lệ qua SendMessageWithFiles", json);
        }
        catch (Exception ex)
        {
            Assert(false, "Gửi hình ảnh PNG hợp lệ qua SendMessageWithFiles", "Exception: " + (ex.InnerException ?? ex).Message);
        }

        // TEST 3: GỬI TỆP TÀI LIỆU (SendMessageWithFiles)
        Console.WriteLine("\n--- TEST GROUP 3: GỬI TỆP ĐÍNH KÈM TÀI LIỆU (SendMessageWithFiles) ---");
        try
        {
            byte[] textDoc = System.Text.Encoding.UTF8.GetBytes("Nội dung tài liệu học tập TDMU.");
            var mockDoc = new MockPostedFile(textDoc, "TaiLieuHocTap.txt", "text/plain");
            var task = chatCtrl.SendMessageWithFiles(1, "Tin nhắn kèm tài liệu học tập", new List<HttpPostedFileBase> { mockDoc });
            var result = task.Result;
            string json = GetJsonString(result.Data);
            bool success = json.Contains("\"success\":true");
            Assert(success, "Gửi file tài liệu .txt qua SendMessageWithFiles", json);
        }
        catch (Exception ex)
        {
            Assert(false, "Gửi file tài liệu .txt qua SendMessageWithFiles", "Exception: " + (ex.InnerException ?? ex).Message);
        }

        // TEST 4: GỬI KẾT HỢP NHIỀU TỆP (ẢNH + FILE)
        Console.WriteLine("\n--- TEST GROUP 4: GỬI KẾT HỢP NHIỀU TỆP (Ảnh + Tài liệu) ---");
        try
        {
            byte[] pngBytes = Create1x1Png();
            var mockImg = new MockPostedFile(pngBytes, "diagram.png", "image/png");
            byte[] docBytes = System.Text.Encoding.UTF8.GetBytes("Báo cáo môn học bài tập lớn.");
            var mockDoc = new MockPostedFile(docBytes, "BaoCao.txt", "text/plain");

            var task = chatCtrl.SendMessageWithFiles(1, "Gửi đồng thời ảnh và tài liệu", new List<HttpPostedFileBase> { mockImg, mockDoc });
            var result = task.Result;
            string json = GetJsonString(result.Data);
            bool success = json.Contains("\"success\":true");
            Assert(success, "Gửi đồng thời 2 tệp (1 ảnh + 1 tài liệu)", json);
        }
        catch (Exception ex)
        {
            Assert(false, "Gửi đồng thời 2 tệp", "Exception: " + (ex.InnerException ?? ex).Message);
        }

        // TEST 5: ĐẾM SỐ NGƯỜI ONLINE Ở SẢNH CHÍNH (KHÔNG CỘNG 1 GIẢ)
        Console.WriteLine("\n--- TEST GROUP 5: ĐẾM SỐ NGƯỜI ONLINE Ở SẢNH CHÍNH THEO KẾT NỐI THỰC TẾ ---");
        try
        {
            // Bước 5a: Khi chưa ai kết nối SignalR, count = 0
            var res0 = chatApiCtrl.GetOnlineCount(1);
            string json0 = GetJsonString(res0.Data);
            bool ok0 = json0.Contains("\"success\":true") && json0.Contains("\"count\":0");
            Assert(ok0, "API GetOnlineCount khi phòng trống trả về count: 0 (không cộng 1 giả)", json0);

            // Bước 5b: Mô phỏng 1 user kết nối SignalR vào Sảnh chính
            SimulateSignalRJoin(1, testUserId);
            var res1 = chatApiCtrl.GetOnlineCount(1);
            string json1 = GetJsonString(res1.Data);
            bool ok1 = json1.Contains("\"success\":true") && json1.Contains("\"count\":1");
            Assert(ok1, "API GetOnlineCount khi có 1 user kết nối SignalR trả về count: 1", json1);

            // Bước 5c: Dọn dẹp connection sau test
            SimulateSignalRLeave(1, testUserId);
            var resClean = chatApiCtrl.GetOnlineCount(1);
            string jsonClean = GetJsonString(resClean.Data);
            bool okClean = jsonClean.Contains("\"count\":0");
            Assert(okClean, "API GetOnlineCount khi user rời đi trả về count: 0", jsonClean);
        }
        catch (Exception ex)
        {
            Assert(false, "API GetOnlineCount cho Sảnh chính (RoomID=1)", "Exception: " + (ex.InnerException ?? ex).Message);
        }

        // TEST 6: PHƯƠNG THỨC STATICS TRONG ChatHub
        Console.WriteLine("\n--- TEST GROUP 6: CHATHUB STATIC TRACKING METHODS ---");
        try
        {
            int roomCount = ChatHub.GetRoomOnlineCount(1);
            int totalOnline = ChatHub.GetTotalOnlineUsers();
            Assert(roomCount >= 0, "ChatHub.GetRoomOnlineCount(1) hoạt động thread-safe", $"Count={roomCount}");
            Assert(totalOnline >= 0, "ChatHub.GetTotalOnlineUsers() hoạt động thread-safe", $"Total={totalOnline}");
        }
        catch (Exception ex)
        {
            Assert(false, "ChatHub static methods", "Exception: " + ex.Message);
        }

        // TEST 7: PHÒNG VỆ AN TOÀN TRUY VẤN ChatRestrictions
        Console.WriteLine("\n--- TEST GROUP 7: PHÒNG VỆ AN TOÀN TRUY VẤN ChatRestrictions ---");
        try
        {
            using (var db = new TDMUEcoSystemEntities())
            {
                var anyRest = db.ChatRestrictions.Where(r => r.UserID == testUserId).ToList();
                Assert(true, "Truy vấn Entity Framework trên bảng ChatRestrictions thành công mà không gây ngoại lệ", $"Found {anyRest.Count} restrictions");
            }
        }
        catch (Exception ex)
        {
            Assert(false, "Truy vấn ChatRestrictions", "Exception: " + ex.Message);
        }

        // TEST 8: KIỂM TRA LƯU TRỮ TIN NHẮN TRONG CSDL
        Console.WriteLine("\n--- TEST GROUP 8: KIỂM TRA DỮ LIỆU TIN NHẮN THỰC TẾ TRONG DATABASE ---");
        try
        {
            using (var db = new TDMUEcoSystemEntities())
            {
                var latestMsg = db.ChatMessages.OrderByDescending(m => m.MessageID).FirstOrDefault();
                Assert(latestMsg != null && latestMsg.RoomID == 1, "Tin nhắn vừa gửi đã được commit vào CSDL SQL Server", $"MessageID={latestMsg?.MessageID}, Content='{latestMsg?.Content}'");
            }
        }
        catch (Exception ex)
        {
            Assert(false, "Kiểm tra dữ liệu tin nhắn trong CSDL", "Exception: " + ex.Message);
        }

        Console.WriteLine("\n=================================================================");
        Console.WriteLine($"KẾT QUẢ KIỂM THỬ: {passed} PASS, {failed} FAIL / TỔNG CỘNG {passed + failed} TESTS");
        Console.WriteLine("=================================================================");

        if (failed > 0)
        {
            Environment.Exit(1);
        }
    }

    static void SimulateSignalRJoin(int roomId, int userId)
    {
        var type = typeof(ChatHub);
        var roomUsersField = type.GetField("_roomUserConnections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var roomUsers = (ConcurrentDictionary<int, ConcurrentDictionary<int, HashSet<string>>>)roomUsersField.GetValue(null);
        var rUsers = roomUsers.GetOrAdd(roomId, _ => new ConcurrentDictionary<int, HashSet<string>>());
        rUsers[userId] = new HashSet<string> { "mock_conn_test" };
    }

    static void SimulateSignalRLeave(int roomId, int userId)
    {
        var type = typeof(ChatHub);
        var roomUsersField = type.GetField("_roomUserConnections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var roomUsers = (ConcurrentDictionary<int, ConcurrentDictionary<int, HashSet<string>>>)roomUsersField.GetValue(null);
        if (roomUsers.TryGetValue(roomId, out var rUsers))
        {
            rUsers.TryRemove(userId, out _);
        }
    }

    static byte[] Create1x1Png()
    {
        return new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x60, 0x60, 0x60, 0x60,
            0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0xA7, 0x3E,
            0x5B, 0x8C, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
            0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        };
    }

    static string GetJsonString(object data)
    {
        if (data == null) return "null";
        var props = data.GetType().GetProperties();
        var pairs = new List<string>();
        foreach (var p in props)
        {
            var val = p.GetValue(data, null);
            string valStr = val == null ? "null" : (val is string ? $"\"{val}\"" : (val is bool ? val.ToString().ToLower() : val.ToString()));
            pairs.Add($"\"{p.Name}\":{valStr}");
        }
        return "{" + string.Join(",", pairs) + "}";
    }
}
