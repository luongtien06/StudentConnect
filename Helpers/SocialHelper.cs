using StudentConnect.Helpers;
using System.Data.Entity;
using System.Globalization;
using System.Text;

﻿using System;

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
            if (userId <= 0 || db == null) return new List<int>();

            return db.Friendships
                .AsNoTracking()
                .Where(f => (f.RequesterID == userId || f.ReceiverID == userId) && f.Status == FriendshipStatus.Accepted)
                .Select(f => f.RequesterID == userId ? f.ReceiverID : f.RequesterID)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Gom nhóm truy vấn quan hệ bạn bè (Bạn bè, Đang chờ, Bị chặn) trong 1 lần truy vấn SQL duy nhất
        /// </summary>
        public static void GetFriendshipOverview(int userId, TDMUEcoSystemEntities db,
            out List<int> friendIds, out List<int> pendingIds, out List<int> blockedIds)
        {
            friendIds = new List<int>();
            pendingIds = new List<int>();
            blockedIds = new List<int>();

            if (userId <= 0 || db == null) return;

            var myFriendships = db.Friendships
                .AsNoTracking()
                .Where(f => f.RequesterID == userId || f.ReceiverID == userId)
                .Select(f => new { f.RequesterID, f.ReceiverID, f.Status })
                .ToList();

            var friendSet = new HashSet<int>();
            var pendingSet = new HashSet<int>();
            var blockedSet = new HashSet<int>();

            foreach (var f in myFriendships)
            {
                int otherId = (f.RequesterID == userId) ? f.ReceiverID : f.RequesterID;
                if (f.Status == FriendshipStatus.Accepted)
                {
                    friendSet.Add(otherId);
                }
                else if (f.Status == FriendshipStatus.Pending)
                {
                    pendingSet.Add(otherId);
                }
                else if (f.Status == FriendshipStatus.Blocked)
                {
                    blockedSet.Add(otherId);
                }
            }

            friendIds = friendSet.ToList();
            pendingIds = pendingSet.ToList();
            blockedIds = blockedSet.ToList();
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

                AuthorAvatar = author?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

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

                CanDelete = post.UserID == currentUserId,

                CanModerateComments = post.UserID == currentUserId

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

                AuthorAvatar = author?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

                IsTDMUStudent = author?.IsTDMUStudent ?? false,



                Content = comment.Content,

                ImageUrl = comment.ImageUrl,

                ParentID = comment.ParentID,

                CreatedAt = comment.CreatedAt,

                TimeAgo = FormatTimeAgo(comment.CreatedAt),



                IsOwner = comment.UserID == currentUserId,

                CanDelete = comment.UserID == currentUserId,

                Replies = comment.PostComments1?

                    .OrderBy(c => c.CreatedAt)

                    .Select(c => MapToCommentViewModel(c, currentUserId))

                    .ToList() ?? new List<PostCommentItemViewModel>()

            };



            return vm;

        }



        /// <summary>

        /// Chuyển đổi một thực thể PostShare sang PostItemViewModel dạng bài viết được chia sẻ

        /// </summary>

        public static PostItemViewModel MapShareToPostViewModel(PostShare share, int currentUserId, int? shareCount = null)

        {

            if (share == null) return null;



            var userWhoShared = share.User;

            var userWhoSharedProfile = userWhoShared?.UserProfiles?.FirstOrDefault();

            var originalPost = share.Post;



            if (originalPost == null)

            {

                return new PostItemViewModel

                {

                    PostID = -(2000000 + share.ShareID),

                    UserID = share.UserID,

                    AuthorUsername = userWhoShared?.DisplayName ?? userWhoShared?.Username ?? "Người dùng",

                    AuthorAvatar = userWhoShared?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

                    IsTDMUStudent = userWhoShared?.IsTDMUStudent ?? false,

                    Faculty = userWhoSharedProfile?.Faculty ?? "",

                    Major = userWhoSharedProfile?.Major ?? "",

                    StudentYear = userWhoSharedProfile?.StudentYear,

                    Content = share.Content,

                    Privacy = PostPrivacy.Public,

                    CreatedAt = share.CreatedAt,

                    UpdatedAt = share.CreatedAt,

                    TimeAgo = FormatTimeAgo(share.CreatedAt),

                    SharedPost = new PostShareInfoViewModel

                    {

                        ShareID = share.ShareID,

                        ShareType = "Post",

                        OriginalPostID = share.PostID,

                        IsOriginalDeleted = true,

                        UnavailableReason = "Bài viết gốc đã bị xóa hoặc không còn tồn tại.",

                        OriginalBadge = "Bài viết"

                    },

                    LikeCount = 0,

                    CommentCount = 0,

                    ShareCount = 0,

                    IsOwner = share.UserID == currentUserId,

                    CanEdit = false,

                    CanDelete = share.UserID == currentUserId,

                    CanModerateComments = false,

                    Comments = new List<PostCommentItemViewModel>()

                };

            }



            var originalAuthor = originalPost.User;

            var originalLike = originalPost.PostLikes?.FirstOrDefault(l => l.UserID == currentUserId);



            var vm = new PostItemViewModel

            {

                PostID = originalPost.PostID,

                UserID = share.UserID,

                AuthorUsername = userWhoShared?.DisplayName ?? userWhoShared?.Username ?? "Người dùng",

                AuthorAvatar = userWhoShared?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

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

                    ShareType = "Post",

                    OriginalPostID = originalPost.PostID,

                    OriginalAuthorID = originalPost.UserID,

                    OriginalAuthorName = originalAuthor?.DisplayName ?? originalAuthor?.Username ?? "Người dùng",

                    OriginalAuthorAvatar = originalAuthor?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

                    OriginalContent = originalPost.Content,

                    OriginalTitle = !string.IsNullOrWhiteSpace(originalPost.Content) && originalPost.Content.Length > 60

                        ? originalPost.Content.Substring(0, 57) + "..."

                        : originalPost.Content,

                    OriginalUrl = "/User/Profile/" + originalPost.UserID + "#post-" + originalPost.PostID,

                    OriginalBadge = "Bài viết",

                    OriginalSubtitle = FormatTimeAgo(originalPost.CreatedAt),

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

                ShareCount = shareCount ?? 0,



                CurrentUserReaction = originalLike?.LikeType,

                TopReactions = GetTopReactions(originalPost.PostLikes),



                IsOwner = share.UserID == currentUserId,

                CanModerateComments = originalPost.UserID == currentUserId,

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



        public static void ApplyCommentModeration(IEnumerable<PostItemViewModel> posts, TDMUEcoSystemEntities db, int currentUserId, bool? isCurrentUserAdmin = null)
        {
            var postList = posts?.ToList() ?? new List<PostItemViewModel>();
            bool isAdmin = isCurrentUserAdmin.HasValue
                ? isCurrentUserAdmin.Value
                : (currentUserId > 0 && db.Users.Any(u => u.UserID == currentUserId && u.IsAdmin == true));

            foreach (var post in postList)
            {
                int originalAuthorId = post.SharedPost != null ? post.SharedPost.OriginalAuthorID : post.UserID;
                post.CanModerateComments = currentUserId > 0 && originalAuthorId == currentUserId;
                foreach (var comment in post.Comments)
                {
                    ApplyCommentPermissions(comment, post.CanModerateComments, isAdmin);
                }
            }
        }



        private static void ApplyCommentPermissions(PostCommentItemViewModel comment, bool canModerate, bool isAdmin)

        {

            comment.CanDelete = comment.IsOwner || canModerate || isAdmin;

            foreach (var reply in comment.Replies)

                ApplyCommentPermissions(reply, canModerate, isAdmin);

        }

    

        /// <summary>

        /// Chuyển đổi một SharedPost (chia sẻ từ Chợ, Kết nối hoặc Bài viết) sang PostItemViewModel

        /// </summary>

        public static PostItemViewModel MapSharedPostToPostViewModel(SharedPost share, int currentUserId, TDMUEcoSystemEntities db)

        {

            if (share == null || db == null) return null;



            var userWhoShared = share.User;

            var userWhoSharedProfile = userWhoShared?.UserProfiles?.FirstOrDefault();

            string normalizedType = (share.ItemType ?? "").Trim().ToLower();



            PostShareInfoViewModel shareInfo = null;



            if (normalizedType == "market")

            {

                var market = db.MarketPosts

                    .Include(m => m.User)

                    .Include(m => m.MarketCategory)

                    .Include(m => m.MarketImages)

                    .FirstOrDefault(m => m.PostID == share.PostID);



                if (market == null || market.IsHide == true)

                {

                    shareInfo = new PostShareInfoViewModel

                    {

                        ShareID = share.ShareID,

                        ShareType = "Market",

                        OriginalPostID = share.PostID,

                        IsOriginalDeleted = true,

                        UnavailableReason = "Sản phẩm trong Chợ sinh viên đã bị xóa hoặc đã bị ẩn.",

                        OriginalBadge = "Chợ sinh viên"

                    };

                }

                else

                {

                    string firstImage = market.MarketImages != null && market.MarketImages.Any()

                        ? market.MarketImages.First().ImageUrl

                        : market.MainImage;



                    shareInfo = new PostShareInfoViewModel

                    {

                        ShareID = share.ShareID,

                        ShareType = "Market",

                        OriginalPostID = market.PostID,

                        OriginalAuthorID = market.UserID ?? 0,

                        OriginalAuthorName = market.User?.DisplayName ?? market.User?.Username ?? "Người bán",

                        OriginalAuthorAvatar = market.User?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

                        OriginalContent = market.Description,

                        OriginalTitle = market.Title,

                        OriginalUrl = "/Market/Details/" + market.PostID,

                        OriginalSubtitle = (market.Price ?? 0).ToString("N0") + " VNĐ",

                        OriginalBadge = market.MarketCategory?.CategoryName ?? "Chợ sinh viên",

                        OriginalCreatedAt = market.CreatedAt,

                        OriginalTimeAgo = FormatTimeAgo(market.CreatedAt),

                        OriginalMediaList = !string.IsNullOrEmpty(firstImage)

                            ? new List<PostMediaItemViewModel> { new PostMediaItemViewModel { MediaUrl = firstImage, MediaType = "image" } }

                            : new List<PostMediaItemViewModel>()

                    };

                }

            }

            else if (normalizedType == "connect")

            {

                var connect = db.ConnectPosts

                    .Include(c => c.User)

                    .Include(c => c.ConnectCategory)

                    .FirstOrDefault(c => c.PostID == share.PostID);



                if (connect == null)

                {

                    shareInfo = new PostShareInfoViewModel

                    {

                        ShareID = share.ShareID,

                        ShareType = "Connect",

                        OriginalPostID = share.PostID,

                        IsOriginalDeleted = true,

                        UnavailableReason = "Bài viết khám phá Kết nối đã bị xóa hoặc không còn tồn tại.",

                        OriginalBadge = "Kết nối"

                    };

                }

                else

                {

                    string firstImage = ConnectImageHelper.GetFirstPath(connect.MainImage);



                    shareInfo = new PostShareInfoViewModel

                    {

                        ShareID = share.ShareID,

                        ShareType = "Connect",

                        OriginalPostID = connect.PostID,

                        OriginalAuthorID = connect.UserID ?? 0,

                        OriginalAuthorName = connect.User?.DisplayName ?? connect.User?.Username ?? "Tác giả",

                        OriginalAuthorAvatar = connect.User?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

                        OriginalContent = connect.Description,

                        OriginalTitle = connect.Title,

                        OriginalUrl = "/Connect/Details/" + connect.PostID,

                        OriginalSubtitle = connect.ConnectCategory?.CategoryName ?? "Kết nối",

                        OriginalBadge = connect.ConnectCategory?.CategoryName ?? "Kết nối",

                        OriginalCreatedAt = connect.CreatedAt,

                        OriginalTimeAgo = FormatTimeAgo(connect.CreatedAt),

                        OriginalMediaList = !string.IsNullOrEmpty(firstImage)

                            ? new List<PostMediaItemViewModel> { new PostMediaItemViewModel { MediaUrl = firstImage, MediaType = "image" } }

                            : new List<PostMediaItemViewModel>()

                    };

                }

            }

            else if (normalizedType == "post" || normalizedType == "feed" || normalizedType == "social")

            {

                var post = db.Posts

                    .Include(p => p.User)

                    .Include(p => p.PostMedias)

                    .FirstOrDefault(p => p.PostID == share.PostID);



                if (post == null)

                {

                    shareInfo = new PostShareInfoViewModel

                    {

                        ShareID = share.ShareID,

                        ShareType = "Post",

                        OriginalPostID = share.PostID,

                        IsOriginalDeleted = true,

                        UnavailableReason = "Bài viết gốc đã bị xóa hoặc không còn tồn tại.",

                        OriginalBadge = "Bài viết"

                    };

                }

                else

                {

                    shareInfo = new PostShareInfoViewModel

                    {

                        ShareID = share.ShareID,

                        ShareType = "Post",

                        OriginalPostID = post.PostID,

                        OriginalAuthorID = post.UserID,

                        OriginalAuthorName = post.User?.DisplayName ?? post.User?.Username ?? "Người dùng",

                        OriginalAuthorAvatar = post.User?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

                        OriginalContent = post.Content,

                        OriginalTitle = !string.IsNullOrWhiteSpace(post.Content) && post.Content.Length > 60

                            ? post.Content.Substring(0, 57) + "..."

                            : post.Content,

                        OriginalUrl = "/User/Profile/" + post.UserID + "#post-" + post.PostID,

                        OriginalBadge = "Bài viết",

                        OriginalSubtitle = FormatTimeAgo(post.CreatedAt),

                        OriginalCreatedAt = post.CreatedAt,

                        OriginalTimeAgo = FormatTimeAgo(post.CreatedAt),

                        OriginalMediaList = post.PostMedias?

                            .OrderBy(m => m.SortOrder)

                            .Select(m => new PostMediaItemViewModel

                            {

                                MediaID = m.MediaID,

                                PostID = m.PostID,

                                MediaUrl = m.MediaUrl,

                                MediaType = m.MediaType,

                                SortOrder = m.SortOrder

                            }).ToList() ?? new List<PostMediaItemViewModel>()

                    };

                }

            }

            else

            {

                return null;

            }



            return new PostItemViewModel

            {

                PostID = -(1000000 + share.ShareID),

                UserID = share.UserID,

                AuthorUsername = userWhoShared?.DisplayName ?? userWhoShared?.Username ?? "Người dùng",

                AuthorAvatar = userWhoShared?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,

                IsTDMUStudent = userWhoShared?.IsTDMUStudent ?? false,

                Faculty = userWhoSharedProfile?.Faculty ?? "",

                Major = userWhoSharedProfile?.Major ?? "",

                StudentYear = userWhoSharedProfile?.StudentYear,

                Content = share.Content,

                Privacy = PostPrivacy.Public,

                CreatedAt = share.CreatedAt,

                UpdatedAt = share.CreatedAt,

                TimeAgo = FormatTimeAgo(share.CreatedAt),

                SharedPost = shareInfo,

                LikeCount = 0,

                CommentCount = 0,

                ShareCount = 0,

                IsOwner = share.UserID == currentUserId,

                CanEdit = false,

                CanDelete = share.UserID == currentUserId,

                CanModerateComments = false,

                Comments = new List<PostCommentItemViewModel>()

            };

        }



        /// <summary>

        /// Lấy danh sách gợi ý bạn bè thông minh ("Những người bạn có thể biết")

        /// Ưu tiên: Bạn chung -> Cùng ngành -> Cùng khoa -> Đang theo dõi -> Sinh viên khác

        /// Không có N+1 query, loại trừ bản thân, bạn bè hiện có và các lời mời đang chờ.

        /// </summary>

        public static List<FriendSuggestionItemViewModel> GetFriendSuggestions(
            int currentUserId,
            TDMUEcoSystemEntities db,
            int take = 10,
            int skip = 0,
            List<int> cachedFriendIds = null,
            List<int> cachedPendingIds = null,
            List<int> cachedBlockedIds = null,
            string myFaculty = null,
            string myMajor = null)
        {
            var excludedIds = new HashSet<int>();
            var myFriendIds = new List<int>();

            if (currentUserId > 0)
            {
                excludedIds.Add(currentUserId);

                List<int> friendIds;
                List<int> pendingIds;
                List<int> blockedIds;

                if (cachedFriendIds != null && cachedPendingIds != null && cachedBlockedIds != null)
                {
                    friendIds = cachedFriendIds;
                    pendingIds = cachedPendingIds;
                    blockedIds = cachedBlockedIds;
                }
                else
                {
                    GetFriendshipOverview(currentUserId, db, out friendIds, out pendingIds, out blockedIds);
                }

                myFriendIds = friendIds;
                foreach (var fid in friendIds) excludedIds.Add(fid);
                foreach (var pid in pendingIds) excludedIds.Add(pid);
                foreach (var bid in blockedIds) excludedIds.Add(bid);
            }

            var mutualCounts = new Dictionary<int, int>();

            if (currentUserId > 0)
            {
                if (myFaculty == null && myMajor == null)
                {
                    var myProfile = db.UserProfiles.AsNoTracking().FirstOrDefault(p => p.UserID == currentUserId);
                    myFaculty = myProfile?.Faculty?.Trim();
                    myMajor = myProfile?.Major?.Trim();
                }

                // 1. Bạn chung (Friends of friends)
                if (myFriendIds.Any())
                {
                    var friendFriendships = db.Friendships
                        .AsNoTracking()
                        .Where(f => f.Status == FriendshipStatus.Accepted &&
                            (myFriendIds.Contains(f.RequesterID) || myFriendIds.Contains(f.ReceiverID)))
                        .Select(f => new { f.RequesterID, f.ReceiverID })
                        .ToList();

                    foreach (var f in friendFriendships)
                    {
                        int otherId = myFriendIds.Contains(f.RequesterID) ? f.ReceiverID : f.RequesterID;
                        if (!excludedIds.Contains(otherId))
                        {
                            if (!mutualCounts.ContainsKey(otherId)) mutualCounts[otherId] = 0;
                            mutualCounts[otherId]++;
                        }
                    }
                }
            }

            // 2. Cùng khoa / cùng ngành

            var profileCandidates = new List<int>();

            if (!string.IsNullOrEmpty(myFaculty) || !string.IsNullOrEmpty(myMajor))

            {

                profileCandidates = db.UserProfiles

                    .AsNoTracking()

                    .Where(p => !excludedIds.Contains(p.UserID) &&

                        ((myFaculty != null && myFaculty != "" && p.Faculty == myFaculty) ||

                         (myMajor != null && myMajor != "" && p.Major == myMajor)))

                    .Select(p => p.UserID)

                    .Take(50)

                    .ToList();

            }



            // 3. Người đang theo dõi (chưa kết bạn)

            var followingIds = currentUserId > 0

                ? db.Follows

                    .AsNoTracking()

                    .Where(f => f.FollowerID == currentUserId && !excludedIds.Contains(f.FollowingID))

                    .Select(f => f.FollowingID)

                    .ToList()

                : new List<int>();



            // Tập hợp ứng viên

            var candidatePool = new HashSet<int>(mutualCounts.Keys);

            foreach (var uid in profileCandidates) candidatePool.Add(uid);

            foreach (var uid in followingIds) candidatePool.Add(uid);



            if (candidatePool.Count < 20)

            {

                var extraUsers = db.Users

                    .AsNoTracking()

                    .Where(u => !excludedIds.Contains(u.UserID))

                    .OrderByDescending(u => u.CreatedAt)

                    .Select(u => u.UserID)

                    .Take(30)

                    .ToList();

                foreach (var uid in extraUsers) candidatePool.Add(uid);

            }



            var candidateIds = candidatePool.ToList();

            if (!candidateIds.Any()) return new List<FriendSuggestionItemViewModel>();



            // Lấy thông tin User & Profile 1 lần duy nhất (không N+1)

            var candidates = db.Users

                .AsNoTracking()

                .Include(u => u.UserProfiles)

                .Where(u => candidateIds.Contains(u.UserID))

                .ToList();



            var scored = candidates.Select(u =>

            {

                var prof = u.UserProfiles?.FirstOrDefault();

                int mutualCount = mutualCounts.ContainsKey(u.UserID) ? mutualCounts[u.UserID] : 0;

                bool isSameMajor = (!string.IsNullOrEmpty(myMajor) && prof != null && prof.Major == myMajor);

                bool isSameFaculty = (!string.IsNullOrEmpty(myFaculty) && prof != null && prof.Faculty == myFaculty);

                bool isFollowing = followingIds.Contains(u.UserID);



                int score = (mutualCount * 25) + (isSameMajor ? 12 : 0) + (isSameFaculty ? 6 : 0) + (isFollowing ? 10 : 0);



                string reason;

                if (mutualCount > 0)

                    reason = $"{mutualCount} bạn chung";

                else if (isSameMajor && !string.IsNullOrEmpty(prof.Major))

                    reason = $"Cùng ngành {prof.Major}";

                else if (isSameFaculty && !string.IsNullOrEmpty(prof.Faculty))

                    reason = $"Cùng khoa {prof.Faculty}";

                else if (isFollowing)

                    reason = "Đang theo dõi";

                else if (prof != null && !string.IsNullOrEmpty(prof.Faculty))

                    reason = prof.Faculty;

                else

                    reason = "Sinh viên TDMU";



                return new

                {

                    User = u,

                    Profile = prof,

                    MutualCount = mutualCount,

                    IsFollowing = isFollowing,

                    Score = score,

                    Reason = reason

                };

            })

            .OrderByDescending(x => x.Score)

            .ThenByDescending(x => x.User.UserID)

            .Skip(skip)

            .Take(take)

            .ToList();



            return scored.Select(x => new FriendSuggestionItemViewModel

            {

                UserID = x.User.UserID,

                Username = x.User.Username,

                DisplayName = x.User.DisplayName,

                Avatar = x.User.SafeAvatar,

                Faculty = x.Profile?.Faculty ?? "",

                Major = x.Profile?.Major ?? "",

                StudentYear = x.Profile?.StudentYear,

                Bio = x.Profile?.Bio ?? "",

                MutualFriendsCount = x.MutualCount,

                RelationshipStatus = x.IsFollowing ? "Following" : "None",

                IsFollowing = x.IsFollowing,

                Reason = x.Reason

            }).ToList();

        }



        /// <summary>
        /// Chuẩn hóa chuỗi tiếng Việt: loại bỏ dấu thanh, dấu mũ, chuyển đ/Đ thành d/D để tìm kiếm không dấu
        /// </summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(c);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D').Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Tìm kiếm người dùng theo tên hiển thị, username, email hoặc khoa/ngành.
        /// Hỗ trợ cả tiếng Việt có dấu và không dấu (diacritic-agnostic), chống SQL Injection, không N+1 query.
        /// </summary>
        public static Tuple<List<FriendSuggestionItemViewModel>, int> SearchUsers(
            int currentUserId, 
            string query, 
            TDMUEcoSystemEntities db, 
            int page = 1, 
            int pageSize = 8)
        {
            string clean = (query ?? "").Trim();
            if (string.IsNullOrEmpty(clean))
            {
                return new Tuple<List<FriendSuggestionItemViewModel>, int>(new List<FriendSuggestionItemViewModel>(), 0);
            }

            string cleanNorm = RemoveDiacritics(clean);
            string pattern = "%" + clean + "%";
            string patternNorm = "%" + cleanNorm + "%";

            List<int> matchedIds;

            try
            {
                // Thử truy vấn tối ưu với Latin1_General_CI_AI (Accent Insensitive)
                string sql = @"
                    SELECT DISTINCT u.UserID
                    FROM Users u
                    LEFT JOIN UserProfiles p ON u.UserID = p.UserID
                    WHERE (@p0 <= 0 OR u.UserID <> @p0)
                      AND (u.Username COLLATE Latin1_General_CI_AI LIKE @p1
                        OR u.Username COLLATE Latin1_General_CI_AI LIKE @p2
                        OR u.Email COLLATE Latin1_General_CI_AI LIKE @p1
                        OR u.Email COLLATE Latin1_General_CI_AI LIKE @p2
                        OR p.Faculty COLLATE Latin1_General_CI_AI LIKE @p1
                        OR p.Faculty COLLATE Latin1_General_CI_AI LIKE @p2
                        OR p.Major COLLATE Latin1_General_CI_AI LIKE @p1
                        OR p.Major COLLATE Latin1_General_CI_AI LIKE @p2
                        OR p.Bio COLLATE Latin1_General_CI_AI LIKE @p1
                        OR p.Bio COLLATE Latin1_General_CI_AI LIKE @p2)
                ";
                matchedIds = db.Database.SqlQuery<int>(sql, currentUserId, pattern, patternNorm).ToList();
            }
            catch
            {
                // Fallback an toàn qua LINQ to Entities nếu hệ quản trị không hỗ trợ collation
                string lowerClean = clean.ToLower();
                matchedIds = db.Users
                    .AsNoTracking()
                    .Include(u => u.UserProfiles)
                    .Where(u => (currentUserId <= 0 || u.UserID != currentUserId) &&
                        (u.Username.ToLower().Contains(lowerClean) ||
                         (u.Email != null && u.Email.ToLower().Contains(lowerClean)) ||
                         u.UserProfiles.Any(p => (p.Faculty != null && p.Faculty.ToLower().Contains(lowerClean)) ||
                                                 (p.Major != null && p.Major.ToLower().Contains(lowerClean)) ||
                                                 (p.Bio != null && p.Bio.ToLower().Contains(lowerClean)))))
                    .Select(u => u.UserID)
                    .ToList();
            }

            int total = matchedIds.Count;
            if (total == 0)
            {
                return new Tuple<List<FriendSuggestionItemViewModel>, int>(new List<FriendSuggestionItemViewModel>(), 0);
            }

            // Tải chi tiết các user phù hợp trong 1 query duy nhất
            var matchedUsers = db.Users
                .AsNoTracking()
                .Include(u => u.UserProfiles)
                .Where(u => matchedIds.Contains(u.UserID))
                .ToList();

            var myFriendIds = currentUserId > 0 ? GetFriendIds(currentUserId, db) : new List<int>();

            // Lấy quan hệ bạn bè trực tiếp trong 1 query
            var friendships = currentUserId > 0
                ? db.Friendships
                    .AsNoTracking()
                    .Where(f => (f.RequesterID == currentUserId && matchedIds.Contains(f.ReceiverID)) ||
                                (f.ReceiverID == currentUserId && matchedIds.Contains(f.RequesterID)))
                    .ToList()
                : new List<Friendship>();

            // Lấy danh sách đang theo dõi trong 1 query
            var followingIds = currentUserId > 0
                ? new HashSet<int>(db.Follows
                    .AsNoTracking()
                    .Where(f => f.FollowerID == currentUserId && matchedIds.Contains(f.FollowingID))
                    .Select(f => f.FollowingID))
                : new HashSet<int>();

            // Tính số lượng bạn chung trong 1 query
            var mutualMap = new Dictionary<int, int>();
            if (myFriendIds.Any() && matchedIds.Any())
            {
                var mutualFs = db.Friendships
                    .AsNoTracking()
                    .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (myFriendIds.Contains(f.RequesterID) || myFriendIds.Contains(f.ReceiverID)) &&
                        (matchedIds.Contains(f.RequesterID) || matchedIds.Contains(f.ReceiverID)))
                    .Select(f => new { f.RequesterID, f.ReceiverID })
                    .ToList();

                foreach (var uid in matchedIds)
                {
                    int mCount = mutualFs.Count(f =>
                        (f.RequesterID == uid && myFriendIds.Contains(f.ReceiverID)) ||
                        (f.ReceiverID == uid && myFriendIds.Contains(f.RequesterID)));
                    mutualMap[uid] = mCount;
                }
            }

            // Sắp xếp ưu tiên: Khớp bắt đầu tên -> Nhiều bạn chung -> Đang theo dõi
            string cleanNormLower = cleanNorm.ToLowerInvariant();
            string cleanLower = clean.ToLowerInvariant();

            var scoredList = matchedUsers.Select(u =>
            {
                string unameNorm = RemoveDiacritics(u.Username);
                string unameLower = (u.Username ?? "").ToLowerInvariant();

                bool startsWith = unameNorm.StartsWith(cleanNormLower) || unameLower.StartsWith(cleanLower);
                bool exactMatch = unameNorm == cleanNormLower || unameLower == cleanLower;
                int mutualCount = mutualMap.ContainsKey(u.UserID) ? mutualMap[u.UserID] : 0;
                bool isFollowing = followingIds.Contains(u.UserID);

                int score = (exactMatch ? 100 : 0) + (startsWith ? 50 : 0) + (mutualCount * 20) + (isFollowing ? 10 : 0);

                return new { User = u, Score = score, MutualCount = mutualCount, IsFollowing = isFollowing };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.User.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

            var result = new List<FriendSuggestionItemViewModel>();
            foreach (var item in scoredList)
            {
                var u = item.User;
                var prof = u.UserProfiles?.FirstOrDefault();
                var fs = friendships.FirstOrDefault(f => f.RequesterID == u.UserID || f.ReceiverID == u.UserID);

                string relStatus = "None";
                int? fsId = null;
                if (fs != null)
                {
                    fsId = fs.FriendshipID;
                    if (fs.Status == FriendshipStatus.Accepted) relStatus = "Friends";
                    else if (fs.Status == FriendshipStatus.Pending)
                        relStatus = (fs.RequesterID == currentUserId) ? "PendingSent" : "PendingReceived";
                }
                else if (item.IsFollowing)
                {
                    relStatus = "Following";
                }

                result.Add(new FriendSuggestionItemViewModel
                {
                    UserID = u.UserID,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    Avatar = u.SafeAvatar,
                    Faculty = prof?.Faculty ?? "",
                    Major = prof?.Major ?? "",
                    StudentYear = prof?.StudentYear,
                    Bio = prof?.Bio ?? "",
                    MutualFriendsCount = item.MutualCount,
                    RelationshipStatus = relStatus,
                    FriendshipID = fsId,
                    IsFollowing = item.IsFollowing,
                    Reason = item.MutualCount > 0 ? $"{item.MutualCount} bạn chung" : (prof?.Faculty ?? "Thành viên TDMU")
                });
            }

            return new Tuple<List<FriendSuggestionItemViewModel>, int>(result, total);
        }
    }
}
