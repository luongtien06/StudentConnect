using System;
using System.Collections.Generic;
using System.Linq;
using StudentConnect.Models;

namespace StudentConnect.Helpers
{
    public static class SocialHelper
    {
        /// <summary>
        /// Định dạng thời gian thân thiện tiếng Việt giống Facebook
        /// </summary>
        public static string FormatTimeAgo(DateTime? dt)
        {
            if (!dt.HasValue) return "";

            var span = DateTime.Now - dt.Value;

            if (span.TotalSeconds < 60)
            {
                return "Vừa xong";
            }
            if (span.TotalMinutes < 60)
            {
                return $"{(int)span.TotalMinutes} phút trước";
            }
            if (span.TotalHours < 24)
            {
                return $"{(int)span.TotalHours} giờ trước";
            }
            if (span.TotalDays < 2)
            {
                return $"Hôm qua lúc {dt.Value:HH:mm}";
            }
            if (span.TotalDays < 7)
            {
                return $"{(int)span.TotalDays} ngày trước";
            }
            if (dt.Value.Year == DateTime.Now.Year)
            {
                return $"{dt.Value.Day} tháng {dt.Value.Month} lúc {dt.Value:HH:mm}";
            }

            return $"{dt.Value:dd/MM/yyyy} lúc {dt.Value:HH:mm}";
        }

        /// <summary>
        /// Lấy danh sách ID bạn bè đã chấp nhận kết bạn
        /// </summary>
        public static List<int> GetFriendIds(int userId, TDMUEcoSystemEntities db)
        {
            var sentAccepted = db.Friendships
                .Where(f => f.RequesterID == userId && f.Status == FriendshipStatus.Accepted)
                .Select(f => f.ReceiverID);

            var receivedAccepted = db.Friendships
                .Where(f => f.ReceiverID == userId && f.Status == FriendshipStatus.Accepted)
                .Select(f => f.RequesterID);

            return sentAccepted.Concat(receivedAccepted).Distinct().ToList();
        }

        /// <summary>
        /// Lấy danh sách ID những người mà user đang theo dõi
        /// </summary>
        public static List<int> GetFollowingIds(int userId, TDMUEcoSystemEntities db)
        {
            return db.Follows
                .Where(f => f.FollowerID == userId)
                .Select(f => f.FollowingID)
                .ToList();
        }

        /// <summary>
        /// Xác định trạng thái quan hệ giữa currentUserId và targetUserId
        /// </summary>
        public static UserRelationshipStatus GetRelationshipStatus(
            int currentUserId, 
            int targetUserId, 
            TDMUEcoSystemEntities db, 
            out int? friendshipId)
        {
            friendshipId = null;

            if (currentUserId == targetUserId)
            {
                return UserRelationshipStatus.Self;
            }

            // Kiểm tra trong bảng Friendships (cả 2 chiều)
            var friendship = db.Friendships.FirstOrDefault(f =>
                (f.RequesterID == currentUserId && f.ReceiverID == targetUserId) ||
                (f.RequesterID == targetUserId && f.ReceiverID == currentUserId));

            if (friendship != null)
            {
                friendshipId = friendship.FriendshipID;

                if (friendship.Status == FriendshipStatus.Accepted)
                {
                    return UserRelationshipStatus.Friends;
                }

                if (friendship.Status == FriendshipStatus.Pending)
                {
                    if (friendship.RequesterID == currentUserId)
                    {
                        return UserRelationshipStatus.PendingSent;
                    }
                    else
                    {
                        return UserRelationshipStatus.PendingReceived;
                    }
                }
            }

            // Kiểm tra xem có đang follow không
            bool isFollowing = db.Follows.Any(f => f.FollowerID == currentUserId && f.FollowingID == targetUserId);
            if (isFollowing)
            {
                return UserRelationshipStatus.Following;
            }

            return UserRelationshipStatus.None;
        }

        /// <summary>
        /// Đếm số lượng bạn chung giữa 2 người dùng
        /// </summary>
        public static int GetMutualFriendsCount(int user1Id, int user2Id, TDMUEcoSystemEntities db)
        {
            if (user1Id == user2Id) return 0;

            var friends1 = GetFriendIds(user1Id, db);
            var friends2 = GetFriendIds(user2Id, db);

            return friends1.Intersect(friends2).Count();
        }

        /// <summary>
        /// Kiểm tra quyền xem bài viết
        /// </summary>
        public static bool CanViewPost(Post post, int currentUserId, TDMUEcoSystemEntities db, List<int> friendIds = null)
        {
            if (post == null) return false;

            // Tác giả bài viết luôn xem được bài của mình
            if (post.UserID == currentUserId) return true;

            // Chỉ mình tôi
            if (post.Privacy == PostPrivacy.OnlyMe) return false;

            // Nếu tài khoản của tác giả là riêng tư (IsPrivate = true) và không phải là bạn bè
            bool isAuthorPrivate = post.User != null && post.User.IsPrivate;

            if (post.Privacy == PostPrivacy.Friends || isAuthorPrivate)
            {
                if (friendIds == null)
                {
                    friendIds = GetFriendIds(currentUserId, db);
                }
                return friendIds.Contains(post.UserID);
            }

            // Mặc định công khai
            return true;
        }

        /// <summary>
        /// Tạo danh sách tóm tắt các reaction hàng đầu của một bài viết
        /// </summary>
        public static List<ReactionSummaryItem> GetTopReactions(ICollection<PostLike> likes)
        {
            if (likes == null || !likes.Any()) return new List<ReactionSummaryItem>();

            return likes
                .GroupBy(l => l.LikeType)
                .Select(g =>
                {
                    var meta = ReactionType.GetMeta(g.Key);
                    return new ReactionSummaryItem
                    {
                        LikeType = g.Key,
                        Count = g.Count(),
                        Emoji = meta.Emoji,
                        IconClass = meta.IconClass,
                        Color = meta.Color
                    };
                })
                .OrderByDescending(r => r.Count)
                .Take(3)
                .ToList();
        }

        /// <summary>
        /// Chuyển đổi một thực thể Post sang PostItemViewModel
        /// </summary>
        public static PostItemViewModel MapToPostViewModel(Post post, int currentUserId)
        {
            if (post == null) return null;

            var author = post.User;
            var authorProfile = author?.UserProfiles?.FirstOrDefault();

            var currentLike = post.PostLikes?.FirstOrDefault(l => l.UserID == currentUserId);

            var vm = new PostItemViewModel
            {
                PostID = post.PostID,
                UserID = post.UserID,
                AuthorUsername = author?.DisplayName ?? "Người dùng",
                AuthorAvatar = author?.SafeAvatar ?? "/Content/Images/default-avatar.png",
                IsTDMUStudent = author?.IsTDMUStudent ?? false,
                Faculty = authorProfile?.Faculty ?? "",
                Major = authorProfile?.Major ?? "",
                StudentYear = authorProfile?.StudentYear,

                Content = post.Content,
                Privacy = post.Privacy,
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                TimeAgo = FormatTimeAgo(post.CreatedAt),

                MediaList = post.PostMedias?
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new PostMediaItemViewModel
                    {
                        MediaID = m.MediaID,
                        PostID = m.PostID,
                        MediaUrl = m.MediaUrl,
                        MediaType = m.MediaType,
                        SortOrder = m.SortOrder
                    }).ToList() ?? new List<PostMediaItemViewModel>(),

                LikeCount = post.PostLikes?.Count ?? 0,
                CommentCount = post.PostComments?.Count ?? 0,
                ShareCount = post.PostShares?.Count ?? 0,

                CurrentUserReaction = currentLike?.LikeType,
                TopReactions = GetTopReactions(post.PostLikes),

                IsOwner = post.UserID == currentUserId,
                CanEdit = post.UserID == currentUserId,
                // Only the post owner can delete (admins treated same as students)
                CanDelete = post.UserID == currentUserId
            };

            // Lấy 3 bình luận mới nhất (cấp gốc ParentID == null)
            if (post.PostComments != null)
            {
                vm.Comments = post.PostComments
                    .Where(c => c.ParentID == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(3)
                    .Select(c => MapToCommentViewModel(c, currentUserId))
                    .OrderBy(c => c.CreatedAt) // Sắp xếp lại tăng dần để hiển thị
                    .ToList();
            }

            return vm;
        }

        /// <summary>
        /// Chuyển đổi một thực thể PostComment sang PostCommentItemViewModel
        /// </summary>
        public static PostCommentItemViewModel MapToCommentViewModel(PostComment comment, int currentUserId)
        {
            if (comment == null) return null;

            var author = comment.User;

            var vm = new PostCommentItemViewModel
            {
                CommentID = comment.CommentID,
                PostID = comment.PostID,
                UserID = comment.UserID,
                AuthorUsername = author?.DisplayName ?? "Người dùng",
                AuthorAvatar = author?.SafeAvatar ?? "/Content/Images/default-avatar.png",
                IsTDMUStudent = author?.IsTDMUStudent ?? false,

                Content = comment.Content,
                ImageUrl = comment.ImageUrl,
                ParentID = comment.ParentID,
                CreatedAt = comment.CreatedAt,
                TimeAgo = FormatTimeAgo(comment.CreatedAt),

                IsOwner = comment.UserID == currentUserId,
                CanDelete = comment.UserID == currentUserId
            };

            return vm;
        }

        /// <summary>
        /// Chuyển đổi một thực thể PostShare sang PostItemViewModel dạng bài viết được chia sẻ
        /// </summary>
        public static PostItemViewModel MapShareToPostViewModel(PostShare share, int currentUserId)
        {
            if (share == null || share.Post == null) return null;

            var userWhoShared = share.User;
            var userWhoSharedProfile = userWhoShared?.UserProfiles?.FirstOrDefault();
            var originalPost = share.Post;
            var originalAuthor = originalPost.User;

            var originalLike = originalPost.PostLikes?.FirstOrDefault(l => l.UserID == currentUserId);

            var vm = new PostItemViewModel
            {
                PostID = originalPost.PostID,
                UserID = share.UserID,
                AuthorUsername = userWhoShared?.DisplayName ?? "Người dùng",
                AuthorAvatar = userWhoShared?.SafeAvatar ?? "/Content/Images/default-avatar.png",
                IsTDMUStudent = userWhoShared?.IsTDMUStudent ?? false,
                Faculty = userWhoSharedProfile?.Faculty ?? "",
                Major = userWhoSharedProfile?.Major ?? "",
                StudentYear = userWhoSharedProfile?.StudentYear,

                Content = share.Content, // Lời bình của người chia sẻ
                Privacy = originalPost.Privacy,
                CreatedAt = share.CreatedAt,
                UpdatedAt = share.CreatedAt,
                TimeAgo = FormatTimeAgo(share.CreatedAt),

                // Thông tin bài viết gốc được lồng bên trong
                SharedPost = new PostShareInfoViewModel
                {
                    ShareID = share.ShareID,
                    OriginalPostID = originalPost.PostID,
                    OriginalAuthorID = originalPost.UserID,
                    OriginalAuthorName = originalAuthor?.DisplayName ?? "Người dùng",
                    OriginalAuthorAvatar = originalAuthor?.SafeAvatar ?? "/Content/Images/default-avatar.png",
                    OriginalContent = originalPost.Content,
                    OriginalCreatedAt = originalPost.CreatedAt,
                    OriginalTimeAgo = FormatTimeAgo(originalPost.CreatedAt),
                    OriginalMediaList = originalPost.PostMedias?
                        .OrderBy(m => m.SortOrder)
                        .Select(m => new PostMediaItemViewModel
                        {
                            MediaID = m.MediaID,
                            PostID = m.PostID,
                            MediaUrl = m.MediaUrl,
                            MediaType = m.MediaType,
                            SortOrder = m.SortOrder
                        }).ToList() ?? new List<PostMediaItemViewModel>()
                },

                LikeCount = originalPost.PostLikes?.Count ?? 0,
                CommentCount = originalPost.PostComments?.Count ?? 0,
                ShareCount = originalPost.PostShares?.Count ?? 0,

                CurrentUserReaction = originalLike?.LikeType,
                TopReactions = GetTopReactions(originalPost.PostLikes),

                IsOwner = share.UserID == currentUserId,
                CanEdit = false,
                // Only the sharer (owner of this share entry) can delete the share
                CanDelete = share.UserID == currentUserId
            };

            // Lấy bình luận từ bài viết gốc
            if (originalPost.PostComments != null)
            {
                vm.Comments = originalPost.PostComments
                    .Where(c => c.ParentID == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(3)
                    .Select(c => MapToCommentViewModel(c, currentUserId))
                    .OrderBy(c => c.CreatedAt)
                    .ToList();
            }

            return vm;
        }
    }
}
