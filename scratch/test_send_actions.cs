using StudentConnect.Controllers;
using StudentConnect.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace StudentConnect.Scratch
{
    public class FakeHttpContext : HttpContextBase
    {
        private readonly FakeHttpRequest _request = new FakeHttpRequest();
        private readonly FakeHttpResponse _response = new FakeHttpResponse();
        private readonly FakeHttpSessionState _session = new FakeHttpSessionState();
        private readonly System.Security.Principal.IPrincipal _user;

        public FakeHttpContext(int userId, string username, string email)
        {
            _session["UserID"] = userId;
            _session["Username"] = username;
            _session["Email"] = email;
            var identity = new System.Security.Principal.GenericIdentity(username);
            _user = new System.Security.Principal.GenericPrincipal(identity, new string[0]);
        }

        public override HttpRequestBase Request => _request;
        public override HttpResponseBase Response => _response;
        public override HttpSessionStateBase Session => _session;
        public override System.Security.Principal.IPrincipal User => _user;
        public override HttpServerUtilityBase Server => new FakeHttpServerUtility();
    }

    public class FakeHttpRequest : HttpRequestBase
    {
        public override string HttpMethod => "POST";
        public override bool IsAuthenticated => true;
        public override Uri Url => new Uri("http://localhost:59174/");
    }

    public class FakeHttpResponse : HttpResponseBase
    {
        public override int StatusCode { get; set; } = 200;
    }

    public class FakeHttpSessionState : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> _dict = new Dictionary<string, object>();
        public override object this[string name]
        {
            get => _dict.ContainsKey(name) ? _dict[name] : null;
            set => _dict[name] = value;
        }
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

    public class FakePostedFile : HttpPostedFileBase
    {
        private readonly byte[] _bytes;
        public FakePostedFile(string fileName, string contentType, byte[] bytes)
        {
            FileName = fileName;
            ContentType = contentType;
            _bytes = bytes;
        }
        public override string FileName { get; }
        public override string ContentType { get; }
        public override int ContentLength => _bytes.Length;
        public override Stream InputStream => new MemoryStream(_bytes);
        public override void SaveAs(string filename) => File.WriteAllBytes(filename, _bytes);
    }

    class Program
    {
        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            using (var db = new TDMUEcoSystemEntities())
            {
                var user = db.Users.FirstOrDefault();
                Console.WriteLine($"Test User: ID={user.UserID}, Username={user.Username}");

                var ctrl = new ChatController();
                var ctx = new FakeHttpContext(user.UserID, user.Username, user.Email);
                ctrl.ControllerContext = new ControllerContext(ctx, new RouteData(), ctrl);

                // TEST 1: SendMessage
                Console.WriteLine("\n--- TESTING SendMessage ---");
                try
                {
                    var res1 = ctrl.SendMessage(1, "Tin nhắn kiểm thử từ automation runner", null, false);
                    string json1 = Newtonsoft.Json.JsonConvert.SerializeObject(res1.Data);
                    Console.WriteLine("SendMessage Result: " + json1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SendMessage EXCEPTION: " + ex.ToString());
                }

                // TEST 2: SendMessageWithFiles (Image)
                Console.WriteLine("\n--- TESTING SendMessageWithFiles (PNG Image) ---");
                try
                {
                    byte[] pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };
                    var fakeFile = new FakePostedFile("test_dot.png", "image/png", pngBytes);
                    var res2 = ctrl.SendMessageWithFiles(1, "Ảnh kiểm thử", new List<HttpPostedFileBase> { fakeFile }, null, false).GetAwaiter().GetResult();
                    string json2 = Newtonsoft.Json.JsonConvert.SerializeObject(res2.Data);
                    Console.WriteLine("SendMessageWithFiles (Image) Result: " + json2);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SendMessageWithFiles (Image) EXCEPTION: " + ex.ToString());
                }

                // TEST 3: SendMessageWithFiles (Text / Doc File)
                Console.WriteLine("\n--- TESTING SendMessageWithFiles (Doc File) ---");
                try
                {
                    byte[] txtBytes = Encoding.UTF8.GetBytes("Nội dung tài liệu kiểm thử.");
                    var fakeDoc = new FakePostedFile("document.txt", "text/plain", txtBytes);
                    var res3 = ctrl.SendMessageWithFiles(1, "File đính kèm kiểm thử", new List<HttpPostedFileBase> { fakeDoc }, null, false).GetAwaiter().GetResult();
                    string json3 = Newtonsoft.Json.JsonConvert.SerializeObject(res3.Data);
                    Console.WriteLine("SendMessageWithFiles (Doc) Result: " + json3);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("SendMessageWithFiles (Doc) EXCEPTION: " + ex.ToString());
                }
            }
        }
    }
}
