using System;
using System.Collections.Generic;
using System.Web;

namespace StudentConnect.Models
{
    // =========================================================================
    // 1. VIEWMODELS CHO BÀI ĐĂNG (POSTS)
    // =========================================================================

    /// <summary>
    /// ViewModel chi tiết hiển thị một bài viết trên Bảng tin hoặc Trang cá nhân
    /// </summary>
    public class PostItemViewModel
    {
        public int PostID { get; set; }
        public int UserID { get; set; }
        public string AuthorUsername { get; set; }
        public string AuthorAvatar { get; set; }
        public bool IsTDMUStudent { get; set; }
        public string Faculty { get; set; }
        public string Major { get; set; }
        public int? StudentYear { get; set; }

        public string Content { get; set; }
        public string Privacy { get; set; }
        public string PrivacyLabel => PostPrivacy.GetLabel(Privacy);
        public string PrivacyIcon => PostPrivacy.GetIcon(Privacy);

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string TimeAgo { get; set; }

        public List<PostMediaItemViewModel> MediaList { get; set; } = new List<PostMediaItemViewModel>();

        // Thống kê tương tác
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ShareCount { get; set; }

        // Reaction của người dùng hiện tại (null nếu chưa thả cảm xúc)
        public string CurrentUserReaction { get; set; }
        public ReactionMeta CurrentUserReactionMeta => !string.IsNullOrEmpty(CurrentUserReaction) 
            ? ReactionType.GetMeta(CurrentUserReaction) 
            : null;

        // Tóm tắt các reaction hàng đầu (VD: 3 Like, 2 Love, 1 Haha)
        public List<ReactionSummaryItem> TopReactions { get; set; } = new List<ReactionSummaryItem>();

        // Bình luận hiển thị trước (VD: 3 bình luận mới nhất)
        public List<PostCommentItemViewModel> Comments { get; set; } = new List<PostCommentItemViewModel>();

        // Phân quyền trên bài viết
        public bool IsOwner { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        // Whether the current user has hidden this post for themselves
        public bool IsHidden { get; set; }

        // Nếu là bài viết được chia sẻ
        public PostShareInfoViewModel SharedPost { get; set; }
    }

    /// <summary>
    /// File media (ảnh/video) đính kèm bài viết
    /// </summary>
    public class PostMediaItemViewModel
    {
        public int MediaID { get; set; }
        public int PostID { get; set; }
        public string MediaUrl { get; set; }
        public string MediaType { get; set; } // image / video
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// Thông tin bài viết gốc khi được chia sẻ
    /// </summary>
    public class PostShareInfoViewModel
    {
        public int ShareID { get; set; }
        public int OriginalPostID { get; set; }
        public int OriginalAuthorID { get; set; }
        public string OriginalAuthorName { get; set; }
        public string OriginalAuthorAvatar { get; set; }
        public string OriginalContent { get; set; }
        public DateTime? OriginalCreatedAt { get; set; }
        public string OriginalTimeAgo { get; set; }
        public List<PostMediaItemViewModel> OriginalMediaList { get; set; } = new List<PostMediaItemViewModel>();
    }

    /// <summary>
    /// Dữ liệu đầu vào khi tạo hoặc chỉnh sửa bài viết
    /// </summary>
    public class CreatePostInputModel
    {
        public string Content { get; set; }
        public string Privacy { get; set; } = PostPrivacy.Public;
        public List<HttpPostedFileBase> MediaFiles { get; set; }
    }

    public class EditPostInputModel
    {
        public int PostID { get; set; }
        public string Content { get; set; }
        public string Privacy { get; set; }
        public List<int> RemovedMediaIDs { get; set; }
        public List<HttpPostedFileBase> NewMediaFiles { get; set; }
    }


    // =========================================================================
    // 2. VIEWMODELS CHO BÌNH LUẬN (COMMENTS)
    // =========================================================================

    public class PostCommentItemViewModel
    {
        public int CommentID { get; set; }
        public int PostID { get; set; }
        public int UserID { get; set; }
        public string AuthorUsername { get; set; }
        public string AuthorAvatar { get; set; }
        public bool IsTDMUStudent { get; set; }

        public string Content { get; set; }
        public string ImageUrl { get; set; }
        public int? ParentID { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string TimeAgo { get; set; }

        public bool IsOwner { get; set; }
        public bool CanDelete { get; set; }

        // Danh sách phản hồi con (Replies)
        public List<PostCommentItemViewModel> Replies { get; set; } = new List<PostCommentItemViewModel>();
        public int ReplyCount => Replies?.Count ?? 0;
    }

    public class CreateCommentInputModel
    {
        public int PostID { get; set; }
        public string Content { get; set; }
        public int? ParentID { get; set; }
        public HttpPostedFileBase ImageFile { get; set; }
    }


    // =========================================================================
    // 3. VIEWMODELS CHO TƯƠNG TÁC / REACTION (LIKES)
    // =========================================================================

    public class ReactionSummaryItem
    {
        public string LikeType { get; set; }
        public string Emoji { get; set; }
        public string IconClass { get; set; }
        public string Color { get; set; }
        public int Count { get; set; }
    }

    public class ToggleReactionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string CurrentReaction { get; set; } // null nếu vừa hủy reaction, hoặc tên reaction mới
        public string ReactionEmoji { get; set; }
        public string ReactionLabel { get; set; }
        public string ReactionColor { get; set; }
        public int TotalLikes { get; set; }
        public List<ReactionSummaryItem> TopReactions { get; set; } = new List<ReactionSummaryItem>();
    }


    // =========================================================================
    // 4. VIEWMODELS CHO TIN 24H (STORIES)
    // =========================================================================

    public class StoryItemViewModel
    {
        public int StoryID { get; set; }
        public int UserID { get; set; }
        public string MediaUrl { get; set; }
        public string Caption { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string TimeAgo { get; set; }
        public int ViewCount { get; set; }
        public bool HasViewed { get; set; }
        public bool IsOwner { get; set; }
    }

    /// <summary>
    /// Nhóm tin theo từng người dùng (khay Stories trên đầu News Feed)
    /// </summary>
    public class UserStoryGroupViewModel
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Avatar { get; set; }
        public bool IsCurrentUser { get; set; }
        public bool HasUnviewedStories { get; set; }
        public List<StoryItemViewModel> Stories { get; set; } = new List<StoryItemViewModel>();
        public StoryItemViewModel LatestStory => Stories?.Count > 0 ? Stories[Stories.Count - 1] : null;
    }

    public class CreateStoryInputModel
    {
        public string Caption { get; set; }
        public HttpPostedFileBase MediaFile { get; set; }
    }

    public class StoryHighlightViewModel
    {
        public string HighlightID { get; set; }
        public int UserID { get; set; }
        public string Title { get; set; }
        public string CoverUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<int> StoryIDs { get; set; } = new List<int>();
        public int StoryCount => StoryIDs?.Count ?? 0;
        public List<StoryItemViewModel> Stories { get; set; } = new List<StoryItemViewModel>();
    }

    public class CreateHighlightInputModel
    {
        public string HighlightID { get; set; }
        public string Title { get; set; }
        public string CoverUrl { get; set; }
        public List<int> StoryIDs { get; set; } = new List<int>();
    }

    public class StoryReactionModel
    {
        public int StoryID { get; set; }
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Avatar { get; set; }
        public string Emoji { get; set; }
        public DateTime ReactedAt { get; set; }
        public string TimeAgo { get; set; }
    }

    // =========================================================================
    // 5. VIEWMODELS CHO TRANG CÁ NHÂN (USER PROFILE)
    // =========================================================================

    public class SocialProfileViewModel
    {
        // Thông tin bảng Users
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Avatar { get; set; }
        public bool IsTDMUStudent { get; set; }
        public bool IsPrivate { get; set; }
        public DateTime? CreatedAt { get; set; }

        // Thông tin mở rộng bảng UserProfiles
        public int? ProfileID { get; set; }
        public string CoverPhoto { get; set; }
        public string Bio { get; set; }
        public string Faculty { get; set; }
        public string Major { get; set; }
        public int? StudentYear { get; set; }
        public string Hometown { get; set; }

        // Mối quan hệ với người đang xem trang
        public UserRelationshipStatus RelationshipStatus { get; set; }
        public int? FriendshipID { get; set; } // Dùng cho nút Chấp nhận/Hủy lời mời
        public bool IsFollowing { get; set; }

        public string DisplayName => !string.IsNullOrEmpty(Username) ? Username : Email;

        // Thống kê
        public int FriendCount { get; set; }
        public int MutualFriendCount { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public int PostCount { get; set; }

        // Danh sách hiển thị xem trước
        public List<UserProfilePreviewItem> FriendsPreview { get; set; } = new List<UserProfilePreviewItem>();
        public List<string> RecentPhotos { get; set; } = new List<string>();

        // Dòng thời gian bài viết
        public List<PostItemViewModel> Posts { get; set; } = new List<PostItemViewModel>();

        // Tin 24h
        public List<StoryItemViewModel> ActiveStories { get; set; } = new List<StoryItemViewModel>();
        public bool HasActiveStories => ActiveStories != null && ActiveStories.Count > 0;

        // Tin nổi bật đã ghim (Story Highlights)
        public List<StoryHighlightViewModel> Highlights { get; set; } = new List<StoryHighlightViewModel>();

        public bool IsOwner => RelationshipStatus == UserRelationshipStatus.Self;
    }

    public class UserProfilePreviewItem
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Avatar { get; set; }
        public string Faculty { get; set; }
        public int MutualFriendsCount { get; set; }
    }

    public class UpdateProfileInputModel
    {
        public string Bio { get; set; }
        public string Faculty { get; set; }
        public string Major { get; set; }
        public int? StudentYear { get; set; }
        public string Hometown { get; set; }
        public bool IsPrivate { get; set; }
        public HttpPostedFileBase AvatarFile { get; set; }
        public HttpPostedFileBase CoverFile { get; set; }
    }


    // =========================================================================
    // 6. VIEWMODELS CHO KẾT BẠN & THEO DÕI (FRIENDSHIPS & FOLLOWS)
    // =========================================================================

    public class FriendRequestItemViewModel
    {
        public int FriendshipID { get; set; }
        public int RequesterID { get; set; }
        public string RequesterUsername { get; set; }
        public string RequesterAvatar { get; set; }
        public string Faculty { get; set; }
        public string Major { get; set; }
        public int MutualFriendsCount { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string TimeAgo { get; set; }
    }

    public class FriendSuggestionItemViewModel
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Avatar { get; set; }
        public string Faculty { get; set; }
        public string Major { get; set; }
        public int MutualFriendsCount { get; set; }
    }

    public class FriendsListViewModel
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public bool IsOwner { get; set; }
        public List<UserProfilePreviewItem> Friends { get; set; } = new List<UserProfilePreviewItem>();
        public List<FriendRequestItemViewModel> PendingRequests { get; set; } = new List<FriendRequestItemViewModel>();
        public List<FriendSuggestionItemViewModel> Suggestions { get; set; } = new List<FriendSuggestionItemViewModel>();
    }


    // =========================================================================
    // 7. VIEWMODEL CHÍNH BẢNG TIN (NEWS FEED)
    // =========================================================================

    public class SocialFeedViewModel
    {
        // Thông tin người đăng nhập
        public int CurrentUserID { get; set; }
        public string CurrentUsername { get; set; }
        public string CurrentUserAvatar { get; set; }
        public bool IsTDMUStudent { get; set; }

        // Khay Stories
        public List<UserStoryGroupViewModel> StoryGroups { get; set; } = new List<UserStoryGroupViewModel>();

        // Danh sách bài viết trên Bảng tin
        public List<PostItemViewModel> Posts { get; set; } = new List<PostItemViewModel>();

        // Cột bên phải: Gợi ý kết bạn & Lời mời kết bạn
        public List<FriendSuggestionItemViewModel> FriendSuggestions { get; set; } = new List<FriendSuggestionItemViewModel>();
        public List<FriendRequestItemViewModel> PendingFriendRequests { get; set; } = new List<FriendRequestItemViewModel>();

        // Phân trang
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public bool HasMore { get; set; }
    }

    // =========================================================================
    // 8. BẢNG TIN TRỘN ĐA DẠNG TRANG CHỦ (MIXED HOME FEED: POSTS / MARKET / CONNECT)
    // =========================================================================

    public enum HomeFeedItemType
    {
        SocialPost,
        MarketPost,
        ConnectPost
    }

    public class HomeFeedItemViewModel
    {
        public HomeFeedItemType ItemType { get; set; }
        public DateTime CreatedAt { get; set; }
        public PostItemViewModel SocialPost { get; set; }
        public MarketPost MarketPost { get; set; }
        public ConnectPost ConnectPost { get; set; }
    }
}
