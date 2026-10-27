using System;
using System.Linq;
using StudentConnect.Models;

namespace StudentConnect.Scratch
{
    class DirectMuteTest
    {
        static void Main()
        {
            using (var db = new TDMUEcoSystemEntities())
            {
                try
                {
                    var rest = new ChatRestriction
                    {
                        UserID = 1022,
                        RestrictionType = "Mute",
                        Reason = "Test restriction",
                        CreatedAt = DateTime.Now,
                        ExpiresAt = DateTime.Now.AddHours(24),
                        CreatedByAdminID = 4,
                        IsActive = true
                    };
                    db.ChatRestrictions.Add(rest);
                    db.SaveChanges();
                    Console.WriteLine("Mute SUCCESS: ID=" + rest.RestrictionID);

                    db.ChatRestrictions.Remove(rest);
                    db.SaveChanges();
                    Console.WriteLine("Cleaned Mute.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EX: " + ex);
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine("INNER: " + ex.InnerException);
                        if (ex.InnerException.InnerException != null)
                        {
                            Console.WriteLine("INNER INNER: " + ex.InnerException.InnerException);
                        }
                    }
                }
            }
        }
    }
}
