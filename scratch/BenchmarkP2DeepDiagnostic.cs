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

namespace StudentConnect.DeepDiagnostic
{
    public class DetailedQueryInfo
    {
        public string CommandText { get; set; }
        public double ElapsedMs { get; set; }
    }

    public class DetailedQueryInterceptor : DbCommandInterceptor
    {
        public static bool IsEnabled = false;
        public static List<DetailedQueryInfo> CurrentRunQueries = new List<DetailedQueryInfo>();

        public override void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            if (IsEnabled)
            {
                interceptionContext.SetUserState("sw", Stopwatch.StartNew());
            }
            base.ReaderExecuting(command, interceptionContext);
        }

        public override void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            base.ReaderExecuted(command, interceptionContext);
            if (IsEnabled)
            {
                var sw = interceptionContext.FindUserState("sw") as Stopwatch;
                if (sw != null)
                {
                    sw.Stop();
                    CurrentRunQueries.Add(new DetailedQueryInfo
                    {
                        CommandText = command.CommandText,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds
                    });
                }
            }
        }

        public override void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            if (IsEnabled)
            {
                interceptionContext.SetUserState("sw", Stopwatch.StartNew());
            }
            base.ScalarExecuting(command, interceptionContext);
        }

        public override void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            base.ScalarExecuted(command, interceptionContext);
            if (IsEnabled)
            {
                var sw = interceptionContext.FindUserState("sw") as Stopwatch;
                if (sw != null)
                {
                    sw.Stop();
                    CurrentRunQueries.Add(new DetailedQueryInfo
                    {
                        CommandText = command.CommandText,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds
                    });
                }
            }
        }

        public override void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            if (IsEnabled)
            {
                interceptionContext.SetUserState("sw", Stopwatch.StartNew());
            }
            base.NonQueryExecuting(command, interceptionContext);
        }

        public override void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            base.NonQueryExecuted(command, interceptionContext);
            if (IsEnabled)
            {
                var sw = interceptionContext.FindUserState("sw") as Stopwatch;
                if (sw != null)
                {
                    sw.Stop();
                    CurrentRunQueries.Add(new DetailedQueryInfo
                    {
                        CommandText = command.CommandText,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds
                    });
                }
            }
        }
    }

    public class BenchmarkResult
    {
        public double TotalActionMs { get; set; }
        public double TotalSqlMs { get; set; }
        public double PureCpuMs => Math.Max(0, TotalActionMs - TotalSqlMs);
        public int QueryCount { get; set; }
        public List<DetailedQueryInfo> Queries { get; set; }
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

    class DeepDiagnosticProgram
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("===============================================================================");
            Console.WriteLine("  P2 DEEP PERFORMANCE DIAGNOSTIC & REGRESSION INVESTIGATION (SITE4NOW)         ");
            Console.WriteLine("===============================================================================");

            var interceptor = new DetailedQueryInterceptor();
            DbInterception.Add(interceptor);

            // Ping DB & warm up
            Console.WriteLine("[STEP 0] Warming up connection to Site4Now (sql9001.site4now.net)...");
            using (var db = new TDMUEcoSystemEntities())
            {
                var swPing = Stopwatch.StartNew();
                var count = db.Users.Count();
                swPing.Stop();
                Console.WriteLine($"[STEP 0] Ping RTT: {swPing.ElapsedMilliseconds} ms | Total Users in DB: {count}");
            }

            // Warm up MVC routes & View engines
            var dummyController = new UserController();
            var dummyCtx = new FakeHttpContext();
            dummyCtx.Session["UserID"] = 1;
            dummyController.ControllerContext = new ControllerContext(dummyCtx, new RouteData(), dummyController);
            dummyController.Profile(1);

            const int SAMPLES = 30;

            // 1. OWNER VIEW (User 1 viewing User 1)
            var ownerStats = RunBenchmarkScenario("1. OWNER VIEW (User 1 viewing own profile)", currentUserId: 1, targetUserId: 1, samples: SAMPLES);

            // 2. OTHER USER VIEW (User 1 viewing User 4)
            var otherStats = RunBenchmarkScenario("2. OTHER USER VIEW (User 1 viewing User 4)", currentUserId: 1, targetUserId: 4, samples: SAMPLES);

            // 3. GUEST VIEW (Guest viewing User 1)
            var guestStats = RunBenchmarkScenario("3. GUEST VIEW (Guest viewing User 1)", currentUserId: 0, targetUserId: 1, samples: SAMPLES);

            // 4. PRIVATE ACCOUNT VIEW
            int privateUserId = GetOrCreatePrivateUserId();
            var privateStats = RunBenchmarkScenario($"4. PRIVATE ACCOUNT VIEW (Guest viewing Private User {privateUserId})", currentUserId: 0, targetUserId: privateUserId, samples: SAMPLES);

            // Clean up private flag if needed
            CleanupPrivateUserId(privateUserId);

            // Print Summary Comparison Table
            PrintFinalComparisonTable(ownerStats, otherStats, guestStats, privateStats);
        }

        static int GetOrCreatePrivateUserId()
        {
            using (var db = new TDMUEcoSystemEntities())
            {
                var privateUser = db.Users.FirstOrDefault(u => u.IsPrivate);
                if (privateUser != null)
                {
                    return privateUser.UserID;
                }
                var u7 = db.Users.FirstOrDefault(u => u.UserID == 7);
                if (u7 != null)
                {
                    u7.IsPrivate = true;
                    db.SaveChanges();
                    Console.WriteLine("[INFO] Temporarily marked User 7 as IsPrivate = true for testing.");
                    return 7;
                }
                return 1;
            }
        }

        static void CleanupPrivateUserId(int userId)
        {
            if (userId == 7)
            {
                try
                {
                    using (var db = new TDMUEcoSystemEntities())
                    {
                        var u7 = db.Users.FirstOrDefault(u => u.UserID == 7);
                        if (u7 != null)
                        {
                            u7.IsPrivate = false;
                            db.SaveChanges();
                            Console.WriteLine("[INFO] Restored User 7 IsPrivate = false.");
                        }
                    }
                }
                catch { }
            }
        }

        static List<BenchmarkResult> RunBenchmarkScenario(string title, int currentUserId, int targetUserId, int samples)
        {
            Console.WriteLine($"\n===============================================================================");
            Console.WriteLine($"  RUNNING: {title} (N={samples} samples)");
            Console.WriteLine($"===============================================================================");

            var results = new List<BenchmarkResult>();

            for (int i = 0; i < samples; i++)
            {
                var controller = new UserController();
                var httpContext = new FakeHttpContext();
                if (currentUserId > 0)
                {
                    httpContext.Session["UserID"] = currentUserId;
                    httpContext.Session["Username"] = currentUserId == 1 ? "Tien_AdminS" : "User" + currentUserId;
                }
                controller.ControllerContext = new ControllerContext(httpContext, new RouteData(), controller);

                DetailedQueryInterceptor.CurrentRunQueries.Clear();
                DetailedQueryInterceptor.IsEnabled = true;

                var swAction = Stopwatch.StartNew();
                var actionResult = controller.Profile(targetUserId) as ViewResult;
                swAction.Stop();

                DetailedQueryInterceptor.IsEnabled = false;

                double totalSql = DetailedQueryInterceptor.CurrentRunQueries.Sum(q => q.ElapsedMs);
                var runResult = new BenchmarkResult
                {
                    TotalActionMs = swAction.Elapsed.TotalMilliseconds,
                    TotalSqlMs = totalSql,
                    QueryCount = DetailedQueryInterceptor.CurrentRunQueries.Count,
                    Queries = new List<DetailedQueryInfo>(DetailedQueryInterceptor.CurrentRunQueries)
                };
                results.Add(runResult);

                if ((i + 1) % 5 == 0 || i == 0)
                {
                    Console.WriteLine($"  Sample {i + 1,2}/{samples}: Action={runResult.TotalActionMs:F1}ms | SQL={runResult.TotalSqlMs:F1}ms ({runResult.QueryCount} queries) | Pure CPU={runResult.PureCpuMs:F1}ms");
                }
            }

            PrintQueryBreakdown(results);

            return results;
        }

        static void PrintQueryBreakdown(List<BenchmarkResult> results)
        {
            if (results.Count == 0 || results[0].Queries.Count == 0) return;

            int queryCount = results[0].Queries.Count;
            Console.WriteLine($"\n  [Per-Query Latency Breakdown across {results.Count} runs]:");
            Console.WriteLine($"  {"#",-3} | {"Avg(ms)",-8} | {"Min(ms)",-8} | {"Max(ms)",-8} | {"% Total SQL",-11} | Query Snippet");
            Console.WriteLine($"  {new string('-', 90)}");

            double avgTotalSql = results.Average(r => r.TotalSqlMs);

            for (int q = 0; q < queryCount; q++)
            {
                int qIdx = q;
                var qTimes = results.Where(r => r.Queries.Count > qIdx).Select(r => r.Queries[qIdx].ElapsedMs).ToList();
                if (qTimes.Count == 0) continue;

                double avg = qTimes.Average();
                double min = qTimes.Min();
                double max = qTimes.Max();
                double pct = avgTotalSql > 0 ? (avg / avgTotalSql) * 100 : 0;

                string rawSql = results[0].Queries[qIdx].CommandText.Trim().Replace("\r", " ").Replace("\n", " ");
                while (rawSql.Contains("  ")) rawSql = rawSql.Replace("  ", " ");
                string snippet = rawSql.Length > 45 ? rawSql.Substring(0, 45) + "..." : rawSql;

                Console.WriteLine($"  {q + 1,-3:D2} | {avg,-8:F1} | {min,-8:F1} | {max,-8:F1} | {pct,-10:F1}% | {snippet}");
            }
        }

        static void PrintFinalComparisonTable(List<BenchmarkResult> owner, List<BenchmarkResult> other, List<BenchmarkResult> guest, List<BenchmarkResult> priv)
        {
            Console.WriteLine($"\n===============================================================================");
            Console.WriteLine($"  FINAL BENCHMARK STATISTICAL SUMMARY TABLE (N=30 each)");
            Console.WriteLine($"===============================================================================");
            Console.WriteLine($"{"Scenario",-28} | {"Queries",-7} | {"Min(ms)",-8} | {"p50(ms)",-8} | {"Avg(ms)",-8} | {"p90(ms)",-8} | {"p95(ms)",-8} | {"Max(ms)",-8} | {"SQL%",-6}");
            Console.WriteLine(new string('-', 100));

            PrintScenarioRow("1. Owner View", owner);
            PrintScenarioRow("2. Other User View", other);
            PrintScenarioRow("3. Guest View", guest);
            PrintScenarioRow("4. Private Account View", priv);
        }

        static void PrintScenarioRow(string name, List<BenchmarkResult> results)
        {
            var times = results.Select(r => r.TotalActionMs).OrderBy(t => t).ToList();
            int n = times.Count;
            double min = times[0];
            double max = times[n - 1];
            double avg = times.Average();
            double p50 = times[(int)(n * 0.50)];
            double p90 = times[(int)(n * 0.90)];
            double p95 = times[(int)(n * 0.95)];
            int queries = results[0].QueryCount;
            double avgSql = results.Average(r => r.TotalSqlMs);
            double sqlPct = avg > 0 ? (avgSql / avg) * 100 : 0;

            Console.WriteLine($"{name,-28} | {queries,-7} | {min,-8:F1} | {p50,-8:F1} | {avg,-8:F1} | {p90,-8:F1} | {p95,-8:F1} | {max,-8:F1} | {sqlPct:F1}%");
        }
    }
}
