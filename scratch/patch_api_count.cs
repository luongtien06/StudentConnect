using System;
using System.IO;

class Program {
    static void Main() {
        string path = "Controllers\\ChatApiController.cs";
        string content = File.ReadAllText(path);

        string oldBlock = @"            int count = 0;
            if (room.RoomType == " + "\"Public\")" + @"
            {
                // Ưu tiên đếm số user đang có mặt trong phòng theo SignalR
                int roomUsers = Hubs.ChatHub.GetRoomOnlineCount(roomId);
                if (roomUsers > 0)
                {
                    count = roomUsers;
                }
                else
                {
                    // Dự phòng nếu client đang tải trang trước khi SignalR handshake xong:
                    // Đếm số user đang online trên toàn trang, tối thiểu là 1 (chính caller)
                    int totalOnline = Hubs.ChatHub.GetTotalOnlineUsers();
                    count = totalOnline > 0 ? totalOnline : 1;
                }
            }
            else
            {
                // Phòng riêng hoặc nhóm:
                int roomUsers = Hubs.ChatHub.GetRoomOnlineCount(roomId);
                if (roomUsers > 0)
                {
                    count = roomUsers;
                }
                else
                {
                    var participantIds = room.ChatParticipants
                        .Where(p => p.UserID.HasValue)
                        .Select(p => p.UserID.Value)
                        .ToList();
                    count = participantIds.Count(uid => Hubs.ChatHub.IsUserOnline(uid));
                    if (count <= 0) count = 1; // Bản thân caller đang trực tuyến trong phòng
                }
            }";

        string newBlock = @"            int count = 0;
            if (room.RoomType == " + "\"Public\")" + @"
            {
                // Đếm chính xác số user duy nhất đang kết nối thực tế trong sảnh chính qua SignalR
                count = Hubs.ChatHub.GetRoomOnlineCount(roomId);
            }
            else
            {
                // Phòng riêng hoặc nhóm: Đếm số user đang có mặt trực tiếp trong phòng
                int roomUsers = Hubs.ChatHub.GetRoomOnlineCount(roomId);
                if (roomUsers > 0)
                {
                    count = roomUsers;
                }
                else
                {
                    // Dự phòng: Đếm số thành viên của phòng này đang trực tuyến trên hệ thống
                    var participantIds = room.ChatParticipants
                        .Where(p => p.UserID.HasValue)
                        .Select(p => p.UserID.Value)
                        .ToList();
                    count = participantIds.Count(uid => Hubs.ChatHub.IsUserOnline(uid));
                }
            }";

        if (!content.Contains(oldBlock)) {
            Console.WriteLine("Old block not found directly, trying index approach");
            int startIdx = content.IndexOf("int count = 0;");
            int endIdx = content.IndexOf("return Json(new { success = true, count = count }", startIdx);
            if (startIdx >= 0 && endIdx >= 0) {
                content = content.Substring(0, startIdx) + newBlock + "\n\n            " + content.Substring(endIdx);
                File.WriteAllText(path, content, System.Text.Encoding.UTF8);
                Console.WriteLine("Replaced using index approach!");
                return;
            }
            Console.WriteLine("Could not find block!");
            return;
        }

        content = content.Replace(oldBlock, newBlock);
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        Console.WriteLine("Replaced GetOnlineCount successfully!");
    }
}
