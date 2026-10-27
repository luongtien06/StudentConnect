using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using StudentConnect.Controllers;
using StudentConnect.Models;

namespace StudentConnect.Benchmark
{
    public class QueryInterceptor : DbCommandInterceptor
    {
        public static int QueryCount = 0;
        public static List<string> Queries = new List<string>();
        public static bool IsEnabled = false;

        public override void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            if (IsEnabled)
            {
                QueryCount++;
                Queries.Add(command.CommandText);
            }
            base.ReaderExecuting(command, interceptionContext);
        }

        public override void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            if (IsEnabled)
            {
                QueryCount++;
                Queries.Add(command.CommandText);
            }
            base.ScalarExecuting(command, interceptionContext);
        }

        public override void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            if (IsEnabled)
            {
                QueryCount++;
                Queries.Add(command.CommandText);
            }
            base.NonQueryExecuting(command, interceptionContext);
        }
    }

    public class FakeHttpSessionState : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> _session = new Dictionary<string, object>();
        public override object this[string name]
        {
            get => _session.ContainsKey(name) ? _session[name] : null;
            set => _session[name] = value;
        }
        public override void Clear() => _session.Clear();
    }

    public class FakeHttpRequest : HttpRequestBase
    {
        public override string AppRelativeCurrentExecutionFilePath => "~/User/Profile";
        public override string PathInfo => "";
    }

    public class FakeHttpResponse : HttpResponseBase
    {
    }

    public class FakeHttpContext : HttpContextBase
    {
        private readonly FakeHttpSessionState _session = new FakeHttpSessionState();
        private readonly FakeHttpRequest _request = new FakeHttpRequest();
        private readonly FakeHttpResponse _response = new FakeHttpResponse();

        public override HttpSessionStateBase Session => _session;
        public override HttpRequestBase Request => _request;
        public override HttpResponseBase Response => _response;
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("===============================================================");
            Console.WriteLine("  BENCHMARK P2: FINAL POST-OPTIMIZATION PROFILE (SITE4NOW)     ");
            Console.WriteLine("===============================================================");

            var interceptor = new QueryInterceptor();
            DbInterception.Add(interceptor);

            // Warm-up connection
            Console.WriteLine("[INFO] Warming up connection to Site4Now...");
            using (var db = new TDMUEcoSystemEntities())
            {
                var count = db.Users.Count();
                Console.WriteLine($"[INFO] Connected to DB! Total Users: {count}");
            }

            // Test cases
            RunScenario("1. OWNER VIEW (User 1 viewing own Profile)", currentUserId: 1, targetUserId: 1, iterations: 5);
            RunScenario("2. OTHER USER VIEW (User 1 viewing User 4)", currentUserId: 1, targetUserId: 4, iterations: 5);
            RunScenario("3. GUEST VIEW (Guest viewing User 1)", currentUserId: 0, targetUserId: 1, iterations: 5);

            Console.WriteLine("\n[DONE] Final P2 Benchmark Completed Successfully.");
        }

        static void RunScenario(string title, int currentUserId, int targetUserId, int iterations)
        {
            Console.WriteLine($"\n-------------------------------------------------------------");
            Console.WriteLine($"Scenario: {title}");
            Console.WriteLine($"-------------------------------------------------------------");

            // Warm up scenario once
            {
                var ctrl = new UserController();
                var ctx = new FakeHttpContext();
                if (currentUserId > 0)
                {
                    ctx.Session["UserID"] = currentUserId;
                    ctx.Session["Username"] = currentUserId == 1 ? "Tien_AdminS" : "User" + currentUserId;
                }
                ctrl.ControllerContext = new ControllerContext(ctx, new RouteData(), ctrl);
                ctrl.Profile(targetUserId);
            }

            var elapsedList = new List<double>();
            int queryCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var controller = new UserController();
                var httpContext = new FakeHttpContext();
                if (currentUserId > 0)
                {
                    httpContext.Session["UserID"] = currentUserId;
                    httpContext.Session["Username"] = currentUserId == 1 ? "Tien_AdminS" : "User" + currentUserId;
                }
                controller.ControllerContext = new ControllerContext(httpContext, new RouteData(), controller);

                QueryInterceptor.QueryCount = 0;
                QueryInterceptor.Queries.Clear();
                QueryInterceptor.IsEnabled = true;

                var sw = Stopwatch.StartNew();
                var result = controller.Profile(targetUserId) as ViewResult;
                sw.Stop();

                QueryInterceptor.IsEnabled = false;
                elapsedList.Add(sw.Elapsed.TotalMilliseconds);
                queryCount = QueryInterceptor.QueryCount;

                var model = result?.Model as SocialProfileViewModel;
                Console.WriteLine($"  Iter {i + 1}: {sw.Elapsed.TotalMilliseconds:F1} ms | SQL Queries: {queryCount} | Posts: {model?.Posts?.Count ?? 0} | Friends: {model?.FriendsPreview?.Count ?? 0} | Stories: {model?.ActiveStories?.Count ?? 0}");
            }

            elapsedList.Sort();
            double p50 = elapsedList[elapsedList.Count / 2];
            double avg = elapsedList.Average();
            double min = elapsedList.First();
            double max = elapsedList.Last();

            Console.WriteLine($"===> SUMMARY [{title}]:");
            Console.WriteLine($"     Queries: {queryCount} SQL round-trips");
            Console.WriteLine($"     Time: Min={min:F1}ms | Max={max:F1}ms | Avg={avg:F1}ms | p50={p50:F1}ms");
        }
    }
}
