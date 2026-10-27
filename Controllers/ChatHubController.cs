using Microsoft.AspNet.SignalR;
using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentConnect.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<int, HashSet<string>> _userConnections
            = new ConcurrentDictionary<int, HashSet<string>>();

        // Bổ sung bản đồ O(1) đảo ngược từ ConnectionId -> UserID để giải phóng lock contention khi ngắt kết nối
        private static readonly ConcurrentDictionary<string, int> _connectionToUser
            = new ConcurrentDictionary<string, int>();

        // Quản lý người dùng theo phòng: RoomID -> (UserID -> HashSet<ConnectionId>)
        private static readonly ConcurrentDictionary<int, ConcurrentDictionary<int, HashSet<string>>> _roomUserConnections
            = new ConcurrentDictionary<int, ConcurrentDictionary<int, HashSet<string>>>();

        // Ánh xạ ConnectionId -> tập RoomID mà connection này đã tham gia
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _connectionRooms
            = new ConcurrentDictionary<string, ConcurrentDictionary<int, byte>>();

        // Bộ đệm ngắn hạn (TTL 15s) cho metadata phòng công cộng (RoomID -> (RoomType, IsActive, ExpireAtUtc))
        private static readonly ConcurrentDictionary<int, Tuple<string, bool, DateTime>> _publicRoomMetaCache
            = new ConcurrentDictionary<int, Tuple<string, bool, DateTime>>();

        /// <summary>
        /// Vô hiệu hóa bộ đệm metadata của phòng chat ngay lập tức khi trạng thái phòng thay đổi.
        /// </summary>
        public static void InvalidateRoomCache(int roomId)
        {
            if (roomId > 0)
            {
                _publicRoomMetaCache.TryRemove(roomId, out _);
            }
        }

        /// <summary>
        /// Xóa toàn bộ bộ đệm metadata phòng chat.
        /// </summary>
        public static void ClearAllRoomCache()
        {
            _publicRoomMetaCache.Clear();
        }

        /// <summary>
        /// Trục xuất toàn bộ các kết nối đang tham gia phòng khỏi SignalR Group trên Server
        /// khi phòng bị Quản trị viên đóng hoặc xóa.
        /// </summary>
        public static void EvictRoomConnections(int roomId, string reason = "Phòng trò chuyện đã bị đóng hoặc xóa bởi Quản trị viên.")
        {
            if (roomId <= 0) return;

            InvalidateRoomCache(roomId);

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();

            // 1. Gửi thông báo onForbidden tới tất cả client trong phòng trước khi thu hồi
            try
            {
                hubContext.Clients.Group(roomId.ToString()).onForbidden(reason);
            }
            catch { }

            // 2. Thu hồi toàn bộ Connection trên Server khỏi SignalR Group
            if (_roomUserConnections.TryRemove(roomId, out var roomUsers))
            {
                foreach (var userEntry in roomUsers)
                {
                    var conns = userEntry.Value;
                    if (conns != null)
                    {
                        lock (conns)
                        {
                            foreach (var connId in conns)
                            {
                                try
                                {
                                    hubContext.Groups.Remove(connId, roomId.ToString());
                                    if (_connectionRooms.TryGetValue(connId, out var rMap))
                                    {
                                        rMap.TryRemove(roomId, out _);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Trục xuất toàn bộ kết nối của một User (bao gồm tất cả các tab) khỏi SignalR Group
        /// khi người dùng rời phòng, xóa cuộc trò chuyện hoặc bị thu hồi quyền truy cập.
        /// </summary>
        public static void EvictUserFromRoom(int userId, int roomId, string reason = "Bạn không còn quyền truy cập cuộc trò chuyện này.")
        {
            if (userId <= 0 || roomId <= 0) return;

            var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();

            var connsToRemove = new List<string>();

            // Thu thập connection từ bản đồ phòng
            if (_roomUserConnections.TryGetValue(roomId, out var roomUsers))
            {
                if (roomUsers.TryRemove(userId, out var set))
                {
                    lock (set)
                    {
                        connsToRemove.AddRange(set);
                    }
                }
            }

            // Thu thập thêm mọi connection của user này (hỗ trợ nhiều tab mở cùng lúc)
            if (_userConnections.TryGetValue(userId, out var allUserConns))
            {
                lock (allUserConns)
                {
                    foreach (var c in allUserConns)
                    {
                        if (!connsToRemove.Contains(c))
                        {
                            connsToRemove.Add(c);
                        }
                    }
                }
            }

            // Loại bỏ từng connection khỏi SignalR Group trên Server
            foreach (var connId in connsToRemove)
            {
                try
                {
                    hubContext.Clients.Client(connId).onForbidden(reason);
                    hubContext.Groups.Remove(connId, roomId.ToString());
                    if (_connectionRooms.TryGetValue(connId, out var rMap))
                    {
                        rMap.TryRemove(roomId, out _);
                    }
                }
                catch { }
            }

            // Cập nhật lại số người online trong phòng
            int remainingCount = GetRoomOnlineCount(roomId);
            try
            {
                hubContext.Clients.Group(roomId.ToString()).updateRoomOnlineCount(roomId, remainingCount);
            }
            catch { }
        }

        /// <summary>
        /// Lấy danh tính người dùng đã được xác thực an toàn từ Server (Session hoặc Cookie ký điện tử MachineKey).
        /// Tuyệt đối KHÔNG tin tưởng UserID do Client tự gửi lên.
        /// </summary>
        private int? GetAuthenticatedUserId()
        {
            return UserAuthHelper.GetUserIdFromSignalR(Context);
        }

        // KẾT NỐI VÀO PHÒNG
        public async Task JoinRoom(int roomId, int clientUserId = 0)
        {
            if (roomId <= 0) return;

            int? authUserId = GetAuthenticatedUserId();
            if (!authUserId.HasValue || authUserId.Value <= 0)
            {
                // Người chưa đăng nhập bị từ chối kết nối vào phòng trò chuyện
                Clients.Caller.onUnauthorized("Vui lòng đăng nhập để tham gia cuộc trò chuyện!");
                return;
            }

            int userId = authUserId.Value; // Luôn dùng UserID đã xác thực

            // Kiểm soát quyền tham gia phòng (Tối ưu cache ngắn hạn cho phòng Public, bảo mật phòng riêng)
            bool isMember = false;
            bool isPublicVerified = false;

            // 1. Kiểm tra cache ngắn hạn chỉ áp dụng nếu phòng là phòng Public đã xác thực
            if (_publicRoomMetaCache.TryGetValue(roomId, out var cachedMeta))
            {
                if (cachedMeta.Item3 > DateTime.UtcNow && cachedMeta.Item1 == "Public" && cachedMeta.Item2 == true)
                {
                    isMember = true;
                    isPublicVerified = true;
                }
                else
                {
                    // Cache đã hết hạn hoặc không còn hợp lệ -> loại bỏ ngay lập tức
                    _publicRoomMetaCache.TryRemove(roomId, out _);
                }
            }

            // 2. Nếu chưa được xác thực qua cache phòng công cộng: BẮT BUỘC TRUY VẤN CSDL
            if (!isPublicVerified)
            {
                using (var db = new TDMUEcoSystemEntities())
                {
                    var room = db.ChatRooms.AsNoTracking().FirstOrDefault(r => r.RoomID == roomId);
                    if (room == null || room.IsActive == false)
                    {
                        _publicRoomMetaCache.TryRemove(roomId, out _);
                        Clients.Caller.onForbidden("Phòng trò chuyện không tồn tại hoặc đã bị vô hiệu hóa!");
                        return;
                    }

                    if (room.RoomType == "Public")
                    {
                        isMember = true;
                        // Lưu cache ngắn hạn 15 giây cho phòng Public để giảm tải kết nối DB
                        _publicRoomMetaCache[roomId] = Tuple.Create("Public", true, DateTime.UtcNow.AddSeconds(15));
                    }
                    else
                    {
                        // PHÒNG RIÊNG / PHÒNG NHÓM: BẮT BUỘC KIỂM TRA THÀNH VIÊN TRONG CSDL (TUYỆT ĐỐI KHÔNG BỎ QUA)
                        _publicRoomMetaCache.TryRemove(roomId, out _);
                        isMember = db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == userId);
                    }

                    if (!isMember)
                    {
                        // Người dùng không có quyền truy cập vào phòng chat riêng/nhóm này
                        Clients.Caller.onForbidden("Bạn không có quyền tham gia cuộc trò chuyện này!");
                        return;
                    }
                }
            }

            await Groups.Add(Context.ConnectionId, roomId.ToString());
            await Groups.Add(Context.ConnectionId, "user_" + userId);

            _userConnections.AddOrUpdate(
                userId,
                _ => new HashSet<string> { Context.ConnectionId },
                (_, existingSet) =>
                {
                    lock (existingSet) { existingSet.Add(Context.ConnectionId); }
                    return existingSet;
                }
            );
            _connectionToUser[Context.ConnectionId] = userId;

            // Đăng ký connection vào phòng tương ứng
            var roomUsers = _roomUserConnections.GetOrAdd(roomId, _ => new ConcurrentDictionary<int, HashSet<string>>());
            roomUsers.AddOrUpdate(
                userId,
                _ => new HashSet<string> { Context.ConnectionId },
                (_, existingSet) =>
                {
                    lock (existingSet) { existingSet.Add(Context.ConnectionId); }
                    return existingSet;
                }
            );

            var connRooms = _connectionRooms.GetOrAdd(Context.ConnectionId, _ => new ConcurrentDictionary<int, byte>());
            connRooms[roomId] = 1;

            Clients.Group(roomId.ToString()).updateUserStatus(userId, true, "Đang trực tuyến");

            // Cập nhật số người online theo thời gian thực tới tất cả thành viên trong phòng
            int currentRoomCount = GetRoomOnlineCount(roomId);
            Clients.Caller.updateRoomOnlineCount(roomId, currentRoomCount);
            Clients.Group(roomId.ToString()).updateRoomOnlineCount(roomId, currentRoomCount);
        }

        // ĐĂNG KÝ USER KHI ĐĂNG NHẬP TOÀN TRANG ĐỂ NHẬN THÔNG BÁO & LỜI MỜI CHAT
        public async Task JoinUser(int clientUserId = 0)
        {
            int? authUserId = GetAuthenticatedUserId();
            if (!authUserId.HasValue || authUserId.Value <= 0)
            {
                // Không cho phép người chưa đăng nhập hoặc giả mạo UserID nhận thông báo người khác
                return;
            }

            int userId = authUserId.Value;

            await Groups.Add(Context.ConnectionId, "user_" + userId);

            _userConnections.AddOrUpdate(
                userId,
                _ => new HashSet<string> { Context.ConnectionId },
                (_, existingSet) =>
                {
                    lock (existingSet) { existingSet.Add(Context.ConnectionId); }
                    return existingSet;
                }
            );
            _connectionToUser[Context.ConnectionId] = userId;
        }

        public void InviteToPrivateChat(int targetUserId, int fromUserId, string fromName, int roomId)
        {
            int? authUserId = GetAuthenticatedUserId();
            if (!authUserId.HasValue || authUserId.Value <= 0) return;

            int actualSenderId = authUserId.Value;
            if (targetUserId <= 0 || roomId <= 0 || targetUserId == actualSenderId) return;

            using (var db = new TDMUEcoSystemEntities())
            {
                var sender = db.Users.Find(actualSenderId);
                if (sender == null) return;
                string actualSenderName = sender.DisplayName ?? sender.Username ?? "Một bạn sinh viên";

                // Chỉ cho phép mời nếu chính người gửi là thành viên của phòng
                bool isMember = db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == actualSenderId);
                if (!isMember) return;

                Clients.Group("user_" + targetUserId)
                       .receiveChatInvitation(actualSenderId, actualSenderName, roomId, targetUserId);
            }
        }

        // XỬ LÝ KHI NGẮT KẾT NỐI (TỐI ƯU O(1) LOOKUP, LOẠI BỎ BROADCAST TOÀN TRANG)
        public override Task OnDisconnected(bool stopCalled)
        {
            if (_connectionToUser.TryRemove(Context.ConnectionId, out int userId))
            {
                // Dọn dẹp connection khỏi tất cả các phòng mà nó đã tham gia
                if (_connectionRooms.TryRemove(Context.ConnectionId, out var rooms))
                {
                    foreach (var rId in rooms.Keys)
                    {
                        RemoveConnectionFromRoom(Context.ConnectionId, userId, rId);
                    }
                }

                if (_userConnections.TryGetValue(userId, out var connections))
                {
                    bool becameOffline = false;
                    lock (connections)
                    {
                        connections.Remove(Context.ConnectionId);
                        if (connections.Count == 0)
                        {
                            _userConnections.TryRemove(userId, out _);
                            becameOffline = true;
                        }
                    }

                    if (becameOffline)
                    {
                        // Chỉ gửi trạng thái ngoại tuyến đến người dùng khác, tránh broadcast bão mạng
                        Clients.Others.updateUserStatus(userId, false, "Ngoại tuyến");
                    }
                }
            }

            return base.OnDisconnected(stopCalled);
        }

        // Hàm tiện ích hỗ trợ dọn dẹp connection khỏi phòng và broadcast số người online mới
        private static void RemoveConnectionFromRoom(string connectionId, int userId, int roomId)
        {
            if (_connectionRooms.TryGetValue(connectionId, out var rooms))
            {
                rooms.TryRemove(roomId, out _);
            }

            if (_roomUserConnections.TryGetValue(roomId, out var roomUsers))
            {
                bool userCompletelyLeftRoom = false;
                if (roomUsers.TryGetValue(userId, out var set))
                {
                    lock (set)
                    {
                        set.Remove(connectionId);
                        if (set.Count == 0)
                        {
                            roomUsers.TryRemove(userId, out _);
                            userCompletelyLeftRoom = true;
                        }
                    }
                }

                if (userCompletelyLeftRoom)
                {
                    int remainingCount = roomUsers.Count;
                    try
                    {
                        var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                        hubContext.Clients.Group(roomId.ToString()).updateRoomOnlineCount(roomId, remainingCount);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceWarning("[ChatHub] Error broadcasting room count: " + ex.Message);
                    }
                }
            }
        }

        // Kiểm tra trạng thái Online toàn trang
        public static bool IsUserOnline(int userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }

        // Lấy danh sách ConnectionId của người dùng cụ thể (quét cả User Map và Room Map để đảm bảo không sót bất kỳ connection nào)
        public static string[] GetUserConnections(int userId, int roomId = 0)
        {
            var connsSet = new HashSet<string>();
            if (_userConnections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    foreach (var c in connections) connsSet.Add(c);
                }
            }
            if (roomId > 0 && _roomUserConnections.TryGetValue(roomId, out var roomUsers))
            {
                if (roomUsers.TryGetValue(userId, out var rConns))
                {
                    lock (rConns)
                    {
                        foreach (var c in rConns) connsSet.Add(c);
                    }
                }
            }
            return connsSet.ToArray();
        }

        // Lấy số lượng người dùng duy nhất đang trực tuyến trong phòng cụ thể
        public static int GetRoomOnlineCount(int roomId)
        {
            if (_roomUserConnections.TryGetValue(roomId, out var roomUsers))
            {
                return roomUsers.Count;
            }
            return 0;
        }

        // Lấy tổng số người dùng duy nhất đang trực tuyến toàn hệ thống
        public static int GetTotalOnlineUsers()
        {
            return _userConnections.Count;
        }

        // RỜI KHỎI PHÒNG
        public async Task LeaveRoomSignalR(int roomId)
        {
            if (roomId <= 0) return;
            await Groups.Remove(Context.ConnectionId, roomId.ToString());

            if (_connectionToUser.TryGetValue(Context.ConnectionId, out int userId))
            {
                RemoveConnectionFromRoom(Context.ConnectionId, userId, roomId);
            }
        }

        public void TriggerReloadChatList(int roomId)
        {
            int? authUserId = GetAuthenticatedUserId();
            if (!authUserId.HasValue || authUserId.Value <= 0 || roomId <= 0) return;

            using (var db = new TDMUEcoSystemEntities())
            {
                var room = db.ChatRooms.Find(roomId);
                if (room == null || room.IsActive == false) return;

                bool isMember = room.RoomType == "Public" ||
                                db.ChatParticipants.Any(p => p.RoomID == roomId && p.UserID == authUserId.Value);
                if (!isMember) return;
            }

            Clients.Group(roomId.ToString()).triggerReloadChatList(roomId);
        }

        // MARKET REAL-TIME COMMENTS
        public async Task JoinMarketPost(int postId)
        {
            if (postId > 0)
            {
                await Groups.Add(Context.ConnectionId, "market_post_" + postId);
            }
        }

        public async Task LeaveMarketPost(int postId)
        {
            if (postId > 0)
            {
                await Groups.Remove(Context.ConnectionId, "market_post_" + postId);
            }
        }
    }
}
