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
                var users = db.Users.Select(u => new { u.UserID, u.Username, u.IsPrivate }).ToList();
                Console.WriteLine("Total Users: " + users.Count);
                foreach (var u in users)
                {
                    Console.WriteLine($"ID={u.UserID}, Username={u.Username}, IsPrivate={u.IsPrivate}");
                }
            }
        }
    }
}
