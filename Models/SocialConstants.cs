using System;
using System.Collections.Generic;

namespace StudentConnect.Models
{
    /// <summary>
    /// Các mức quyền riêng tư của bài viết mạng xã hội
    /// </summary>
    public static class PostPrivacy
    {
        public const string Public = "Public";       // Công khai - tất cả mọi người đều xem được
        public const string Friends = "Friends";     // Bạn bè - chỉ bạn bè mới xem được
        public const string OnlyMe = "OnlyMe";       // Chỉ mình tôi - chỉ tác giả mới xem được

        public static string GetLabel(string privacy)
        {
            switch (privacy)
            {
                case Friends: return "Bạn bè";
                case OnlyMe: return "Chỉ mình tôi";
                case Public:
                default: return "Công khai";
            }
        }

        public static string GetIcon(string privacy)
        {
            switch (privacy)
            {
                case Friends: return "fa-solid fa-user-group";
                case OnlyMe: return "fa-solid fa-lock";
                case Public:
                default: return "fa-solid fa-earth-americas";
            }
        }
    }

    /// <summary>
    /// Các loại cảm xúc / reaction bài viết giống Facebook
    /// </summary>
    public static class ReactionType
    {
        public const string Like = "Like";
        public const string Love = "Love";
        public const string Haha = "Haha";
        public const string Wow = "Wow";
        public const string Sad = "Sad";
        public const string Angry = "Angry";

        public static readonly Dictionary<string, ReactionMeta> All = new Dictionary<string, ReactionMeta>(StringComparer.OrdinalIgnoreCase)
        {
            { Like, new ReactionMeta { Type = Like, Label = "Thích", Emoji = "👍", Color = "#1877F2", IconClass = "fa-solid fa-thumbs-up" } },
            { Love, new ReactionMeta { Type = Love, Label = "Yêu thích", Emoji = "❤️", Color = "#F33E5B", IconClass = "fa-solid fa-heart" } },
            { Haha, new ReactionMeta { Type = Haha, Label = "Haha", Emoji = "😆", Color = "#F7B125", IconClass = "fa-solid fa-face-laugh-squint" } },
            { Wow, new ReactionMeta { Type = Wow, Label = "Wow", Emoji = "😮", Color = "#F7B125", IconClass = "fa-solid fa-face-surprise" } },
            { Sad, new ReactionMeta { Type = Sad, Label = "Buồn", Emoji = "😢", Color = "#F7B125", IconClass = "fa-solid fa-face-sad-tear" } },
            { Angry, new ReactionMeta { Type = Angry, Label = "Phẫn nộ", Emoji = "😡", Color = "#E25141", IconClass = "fa-solid fa-face-angry" } }
        };

        public static bool IsValid(string type) => !string.IsNullOrEmpty(type) && All.ContainsKey(type);

        public static ReactionMeta GetMeta(string type)
        {
            if (string.IsNullOrEmpty(type)) return All[Like];
            ReactionMeta meta;
            return All.TryGetValue(type, out meta) ? meta : All[Like];
        }
    }

    public class ReactionMeta
    {
        public string Type { get; set; }
        public string Label { get; set; }
        public string Emoji { get; set; }
        public string Color { get; set; }
        public string IconClass { get; set; }
    }

    /// <summary>
    /// Trạng thái quan hệ bạn bè
    /// </summary>
    public static class FriendshipStatus
    {
        public const string Pending = "Pending";     // Đang chờ chấp nhận
        public const string Accepted = "Accepted";   // Đã là bạn bè
        public const string Declined = "Declined";   // Đã từ chối
        public const string Blocked = "Blocked";     // Đã chặn
    }

    /// <summary>
    /// Mối quan hệ giữa người dùng hiện tại và người dùng khác
    /// </summary>
    public enum UserRelationshipStatus
    {
        Self,            // Là chính mình
        Friends,         // Đã là bạn bè
        PendingSent,     // Mình đã gửi lời mời kết bạn (đang chờ họ duyệt)
        PendingReceived, // Họ đã gửi lời mời kết bạn (mình có nút Chấp nhận/Từ chối)
        Following,       // Đang theo dõi (chưa kết bạn)
        None             // Chưa có mối quan hệ gì (nút Thêm bạn bè)
    }

    /// <summary>
    /// Loại media đính kèm bài viết
    /// </summary>
    public static class PostMediaType
    {
        public const string Image = "image";
        public const string Video = "video";
    }

    /// <summary>
    /// Loại đối tượng tham chiếu trong thông báo (RefType)
    /// </summary>
    public static class SocialRefType
    {
        public const string Post = "post";
        public const string Comment = "comment";
        public const string Story = "story";
        public const string FriendRequest = "friend_request";
        public const string FriendAccept = "friend_accept";
        public const string Like = "like";
        public const string Share = "share";
        public const string Follow = "follow";
    }
}
