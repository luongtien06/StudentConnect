using System;
using System.Linq;
using StudentConnect.Models;

namespace StudentConnect.Benchmark
{
    class Program
    {
        static void Main()
        {
            using (var db = new TDMUEcoSystemEntities())
            {
                foreach (int uid in new[] { 1, 4, 7 })
                {
                    var posts = db.Posts.Count(p => p.UserID == uid);
                    var shares = db.PostShares.Count(s => s.UserID == uid);
                    var sharedItems = db.SharedPosts.Count(s => s.UserID == uid);
                    var markets = db.MarketPosts.Count(m => m.UserID == uid);
                    var connects = db.ConnectPosts.Count(c => c.UserID == uid);
                    Console.WriteLine($"UserID {uid}: Posts={posts}, Shares={shares}, SharedItems={sharedItems}, Market={markets}, Connect={connects}");
                }
            }
        }
    }
}
