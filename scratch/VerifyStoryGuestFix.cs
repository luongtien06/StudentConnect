using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using StudentConnect.Controllers;
using StudentConnect.Models;

namespace StudentConnect.Verification
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== VERIFY STORY TRAY GUEST VS AUTHENTICATED ===");

            // 1. Guest request (No session)
            var guestCtrl = new StoryController();
            var guestHttp = new FakeHttpContext(0, null, null);
            guestCtrl.ControllerContext = new ControllerContext(guestHttp, new RouteData(), guestCtrl);

            var trayResult = guestCtrl.StoryTrayPartial() as PartialViewResult;
            var guestGroups = trayResult?.Model as List<UserStoryGroupViewModel>;
            bool guestHasCurrentUserGroup = guestGroups != null && guestGroups.Any(g => g.IsCurrentUser);
            Console.WriteLine($"[TEST 1] Guest StoryTrayPartial has IsCurrentUser group: {guestHasCurrentUserGroup} (Expected: False)");

            var storyIndexResult = guestCtrl.Index(null) as ViewResult;
            var guestIndexGroups = storyIndexResult?.Model as List<UserStoryGroupViewModel>;
            bool guestIndexHasCurrentUser = guestIndexGroups != null && guestIndexGroups.Any(g => g.IsCurrentUser);
            Console.WriteLine($"[TEST 2] Guest Story Index has IsCurrentUser group: {guestIndexHasCurrentUser} (Expected: False)");

            // 2. Polluted session: Admin logged in without frontend Username
            var adminPollutedCtrl = new StoryController();
            var adminHttp = new FakeHttpContext(1002, null, "admin@tdmu.edu.vn"); // UserID=1002 but Username=null
            adminPollutedCtrl.ControllerContext = new ControllerContext(adminHttp, new RouteData(), adminPollutedCtrl);

            var adminTrayResult = adminPollutedCtrl.StoryTrayPartial() as PartialViewResult;
            var adminGroups = adminTrayResult?.Model as List<UserStoryGroupViewModel>;
            bool adminHasCurrentUser = adminGroups != null && adminGroups.Any(g => g.IsCurrentUser);
            Console.WriteLine($"[TEST 3] Admin without frontend Username has IsCurrentUser group: {adminHasCurrentUser} (Expected: False)");

            // 3. Authenticated student (Both UserID and Username present)
            var studentCtrl = new StoryController();
            var studentHttp = new FakeHttpContext(1022, "testuser01", "testuser01@student.tdmu.edu.vn");
            studentCtrl.ControllerContext = new ControllerContext(studentHttp, new RouteData(), studentCtrl);

            var studentTrayResult = studentCtrl.StoryTrayPartial() as PartialViewResult;
            var studentGroups = studentTrayResult?.Model as List<UserStoryGroupViewModel>;
            bool studentHasCurrentUser = studentGroups != null && studentGroups.Any(g => g.IsCurrentUser);
            Console.WriteLine($"[TEST 4] Authenticated Student has IsCurrentUser group: {studentHasCurrentUser} (Expected: True)");

            if (!guestHasCurrentUserGroup && !guestIndexHasCurrentUser && !adminHasCurrentUser && studentHasCurrentUser)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n>>> ALL TESTS PASSED: GUEST NEVER SEES CURRENT USER / CREATE STORY GROUP! <<<");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n>>> SOME TESTS FAILED! <<<");
                Console.ResetColor();
            }
        }
    }

    public class FakeHttpContext : HttpContextBase
    {
        private readonly FakeHttpSessionState _session = new FakeHttpSessionState();
        private readonly FakeHttpRequest _request = new FakeHttpRequest();
        private readonly FakeHttpResponse _response = new FakeHttpResponse();

        public FakeHttpContext(int userId, string username, string email)
        {
            if (userId > 0) _session["UserID"] = userId;
            if (!string.IsNullOrEmpty(username)) _session["Username"] = username;
            if (!string.IsNullOrEmpty(email)) _session["Email"] = email;
        }

        public override HttpSessionStateBase Session => _session;
        public override HttpRequestBase Request => _request;
        public override HttpResponseBase Response => _response;
    }

    public class FakeHttpRequest : HttpRequestBase
    {
        public override string HttpMethod => "GET";
        public override bool IsAuthenticated => false;
    }

    public class FakeHttpResponse : HttpResponseBase { }

    public class FakeHttpSessionState : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>();
        public override object this[string name]
        {
            get => _items.ContainsKey(name) ? _items[name] : null;
            set => _items[name] = value;
        }
    }
}
