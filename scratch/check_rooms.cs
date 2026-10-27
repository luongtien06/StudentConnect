using StudentConnect.Models;
using System;
using System.Linq;

namespace StudentConnect.Scratch
{
    class Program
    {
        static void Main()
        {
            using (var db = new TDMUEcoSystemEntities())
            {
                var rooms = db.ChatRooms.ToList();
                Console.WriteLine($"Total rooms: {rooms.Count}");
                foreach (var r in rooms)
                {
                    int pCount = db.ChatParticipants.Count(p => p.RoomID == r.RoomID);
                    Console.WriteLine($"RoomID={r.RoomID}, Type='{r.RoomType}', IsActive={r.IsActive}, ParticipantsCount={pCount}");
                }
            }
        }
    }
}
