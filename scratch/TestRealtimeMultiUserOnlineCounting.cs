using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNet.SignalR;
using StudentConnect.Hubs;

class Program
{
    static int passed = 0;
    static int failed = 0;

    static void Assert(bool condition, string testName, string detail = "")
    {
        if (condition)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[PASS] {testName} {detail}");
            Console.ResetColor();
            passed++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] {testName} {detail}");
            Console.ResetColor();
            failed++;
        }
    }

    static void Main()
    {
        Console.WriteLine("=================================================================");
        Console.WriteLine("  KIỂM THỬ THỰC TẾ: ĐẾM NGƯỜI ONLINE SIGNALR ĐA KẾT NỐI (ROOM 1)");
        Console.WriteLine("=================================================================");

        int roomId = 1;

        // BƯỚC 0: Ban đầu phòng trống
        int c0 = ChatHub.GetRoomOnlineCount(roomId);
        Assert(c0 == 0, "Trạng thái ban đầu: Phòng không có ai", $"Count={c0}");

        var hub = new ChatHub();
        
        string connA1 = "conn_userA_tab1_" + Guid.NewGuid().ToString("N");
        string connA2 = "conn_userA_tab2_" + Guid.NewGuid().ToString("N");
        string connB1 = "conn_userB_tab1_" + Guid.NewGuid().ToString("N");

        int userA = 101;
        int userB = 102;

        // 1. User A mở Tab 1
        SimulateJoin(hub, connA1, userA, roomId);
        int c1 = ChatHub.GetRoomOnlineCount(roomId);
        Assert(c1 == 1, "User A mở Tab 1 -> Số người online là 1", $"Count={c1}");

        // 2. User A mở thêm Tab 2 (cùng User A)
        SimulateJoin(hub, connA2, userA, roomId);
        int c2 = ChatHub.GetRoomOnlineCount(roomId);
        Assert(c2 == 1, "User A mở thêm Tab 2 -> Vẫn chỉ tính 1 người duy nhất", $"Count={c2}");

        // 3. User B mở Tab 1
        SimulateJoin(hub, connB1, userB, roomId);
        int c3 = ChatHub.GetRoomOnlineCount(roomId);
        Assert(c3 == 2, "User B mở Tab 1 -> Số người online tăng lên 2", $"Count={c3}");

        // 4. User A đóng Tab 1 (vẫn còn Tab 2)
        SimulateDisconnect(hub, connA1, userA);
        int c4 = ChatHub.GetRoomOnlineCount(roomId);
        Assert(c4 == 2, "User A đóng Tab 1 (vẫn còn Tab 2) -> Số người online vẫn là 2", $"Count={c4}");

        // 5. User A đóng Tab 2 (User A rời phòng hoàn toàn)
        SimulateDisconnect(hub, connA2, userA);
        int c5 = ChatHub.GetRoomOnlineCount(roomId);
        Assert(c5 == 1, "User A đóng nốt Tab 2 -> Số người online giảm xuống 1 (chỉ còn User B)", $"Count={c5}");

        // 6. User B đóng Tab 1 (User B rời phòng)
        SimulateDisconnect(hub, connB1, userB);
        int c6 = ChatHub.GetRoomOnlineCount(roomId);
        Assert(c6 == 0, "User B đóng Tab 1 -> Số người online giảm về 0", $"Count={c6}");

        Console.WriteLine("\n=================================================================");
        Console.WriteLine($"KẾT QUẢ KIỂM THỬ ĐẾM REALTIME: {passed} PASS, {failed} FAIL / TỔNG {passed + failed}");
        Console.WriteLine("=================================================================");

        if (failed > 0) Environment.Exit(1);
    }

    static void SimulateJoin(ChatHub hub, string connId, int userId, int roomId)
    {
        var type = typeof(ChatHub);
        var userConnsField = type.GetField("_userConnections", BindingFlags.NonPublic | BindingFlags.Static);
        var connToUserField = type.GetField("_connectionToUser", BindingFlags.NonPublic | BindingFlags.Static);
        var roomUsersField = type.GetField("_roomUserConnections", BindingFlags.NonPublic | BindingFlags.Static);
        var connRoomsField = type.GetField("_connectionRooms", BindingFlags.NonPublic | BindingFlags.Static);

        var userConns = (ConcurrentDictionary<int, HashSet<string>>)userConnsField.GetValue(null);
        var connToUser = (ConcurrentDictionary<string, int>)connToUserField.GetValue(null);
        var roomUsers = (ConcurrentDictionary<int, ConcurrentDictionary<int, HashSet<string>>>)roomUsersField.GetValue(null);
        var connRooms = (ConcurrentDictionary<string, ConcurrentDictionary<int, byte>>)connRoomsField.GetValue(null);

        connToUser[connId] = userId;

        userConns.AddOrUpdate(
            userId,
            _ => new HashSet<string> { connId },
            (_, set) => { lock (set) { set.Add(connId); } return set; }
        );

        var rUsers = roomUsers.GetOrAdd(roomId, _ => new ConcurrentDictionary<int, HashSet<string>>());
        rUsers.AddOrUpdate(
            userId,
            _ => new HashSet<string> { connId },
            (_, set) => { lock (set) { set.Add(connId); } return set; }
        );

        var cRooms = connRooms.GetOrAdd(connId, _ => new ConcurrentDictionary<int, byte>());
        cRooms[roomId] = (byte)1;
    }

    static void SimulateDisconnect(ChatHub hub, string connId, int userId)
    {
        var type = typeof(ChatHub);
        var removeMethod = type.GetMethod("RemoveConnectionFromRoom", BindingFlags.NonPublic | BindingFlags.Static);
        var connRoomsField = type.GetField("_connectionRooms", BindingFlags.NonPublic | BindingFlags.Static);
        var connRooms = (ConcurrentDictionary<string, ConcurrentDictionary<int, byte>>)connRoomsField.GetValue(null);

        if (connRooms.TryGetValue(connId, out var rooms))
        {
            foreach (var rId in new List<int>(rooms.Keys))
            {
                removeMethod.Invoke(null, new object[] { connId, userId, rId });
            }
        }
    }
}
