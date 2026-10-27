import os

path = r"Controllers\UserController.cs"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

old_profile_start = "        // PROFILE CÁ NHÂN VÀ MẠNG XÃ HỘI\n        public new ActionResult Profile(int? id)"
if old_profile_start not in content:
    old_profile_start = old_profile_start.replace("\n", "\r\n")

# Find the start of Profile and end of Profile
counts_def_marker = "        private class ProfileCounts"
if counts_def_marker not in content:
    counts_def_marker = counts_def_marker.replace("\n", "\r\n")

start_idx = content.find(counts_def_marker)
end_marker = "            return View(vm);\n        }"
if end_marker not in content:
    end_marker = end_marker.replace("\n", "\r\n")
end_idx = content.find(end_marker, start_idx) + len(end_marker)

print(f"Profile found from {start_idx} to {end_idx}")

new_profile_code = """        private class ProfileCounts
        {
            public int FollowerCount { get; set; }
            public int FollowingCount { get; set; }
            public int PostCount { get; set; }
            public int MarketCount { get; set; }
            public int ConnectCount { get; set; }
            public int ShareCount { get; set; }
            public int SharedPostCount { get; set; }
        }

        // PROFILE CÁ NHÂN VÀ MẠNG XÃ HỘI
        public new ActionResult Profile(int? id)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId == 0 && (!id.HasValue || id.Value <= 0))
            {
                return RedirectToAction("Login", "User");
            }
            int targetUserId = id.HasValue && id.Value > 0 ? id.Value : currentUserId;
            var targetUser = db.Users
                .AsNoTracking()
                .Include(u => u.UserProfiles)
                .FirstOrDefault(u => u.UserID == targetUserId);
            if (targetUser == null)
            {
                return RedirectToAction("Index", "Home");
            }
            var userProfile = targetUser.UserProfiles?.FirstOrDefault();
            bool isOwner = currentUserId == targetUserId;

            // Quan hệ với người đang xem
            int? friendshipId = null;
            var relStatus = isOwner
                ? UserRelationshipStatus.Self
                : (currentUserId > 0
                    ? SocialHelper.GetRelationshipStatus(currentUserId, targetUserId, db, out friendshipId)
                    : UserRelationshipStatus.None);

            bool isFriend = relStatus == UserRelationshipStatus.Friends;
            bool isFollowing = !isOwner && currentUserId > 0 && db.Follows.Any(f => f.FollowerID == currentUserId && f.FollowingID == targetUserId);

            // Thống kê bạn bè & tương tác
            var friendIds = SocialHelper.GetFriendIds(targetUserId, db);
            int friendCount = friendIds.Count;

            // Gom nhóm đếm (Follower, Following, Post, Market, Connect, Shares) trong 1 round-trip SQL duy nhất
            var counts = db.Database.SqlQuery<ProfileCounts>(@"
                SELECT 
                    (SELECT COUNT(1) FROM Follows WHERE FollowingID = @uid) AS FollowerCount,
                    (SELECT COUNT(1) FROM Follows WHERE FollowerID = @uid) AS FollowingCount,
                    (SELECT COUNT(1) FROM Posts WHERE UserID = @uid) AS PostCount,
                    (SELECT COUNT(1) FROM MarketPosts WHERE UserID = @uid) AS MarketCount,
                    (SELECT COUNT(1) FROM ConnectPosts WHERE UserID = @uid) AS ConnectCount,
                    (SELECT COUNT(1) FROM PostShares WHERE UserID = @uid) AS ShareCount,
                    (SELECT COUNT(1) FROM SharedPosts WHERE UserID = @uid) AS SharedPostCount",
                new System.Data.SqlClient.SqlParameter("@uid", targetUserId)).FirstOrDefault() ?? new ProfileCounts();

            int followerCount = counts.FollowerCount;
            int followingCount = counts.FollowingCount;
            int postCount = counts.PostCount;

            // Bạn bè xem trước (tối đa 9 người)
            var friendsPreviewUsers = friendCount > 0
                ? db.Users
                    .AsNoTracking()
                    .Include(u => u.UserProfiles)
                    .Where(u => friendIds.Contains(u.UserID))
                    .Take(9)
                    .ToList()
                : new List<User>();

            // Chỉ nạp danh sách bạn của người xem khi targetUser thực sự có bạn bè (tiết kiệm 1 remote query)
            var myFriendIds = (currentUserId > 0 && friendCount > 0)
                ? (isOwner ? friendIds : SocialHelper.GetFriendIds(currentUserId, db))
                : new List<int>();

            int mutualFriendCount = (!isOwner && currentUserId > 0 && friendCount > 0 && myFriendIds.Count > 0)
                ? myFriendIds.Intersect(friendIds).Count()
                : 0;

            // Tính mutual friends count cho các bạn bè preview trong 1 truy vấn duy nhất (loại bỏ N+1)
            var previewIds = friendsPreviewUsers.Select(u => u.UserID).ToList();
            var mutualCountsMap = new Dictionary<int, int>();
            if (currentUserId > 0 && previewIds.Count > 0 && myFriendIds.Count > 0)
            {
                var mutualPairs = db.Friendships
                    .AsNoTracking()
                    .Where(f => f.Status == FriendshipStatus.Accepted &&
                        ((previewIds.Contains(f.RequesterID) && myFriendIds.Contains(f.ReceiverID)) ||
                         (previewIds.Contains(f.ReceiverID) && myFriendIds.Contains(f.RequesterID))))
                    .Select(f => new {
                        PreviewId = previewIds.Contains(f.RequesterID) ? f.RequesterID : f.ReceiverID,
                        FriendId = previewIds.Contains(f.RequesterID) ? f.ReceiverID : f.RequesterID
                    })
                    .ToList();

                foreach (var group in mutualPairs.Where(x => x.FriendId != currentUserId && x.FriendId != x.PreviewId).GroupBy(x => x.PreviewId))
                {
                    mutualCountsMap[group.Key] = group.Select(x => x.FriendId).Distinct().Count();
                }
            }

            var friendsPreview = friendsPreviewUsers
                .Select(u => new UserProfilePreviewItem
                {
                    UserID = u.UserID,
                    Username = u.DisplayName,
                    Avatar = u.SafeAvatar,
                    Faculty = u.UserProfiles?.FirstOrDefault()?.Faculty,
                    MutualFriendsCount = (currentUserId > 0 && currentUserId != u.UserID && mutualCountsMap.ContainsKey(u.UserID))
                        ? mutualCountsMap[u.UserID]
                        : 0
                })
                .ToList();

            // Tin 24h đang còn hiệu lực kèm eager load StoryViews (loại bỏ lazy loading N+1)
            var now = DateTime.Now;
            var activeStories = db.Stories
                .AsNoTracking()
                .Include(s => s.StoryViews)
                .Where(s => s.UserID == targetUserId && s.ExpiresAt > now)
                .OrderBy(s => s.CreatedAt)
                .ToList()
                .Select(s => new StoryItemViewModel
                {
                    StoryID = s.StoryID,
                    UserID = s.UserID,
                    MediaUrl = s.MediaUrl,
                    Caption = s.Caption,
                    CreatedAt = s.CreatedAt,
                    ExpiresAt = s.ExpiresAt,
                    TimeAgo = SocialHelper.FormatTimeAgo(s.CreatedAt),
                    ViewCount = s.StoryViews?.Count ?? 0,
                    HasViewed = currentUserId > 0 && s.StoryViews != null && s.StoryViews.Any(v => v.ViewerID == currentUserId),
                    IsOwner = isOwner
                })
                .ToList();

            // Danh sách ảnh gần đây từ bài viết (chỉ query khi có bài viết)
            var recentPhotos = postCount > 0
                ? db.PostMedias
                    .AsNoTracking()
                    .Where(m => m.Post.UserID == targetUserId && m.MediaType == "image")
                    .OrderByDescending(m => m.Post.CreatedAt)
                    .Take(9)
                    .Select(m => m.MediaUrl)
                    .ToList()
                : new List<string>();

            // Dòng thời gian bài viết (kiểm tra tài khoản riêng tư)
            var postsVm = new List<PostItemViewModel>();
            bool canViewTimeline = isOwner || !targetUser.IsPrivate || isFriend;
            List<MarketPost> marketPosts = null;
            List<ConnectPost> connectPosts = null;

            if (canViewTimeline)
            {
                if (postCount > 0)
                {
                    var rawPosts = db.Posts
                        .AsNoTracking()
                        .Include(p => p.User)
                        .Include(p => p.User.UserProfiles)
                        .Include(p => p.PostMedias)
                        .Include(p => p.PostLikes)
                        .Include(p => p.PostComments.Select(c => c.User))
                        .Include(p => p.PostComments.Select(c => c.PostComments1.Select(r => r.User)))
                        .Where(p => p.UserID == targetUserId)
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(20)
                        .ToList();

                    postsVm = rawPosts
                        .Where(p => SocialHelper.CanViewPost(p, currentUserId, db, friendIds))
                        .Select(p => SocialHelper.MapToPostViewModel(p, currentUserId))
                        .ToList();
                }

                // Lấy các bài viết mà người này đã chia sẻ (chỉ query khi có share)
                if (counts.ShareCount > 0)
                {
                    var rawShares = db.PostShares
                        .AsNoTracking()
                        .Include(s => s.User)
                        .Include(s => s.User.UserProfiles)
                        .Include(s => s.Post)
                        .Include(s => s.Post.User)
                        .Include(s => s.Post.User.UserProfiles)
                        .Include(s => s.Post.PostMedias)
                        .Include(s => s.Post.PostLikes)
                        .Include(s => s.PostComments.Select(c => c.User))
                        .Where(s => s.UserID == targetUserId)
                        .OrderByDescending(s => s.CreatedAt)
                        .Take(20)
                        .ToList();

                    var sharedPostsVm = rawShares
                        .Where(s => s.Post == null || SocialHelper.CanViewPost(s.Post, currentUserId, db, friendIds))
                        .Select(s => SocialHelper.MapShareToPostViewModel(s, currentUserId))
                        .Where(vmItem => vmItem != null)
                        .ToList();

                    if (sharedPostsVm.Count > 0)
                    {
                        postsVm = postsVm.Concat(sharedPostsVm).ToList();
                    }
                }

                // Lấy các bài chia sẻ từ Chợ và Kết nối (chỉ query khi có)
                if (counts.SharedPostCount > 0)
                {
                    var rawSharedItems = db.SharedPosts
                        .AsNoTracking()
                        .Include(s => s.User)
                        .Include(s => s.User.UserProfiles)
                        .Where(s => s.UserID == targetUserId)
                        .OrderByDescending(s => s.CreatedAt)
                        .Take(20)
                        .ToList();

                    var sharedItemsVm = rawSharedItems
                        .Select(s => SocialHelper.MapSharedPostToPostViewModel(s, currentUserId, db))
                        .Where(vmItem => vmItem != null)
                        .ToList();

                    if (sharedItemsVm.Count > 0)
                    {
                        postsVm = postsVm.Concat(sharedItemsVm).ToList();
                    }
                }

                if (postsVm.Count > 0)
                {
                    postsVm = postsVm.OrderByDescending(p => p.CreatedAt).ToList();

                    if (currentUserId > 0)
                    {
                        try
                        {
                            var hiddenPostIds = db.Database.SqlQuery<int>(
                                "SELECT PostID FROM UserHiddenPosts WHERE UserID = @uid",
                                new System.Data.SqlClient.SqlParameter("@uid", currentUserId)).ToList();
                            if (hiddenPostIds.Count > 0)
                            {
                                var hiddenSet = new HashSet<int>(hiddenPostIds);
                                postsVm = postsVm.Where(p => !hiddenSet.Contains(p.PostID)).ToList();
                            }
                        }
                        catch { }
                    }

                    bool isAdmin = (Session["IsAdmin"] as bool?) ?? (isOwner && targetUser.IsAdmin == true);
                    SocialHelper.ApplyCommentModeration(postsVm, db, currentUserId, isAdmin);
                }

                // Nạp Chợ và Kết nối độc lập (chỉ query khi có món đồ / bài viết)
                if (counts.MarketCount > 0)
                {
                    marketPosts = db.MarketPosts
                        .AsNoTracking()
                        .Include(p => p.MarketImages)
                        .Include(p => p.MarketCategory)
                        .Where(p => p.UserID == targetUserId)
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(20)
                        .ToList();
                }

                if (counts.ConnectCount > 0)
                {
                    connectPosts = db.ConnectPosts
                        .AsNoTracking()
                        .Include(p => p.ConnectCategory)
                        .Where(p => p.UserID == targetUserId)
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(20)
                        .ToList();
                }
            }

            var vm = new SocialProfileViewModel
            {
                UserID = targetUser.UserID,
                Username = targetUser.DisplayName,
                Email = targetUser.Email,
                PhoneNumber = targetUser.PhoneNumber,
                Avatar = targetUser.SafeAvatar,
                IsTDMUStudent = targetUser.IsTDMUStudent,
                IsPrivate = targetUser.IsPrivate,
                CreatedAt = targetUser.CreatedAt,
                ProfileID = userProfile?.ProfileID,
                CoverPhoto = !string.IsNullOrEmpty(userProfile?.CoverPhoto) ? userProfile.CoverPhoto : "/Content/Images/default-cover.jpg",
                Bio = userProfile?.Bio,
                Faculty = userProfile?.Faculty,
                Major = userProfile?.Major,
                StudentYear = userProfile?.StudentYear,
                Hometown = userProfile?.Hometown,
                RelationshipStatus = relStatus,
                FriendshipID = friendshipId,
                IsFollowing = isFollowing,
                FriendCount = friendCount,
                MutualFriendCount = mutualFriendCount,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                PostCount = postCount,
                FriendsPreview = friendsPreview,
                RecentPhotos = recentPhotos,
                Posts = postsVm,
                ActiveStories = activeStories,
                Highlights = StoryHelper.GetHighlights(targetUserId)
            };
            ViewBag.MarketPosts = marketPosts ?? new List<MarketPost>();
            ViewBag.ConnectPosts = connectPosts ?? new List<ConnectPost>();
            ViewBag.IsOwner = isOwner;
            if (isOwner)
            {
                bool canChange;
                DateTime? lastChange, nextAllowed;
                int daysRemaining;
                GetNameChangeStatus(currentUserId, out canChange, out lastChange, out nextAllowed, out daysRemaining);
                vm.CanChangeUsername = canChange;
                vm.LastUsernameChangeDate = lastChange;
                vm.NextUsernameChangeDate = nextAllowed;
                vm.DaysUntilNextNameChange = daysRemaining;
            }
            return View(vm);
        }"""

if "\r\n" in content:
    new_profile_code = new_profile_code.replace("\n", "\r\n")

content = content[:start_idx] + new_profile_code + content[end_idx:]

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("Updated Controllers/UserController.cs with optimal query gating successfully!")
