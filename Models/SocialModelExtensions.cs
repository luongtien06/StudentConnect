using System;
using System.Linq;
using StudentConnect.Helpers;

namespace StudentConnect.Models
{
    /// <summary>
    /// Các phương thức và thuộc tính mở rộng cho thực thể Post (Bài viết)
    /// </summary>
    public partial class Post
    {
        public string TimeAgo => SocialHelper.FormatTimeAgo(CreatedAt);

        public int TotalLikes => PostLikes?.Count ?? 0;
        public int TotalComments => PostComments?.Count ?? 0;
        public int TotalShares => PostShares?.Count ?? 0;

        public string FirstImageUrl => PostMedias?.FirstOrDefault(m => m.MediaType == "image")?.MediaUrl;
        public bool HasMedia => PostMedias != null && PostMedias.Any();

        public string PrivacyLabel => PostPrivacy.GetLabel(Privacy);
        public string PrivacyIcon => PostPrivacy.GetIcon(Privacy);
    }

    /// <summary>
    /// Các phương thức và thuộc tính mở rộng cho thực thể User (Người dùng)
    /// </summary>
    public partial class User
    {
        public string DisplayName => !string.IsNullOrEmpty(Username) ? Username : Email;

        public string SafeAvatar => !string.IsNullOrEmpty(Avatar) 
            ? Avatar 
            : "/Content/Images/default-avatar.png";

        public UserProfile Profile => UserProfiles?.FirstOrDefault();

        public string CoverPhotoUrl => !string.IsNullOrEmpty(Profile?.CoverPhoto) 
            ? Profile.CoverPhoto 
            : "/Content/Images/default-cover.jpg";

        public string Bio => Profile?.Bio ?? "";
        public string Faculty => Profile?.Faculty ?? "";
        public string Major => Profile?.Major ?? "";
        public int? StudentYear => Profile?.StudentYear;
        public string Hometown => Profile?.Hometown ?? "";
    }

    /// <summary>
    /// Các phương thức và thuộc tính mở rộng cho thực thể Story (Tin 24h)
    /// </summary>
    public partial class Story
    {
        public bool IsExpired => ExpiresAt <= DateTime.Now;
        public string TimeAgo => SocialHelper.FormatTimeAgo(CreatedAt);
        public int TotalViews => StoryViews?.Count ?? 0;
    }
}
