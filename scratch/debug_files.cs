using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using StudentConnect.Controllers;
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

class FakeHttpContext : HttpContextBase
{
    private readonly FakeHttpSessionState _session = new FakeHttpSessionState();
    private readonly HttpRequestBase _request = new FakeHttpRequest();
    private readonly HttpResponseBase _response = new FakeHttpResponse();
    public override HttpSessionStateBase Session => _session;
    public override HttpRequestBase Request => _request;
    public override HttpResponseBase Response => _response;
    public override HttpServerUtilityBase Server => new FakeHttpServerUtility();
}

class FakeHttpServerUtility : HttpServerUtilityBase
{
    public override string MapPath(string path)
    {
        if (path.StartsWith("~/")) path = path.Substring(2);
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.Replace('/', '\\'));
    }
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
    static void Main()
    {
        int testUserId = 1;
        using (var db = new TDMUEcoSystemEntities())
        {
            var user = db.Users.FirstOrDefault(u => u.IsAdmin) ?? db.Users.FirstOrDefault();
            if (user != null) testUserId = user.UserID;
        }

        var fakeContext = new FakeHttpContext();
        fakeContext.Session["UserID"] = testUserId;
        fakeContext.Session["UserRole"] = "Admin";

        var chatCtrl = new ChatController();
        chatCtrl.ControllerContext = new ControllerContext(fakeContext, new RouteData(), chatCtrl);

        byte[] validPng = new byte[]
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
        var mockImg = new MockPostedFile(validPng, "test_sample.png", "image/png");

        try
        {
            var task = chatCtrl.SendMessageWithFiles(1, "Tin nhắn kèm ảnh", new List<HttpPostedFileBase> { mockImg });
            var res = task.Result;
            Console.WriteLine("Success: " + res);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.ToString());
            if (ex.InnerException != null)
            {
                Console.WriteLine("InnerException: " + ex.InnerException.ToString());
            }
        }
    }
}
