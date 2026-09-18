using Microsoft.AspNet.SignalR;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StudentConnect.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<int, HashSet<string>> _userConnections
            = new ConcurrentDictionary<int, HashSet<string>>();

        // KẾT NỐI VÀO PHÒNG
        public async Task JoinRoom(int roomId, int userId)
        {
            if (roomId <= 0 || userId <= 0) return;

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

            Clients.Group(roomId.ToString()).updateUserStatus(userId, true, "Đang trực tuyến");
        }

        // ĐĂNG KÝ USER KHI ĐĂNG NHẬP TOÀN TRANG ĐỂ NHẬN THÔNG BÁO & LỜI MỜI CHAT
        public async Task JoinUser(int userId)
        {
            if (userId <= 0) return;

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
        }

        public void InviteToPrivateChat(int targetUserId, int fromUserId, string fromName, int roomId)
        {
            // Tránh gửi nhầm hoặc tự mời mình
            if (targetUserId <= 0 || roomId <= 0 || targetUserId == fromUserId) return;

            Clients.Group("user_" + targetUserId)
                   .receiveChatInvitation(fromUserId, fromName, roomId, targetUserId);
        }

        // XỬ LÝ KHI NGẮT KẾT NỐI
        public override Task OnDisconnected(bool stopCalled)
        {
            int disconnectedUserId = 0;

            foreach (var kvp in _userConnections)
            {
                lock (kvp.Value)
                {
                    if (kvp.Value.Contains(Context.ConnectionId))
                    {
                        kvp.Value.Remove(Context.ConnectionId);
                        disconnectedUserId = kvp.Key;
                        if (kvp.Value.Count == 0)
                        {
                            _userConnections.TryRemove(kvp.Key, out _);
                            Clients.All.updateUserStatus(kvp.Key, false, "Ngoại tuyến");
                        }
                        break;
                    }
                }
            }

            return base.OnDisconnected(stopCalled);
        }

        //  Kiểm tra trạng thái Online
        public static bool IsUserOnline(int userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }

        //RỜI KHỎI PHÒNG
        public async Task LeaveRoomSignalR(int roomId)
        {
            await Groups.Remove(Context.ConnectionId, roomId.ToString());
        }

        public void TriggerReloadChatList(int roomId)
        {
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