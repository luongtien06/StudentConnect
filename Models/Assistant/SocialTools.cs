using StudentConnect.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace StudentConnect.Models.Assistant
{
    public class SocialTools
    {
        // ==========================================
        // 1. NHÓM TRA CỨU & ĐỌC DỮ LIỆU (READ TOOLS)
        // ==========================================

        private static DateTime? GetStartDateForTimeRange(string timeRange)
        {
            if (string.IsNullOrWhiteSpace(timeRange)) return null;
            string tr = timeRange.Trim().ToLowerInvariant();
            var now = DateTimeHelper.VietnamNow;
            if (tr == "today" || tr == "homnay") return now.Date;
            if (tr == "yesterday" || tr == "homqua") return now.Date.AddDays(-1);
            if (tr == "week" || tr == "tuannay") return now.AddDays(-7);
            if (tr == "month" || tr == "thangnay") return now.AddDays(-30);
            return null;
        }

        /// <summary>
        /// Tìm bài viết công khai trên bảng tin StudentConnect
        /// </summary>
        public async Task<JArray> SearchPostsAsync(
            TDMUEcoSystemEntities db,
            string keyword,
            int currentUserId,
            string author = null,
            string timeRange = null,
            int limit = 5)
        {
            keyword = (keyword ?? "").Trim();
            author = (author ?? "").Trim();
            limit = Math.Max(1, Math.Min(limit, 10));

            var query = db.Posts
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.Privacy == "Public" || p.Privacy == null || p.UserID == currentUserId);

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(p => p.Content.Contains(keyword));
            }

            if (!string.IsNullOrEmpty(author))
            {
                query = query.Where(p => p.User.Username.Contains(author) ||
                                         (p.User.DisplayName != null && p.User.DisplayName.Contains(author)));
            }

            var startDate = GetStartDateForTimeRange(timeRange);
            if (startDate.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= startDate.Value);
            }

            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync();

            var result = new JArray();
            foreach (var post in posts)
            {
                result.Add(new JObject
                {
                    ["id"] = post.PostID,
                    ["itemType"] = "Post",
                    ["author"] = post.User?.DisplayName ?? post.User?.Username ?? "Sinh viên",
                    ["authorId"] = post.UserID,
                    ["content"] = (post.Content ?? "").Substring(0, Math.Min((post.Content ?? "").Length, 250)),
                    ["createdAt"] = post.CreatedAt.HasValue ? post.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    ["url"] = "/Home/Index#post-" + post.PostID,
                    ["likesCount"] = post.PostLikes?.Count ?? 0,
                    ["commentsCount"] = post.PostComments?.Count ?? 0
                });
            }

            return result;
        }

        /// <summary>
        /// Tìm đồ dùng / giáo trình trên Chợ sinh viên TDMU
        /// </summary>
        public async Task<JArray> SearchMarketAsync(
            TDMUEcoSystemEntities db,
            string keyword,
            string timeRange = null,
            int limit = 5)
        {
            keyword = (keyword ?? "").Trim();
            limit = Math.Max(1, Math.Min(limit, 10));

            var query = db.MarketPosts
                .AsNoTracking()
                .Include(m => m.User)
                .Include(m => m.MarketCategory)
                .Where(m => m.IsHide != true);

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(m => m.Title.Contains(keyword) || m.Description.Contains(keyword));
            }

            var startDate = GetStartDateForTimeRange(timeRange);
            if (startDate.HasValue)
            {
                query = query.Where(m => m.CreatedAt >= startDate.Value);
            }

            var items = await query
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .ToListAsync();

            var result = new JArray();
            foreach (var item in items)
            {
                result.Add(new JObject
                {
                    ["id"] = item.PostID,
                    ["itemType"] = "Market",
                    ["title"] = item.Title ?? "",
                    ["price"] = item.Price.HasValue ? string.Format("{0:N0} đ", item.Price.Value) : "Thương lượng",
                    ["location"] = item.ExchangeLocation ?? "Khuôn viên trường TDMU",
                    ["condition"] = item.Condition ?? "Còn dùng tốt",
                    ["category"] = item.MarketCategory?.CategoryName ?? "Chợ sinh viên",
                    ["author"] = item.User?.DisplayName ?? item.User?.Username ?? "Sinh viên",
                    ["createdAt"] = item.CreatedAt.HasValue ? item.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    ["url"] = "/Market/Details/" + item.PostID
                });
            }

            return result;
        }

        /// <summary>
        /// Tìm tin ghép trọ, tìm bạn cùng phòng, bạn học tập trên mục Kết nối
        /// </summary>
        public async Task<JArray> SearchConnectAsync(
            TDMUEcoSystemEntities db,
            string keyword,
            string timeRange = null,
            int limit = 5)
        {
            keyword = (keyword ?? "").Trim();
            limit = Math.Max(1, Math.Min(limit, 10));

            var query = db.ConnectPosts
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.ConnectCategory)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(c => c.Title.Contains(keyword) || c.Description.Contains(keyword));
            }

            var startDate = GetStartDateForTimeRange(timeRange);
            if (startDate.HasValue)
            {
                query = query.Where(c => c.CreatedAt >= startDate.Value);
            }

            var connects = await query
                .OrderByDescending(c => c.CreatedAt)
                .Take(limit)
                .ToListAsync();

            var result = new JArray();
            foreach (var c in connects)
            {
                result.Add(new JObject
                {
                    ["id"] = c.PostID,
                    ["itemType"] = "Connect",
                    ["title"] = c.Title ?? "",
                    ["description"] = (c.Description ?? "").Substring(0, Math.Min((c.Description ?? "").Length, 200)),
                    ["category"] = c.ConnectCategory?.CategoryName ?? "Kết nối",
                    ["author"] = c.User?.DisplayName ?? c.User?.Username ?? "Sinh viên",
                    ["createdAt"] = c.CreatedAt.HasValue ? c.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    ["url"] = "/Connect/Details/" + c.PostID
                });
            }

            return result;
        }

        /// <summary>
        /// Tìm tin Story (24h) của sinh viên hoặc chính mình
        /// </summary>
        public async Task<JArray> SearchStoriesAsync(
            TDMUEcoSystemEntities db,
            string keyword = null,
            int currentUserId = 0,
            int limit = 5)
        {
            keyword = (keyword ?? "").Trim();
            limit = Math.Max(1, Math.Min(limit, 10));
            var now = DateTimeHelper.VietnamNow;

            var query = db.Stories
                .AsNoTracking()
                .Include(s => s.User)
                .Where(s => s.ExpiresAt > now || (currentUserId > 0 && s.UserID == currentUserId));

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(s => (s.Caption != null && s.Caption.Contains(keyword)) ||
                                         s.User.Username.Contains(keyword) ||
                                         (s.User.DisplayName != null && s.User.DisplayName.Contains(keyword)));
            }

            var stories = await query
                .OrderByDescending(s => s.CreatedAt)
                .Take(limit)
                .ToListAsync();

            var result = new JArray();
            foreach (var s in stories)
            {
                result.Add(new JObject
                {
                    ["id"] = s.StoryID,
                    ["itemType"] = "Story",
                    ["caption"] = s.Caption ?? "(Không có chú thích)",
                    ["author"] = s.User?.DisplayName ?? s.User?.Username ?? "Sinh viên",
                    ["authorId"] = s.UserID,
                    ["createdAt"] = s.CreatedAt.HasValue ? s.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                    ["expiresAt"] = s.ExpiresAt.ToString("dd/MM/yyyy HH:mm"),
                    ["isExpired"] = s.ExpiresAt <= now,
                    ["url"] = "/Home/Index"
                });
            }

            return result;
        }

        /// <summary>
        /// Tìm tất cả các bài viết, tin chợ, tin kết nối hoặc tin story của CHÍNH người dùng hiện tại
        /// </summary>
        public async Task<JArray> GetMyPostsAsync(
            TDMUEcoSystemEntities db,
            int currentUserId,
            string itemType = "all",
            string keyword = null,
            string timeRange = null,
            int limit = 10)
        {
            if (currentUserId <= 0)
                return new JArray();

            keyword = (keyword ?? "").Trim().ToLowerInvariant();
            itemType = (itemType ?? "all").Trim().ToLowerInvariant();
            limit = Math.Max(1, Math.Min(limit, 20));
            var startDate = GetStartDateForTimeRange(timeRange);

            var result = new JArray();

            // 1. Posts
            if (itemType == "all" || itemType == "post" || itemType == "feed")
            {
                var pQuery = db.Posts
                    .AsNoTracking()
                    .Include(p => p.User)
                    .Where(p => p.UserID == currentUserId);

                if (!string.IsNullOrEmpty(keyword))
                {
                    pQuery = pQuery.Where(p => p.Content.Contains(keyword));
                }
                if (startDate.HasValue)
                {
                    pQuery = pQuery.Where(p => p.CreatedAt >= startDate.Value);
                }

                var posts = await pQuery.OrderByDescending(p => p.CreatedAt).Take(limit).ToListAsync();
                foreach (var p in posts)
                {
                    result.Add(new JObject
                    {
                        ["id"] = p.PostID,
                        ["itemType"] = "Post",
                        ["title"] = (p.Content ?? "").Length > 50 ? (p.Content ?? "").Substring(0, 50) + "..." : (p.Content ?? "Bài viết"),
                        ["snippet"] = (p.Content ?? "").Substring(0, Math.Min((p.Content ?? "").Length, 200)),
                        ["privacy"] = p.Privacy ?? "Public",
                        ["createdAt"] = p.CreatedAt.HasValue ? p.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        ["url"] = "/Home/Index#post-" + p.PostID
                    });
                }
            }

            // 2. MarketPosts
            if (itemType == "all" || itemType == "market" || itemType == "cho" || itemType == "shop")
            {
                var mQuery = db.MarketPosts
                    .AsNoTracking()
                    .Include(m => m.MarketCategory)
                    .Where(m => m.UserID == currentUserId);

                if (!string.IsNullOrEmpty(keyword))
                {
                    mQuery = mQuery.Where(m => m.Title.Contains(keyword) || m.Description.Contains(keyword));
                }
                if (startDate.HasValue)
                {
                    mQuery = mQuery.Where(m => m.CreatedAt >= startDate.Value);
                }

                var marketItems = await mQuery.OrderByDescending(m => m.CreatedAt).Take(limit).ToListAsync();
                foreach (var m in marketItems)
                {
                    result.Add(new JObject
                    {
                        ["id"] = m.PostID,
                        ["itemType"] = "Market",
                        ["title"] = m.Title ?? "Tin chợ",
                        ["snippet"] = (m.Description ?? "").Substring(0, Math.Min((m.Description ?? "").Length, 200)),
                        ["price"] = m.Price.HasValue ? string.Format("{0:N0} đ", m.Price.Value) : "Thương lượng",
                        ["category"] = m.MarketCategory?.CategoryName ?? "Chợ sinh viên",
                        ["condition"] = m.Condition ?? "Còn dùng tốt",
                        ["createdAt"] = m.CreatedAt.HasValue ? m.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        ["url"] = "/Market/Details/" + m.PostID
                    });
                }
            }

            // 3. ConnectPosts
            if (itemType == "all" || itemType == "connect" || itemType == "ketnoi" || itemType == "tro")
            {
                var cQuery = db.ConnectPosts
                    .AsNoTracking()
                    .Include(c => c.ConnectCategory)
                    .Where(c => c.UserID == currentUserId);

                if (!string.IsNullOrEmpty(keyword))
                {
                    cQuery = cQuery.Where(c => c.Title.Contains(keyword) || c.Description.Contains(keyword));
                }
                if (startDate.HasValue)
                {
                    cQuery = cQuery.Where(c => c.CreatedAt >= startDate.Value);
                }

                var connects = await cQuery.OrderByDescending(c => c.CreatedAt).Take(limit).ToListAsync();
                foreach (var c in connects)
                {
                    result.Add(new JObject
                    {
                        ["id"] = c.PostID,
                        ["itemType"] = "Connect",
                        ["title"] = c.Title ?? "Tin kết nối",
                        ["snippet"] = (c.Description ?? "").Substring(0, Math.Min((c.Description ?? "").Length, 200)),
                        ["category"] = c.ConnectCategory?.CategoryName ?? "Kết nối",
                        ["createdAt"] = c.CreatedAt.HasValue ? c.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        ["url"] = "/Connect/Details/" + c.PostID
                    });
                }
            }

            // 4. Stories
            if (itemType == "all" || itemType == "story" || itemType == "tin")
            {
                var sQuery = db.Stories
                    .AsNoTracking()
                    .Where(s => s.UserID == currentUserId);

                if (!string.IsNullOrEmpty(keyword))
                {
                    sQuery = sQuery.Where(s => s.Caption != null && s.Caption.Contains(keyword));
                }
                if (startDate.HasValue)
                {
                    sQuery = sQuery.Where(s => s.CreatedAt >= startDate.Value);
                }

                var stories = await sQuery.OrderByDescending(s => s.CreatedAt).Take(limit).ToListAsync();
                foreach (var s in stories)
                {
                    result.Add(new JObject
                    {
                        ["id"] = s.StoryID,
                        ["itemType"] = "Story",
                        ["title"] = s.Caption ?? "Tin 24h",
                        ["snippet"] = s.Caption ?? "(Không có chú thích)",
                        ["createdAt"] = s.CreatedAt.HasValue ? s.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        ["expiresAt"] = s.ExpiresAt.ToString("dd/MM/yyyy HH:mm"),
                        ["url"] = "/Home/Index"
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Xem chi tiết một bài viết kèm các bình luận gần nhất
        /// </summary>
        public async Task<JObject> GetPostDetailsAsync(
            TDMUEcoSystemEntities db,
            int postId,
            int currentUserId)
        {
            var post = await db.Posts
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.PostComments.Select(c => c.User))
                .Include(p => p.PostLikes)
                .FirstOrDefaultAsync(p => p.PostID == postId);

            if (post == null)
            {
                return new JObject { ["error"] = "Không tìm thấy bài viết ID #" + postId };
            }

            // Kiểm tra quyền xem bài viết
            if (post.Privacy == "OnlyMe" && post.UserID != currentUserId)
            {
                return new JObject { ["error"] = "Bài viết này ở chế độ riêng tư của tác giả." };
            }

            var commentsArr = new JArray();
            foreach (var cm in post.PostComments.OrderByDescending(c => c.CreatedAt).Take(5))
            {
                commentsArr.Add(new JObject
                {
                    ["commentId"] = cm.CommentID,
                    ["author"] = cm.User?.DisplayName ?? cm.User?.Username ?? "Ẩn danh",
                    ["content"] = cm.Content ?? "",
                    ["createdAt"] = cm.CreatedAt.HasValue ? cm.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : ""
                });
            }

            bool hasLiked = post.PostLikes.Any(l => l.UserID == currentUserId);

            return new JObject
            {
                ["postId"] = post.PostID,
                ["author"] = post.User?.DisplayName ?? post.User?.Username ?? "Sinh viên",
                ["authorId"] = post.UserID,
                ["content"] = post.Content ?? "",
                ["privacy"] = post.Privacy ?? "Public",
                ["createdAt"] = post.CreatedAt.HasValue ? post.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                ["likesCount"] = post.PostLikes.Count,
                ["hasCurrentUserLiked"] = hasLiked,
                ["commentsCount"] = post.PostComments.Count,
                ["recentComments"] = commentsArr
            };
        }

        /// <summary>
        /// Lấy danh sách thông báo mới nhất của người dùng hiện tại
        /// </summary>
        public async Task<JArray> GetRecentNotificationsAsync(
            TDMUEcoSystemEntities db,
            int currentUserId,
            int limit = 5)
        {
            if (currentUserId <= 0)
                return new JArray();

            limit = Math.Max(1, Math.Min(limit, 10));

            var notifs = await db.Notifications
                .AsNoTracking()
                .Where(n => n.UserID == currentUserId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .ToListAsync();

            var result = new JArray();
            foreach (var n in notifs)
            {
                result.Add(new JObject
                {
                    ["id"] = n.NotificationID,
                    ["message"] = n.Message ?? "",
                    ["type"] = n.Type ?? "general",
                    ["isRead"] = n.IsRead,
                    ["createdAt"] = n.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    ["url"] = n.Url ?? ""
                });
            }

            return result;
        }

        /// <summary>
        /// Lấy thông tin cá nhân của một người dùng hoặc bản thân
        /// </summary>
        public async Task<JObject> GetUserProfileAsync(
            TDMUEcoSystemEntities db,
            string query,
            int currentUserId)
        {
            User targetUser = null;

            if (string.IsNullOrWhiteSpace(query))
            {
                targetUser = await db.Users
                    .Include(u => u.UserProfiles)
                    .FirstOrDefaultAsync(u => u.UserID == currentUserId);
            }
            else if (int.TryParse(query.Trim(), out int targetId))
            {
                targetUser = await db.Users
                    .Include(u => u.UserProfiles)
                    .FirstOrDefaultAsync(u => u.UserID == targetId);
            }
            else
            {
                string cleanQuery = query.Trim().ToLower();
                targetUser = await db.Users
                    .Include(u => u.UserProfiles)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanQuery ||
                                              (u.DisplayName != null && u.DisplayName.ToLower().Contains(cleanQuery)));
            }

            if (targetUser == null)
            {
                return new JObject { ["error"] = "Không tìm thấy người dùng phù hợp." };
            }

            int postCount = await db.Posts.CountAsync(p => p.UserID == targetUser.UserID);
            int followerCount = await db.Follows.CountAsync(f => f.FollowingID == targetUser.UserID);
            int followingCount = await db.Follows.CountAsync(f => f.FollowerID == targetUser.UserID);
            bool isFollowing = currentUserId > 0 && await db.Follows.AnyAsync(f => f.FollowerID == currentUserId && f.FollowingID == targetUser.UserID);

            var profile = targetUser.UserProfiles?.FirstOrDefault();

            return new JObject
            {
                ["userId"] = targetUser.UserID,
                ["username"] = targetUser.Username ?? "",
                ["displayName"] = targetUser.DisplayName ?? targetUser.Username,
                ["bio"] = profile?.Bio ?? "Chưa cập nhật tiểu sử",
                ["faculty"] = profile?.Faculty ?? "Đại học Thủ Dầu Một",
                ["postCount"] = postCount,
                ["followerCount"] = followerCount,
                ["followingCount"] = followingCount,
                ["isCurrentUserFollowing"] = isFollowing
            };
        }

        /// <summary>
        /// Thống kê mạng xã hội của chính người dùng hiện tại
        /// </summary>
        public async Task<JObject> GetSocialStatisticsAsync(
            TDMUEcoSystemEntities db,
            int currentUserId)
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn chưa đăng nhập." };

            int postCount = await db.Posts.CountAsync(p => p.UserID == currentUserId);
            int followerCount = await db.Follows.CountAsync(f => f.FollowingID == currentUserId);
            int followingCount = await db.Follows.CountAsync(f => f.FollowerID == currentUserId);
            int marketCount = await db.MarketPosts.CountAsync(m => m.UserID == currentUserId);
            int unreadNotifs = await db.Notifications.CountAsync(n => n.UserID == currentUserId && !n.IsRead);

            // Tổng lượt thích nhận được trên các bài viết của mình
            int totalLikesReceived = await db.PostLikes.CountAsync(l => l.Post.UserID == currentUserId);

            return new JObject
            {
                ["userId"] = currentUserId,
                ["totalPosts"] = postCount,
                ["totalLikesReceived"] = totalLikesReceived,
                ["followers"] = followerCount,
                ["following"] = followingCount,
                ["marketItems"] = marketCount,
                ["unreadNotifications"] = unreadNotifs
            };
        }

        // ==========================================
        // 2. NHÓM TƯƠNG TÁC (INTERACTION TOOLS)
        // ==========================================

        /// <summary>
        /// Bày tỏ cảm xúc (Thích / Bỏ thích) bài viết
        /// </summary>
        public async Task<JObject> ReactToPostAsync(
            TDMUEcoSystemEntities db,
            int postId,
            int currentUserId,
            string reactionType = "Like")
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn cần đăng nhập để thích bài viết." };

            var reactCheck = await AgentGuardService.CheckReactionSpamAsync(db, currentUserId);
            if (!reactCheck.allowed)
            {
                return new JObject { ["error"] = reactCheck.reason };
            }

            var post = await db.Posts.FirstOrDefaultAsync(p => p.PostID == postId);
            if (post == null)
                return new JObject { ["error"] = "Không tìm thấy bài viết ID #" + postId };

            var existingLike = await db.PostLikes
                .FirstOrDefaultAsync(l => l.PostID == postId && l.UserID == currentUserId);

            string actionTaken;
            if (existingLike != null)
            {
                // Bỏ thích
                db.PostLikes.Remove(existingLike);
                actionTaken = "unliked";
            }
            else
            {
                // Thêm thích
                var newLike = new PostLike
                {
                    PostID = postId,
                    UserID = currentUserId,
                    LikeType = reactionType ?? "Like",
                    CreatedAt = DateTimeHelper.VietnamNow
                };
                db.PostLikes.Add(newLike);
                actionTaken = "liked";

                // Thông báo cho chủ bài viết (nếu không phải tự like bài của mình)
                if (post.UserID != currentUserId)
                {
                    var sender = await db.Users.FindAsync(currentUserId);
                    string senderName = sender?.DisplayName ?? sender?.Username ?? "Một người bạn";
                    db.Notifications.Add(new Notification
                    {
                        UserID = post.UserID,
                        SenderID = currentUserId,
                        Type = "like",
                        Message = senderName + " đã thích bài viết của bạn.",
                        Url = "/Post/Detail/" + postId,
                        IsRead = false,
                        CreatedAt = DateTimeHelper.VietnamNow,
                        RefID = postId,
                        RefType = "post"
                    });
                }
            }

            await db.SaveChangesAsync();
            int newLikeCount = await db.PostLikes.CountAsync(l => l.PostID == postId);

            return new JObject
            {
                ["success"] = true,
                ["action"] = actionTaken,
                ["postId"] = postId,
                ["newLikesCount"] = newLikeCount,
                ["message"] = actionTaken == "liked" ? "Đã thích bài viết thành công." : "Đã bỏ thích bài viết."
            };
        }

        /// <summary>
        /// Thêm bình luận vào bài viết
        /// </summary>
        public async Task<JObject> AddCommentAsync(
            TDMUEcoSystemEntities db,
            int postId,
            int currentUserId,
            string content)
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn cần đăng nhập để bình luận." };

            string cleanContent = (content ?? "").Trim();
            if (string.IsNullOrEmpty(cleanContent))
                return new JObject { ["error"] = "Nội dung bình luận không được để trống." };

            var commentCheck = await AgentGuardService.CheckCommentSpamAsync(db, currentUserId, postId, cleanContent);
            if (!commentCheck.allowed)
            {
                return new JObject { ["error"] = commentCheck.reason };
            }

            var post = await db.Posts.FirstOrDefaultAsync(p => p.PostID == postId);
            if (post == null)
                return new JObject { ["error"] = "Không tìm thấy bài viết ID #" + postId };

            var comment = new PostComment
            {
                PostID = postId,
                UserID = currentUserId,
                Content = cleanContent,
                CreatedAt = DateTimeHelper.VietnamNow
            };

            db.PostComments.Add(comment);

            // Gửi thông báo cho chủ bài viết nếu khác người bình luận
            if (post.UserID != currentUserId)
            {
                var sender = await db.Users.FindAsync(currentUserId);
                string senderName = sender?.DisplayName ?? sender?.Username ?? "Một người bạn";
                db.Notifications.Add(new Notification
                {
                    UserID = post.UserID,
                    SenderID = currentUserId,
                    Type = "comment",
                    Message = senderName + " đã bình luận về bài viết của bạn: \"" +
                              (cleanContent.Length > 40 ? cleanContent.Substring(0, 40) + "..." : cleanContent) + "\"",
                    Url = "/Post/Detail/" + postId,
                    IsRead = false,
                    CreatedAt = DateTimeHelper.VietnamNow,
                    RefID = postId,
                    RefType = "post"
                });
            }

            await db.SaveChangesAsync();

            return new JObject
            {
                ["success"] = true,
                ["commentId"] = comment.CommentID,
                ["postId"] = postId,
                ["content"] = cleanContent,
                ["createdAt"] = comment.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm"),
                ["message"] = "Đã đăng bình luận thành công!"
            };
        }

        /// <summary>
        /// Theo dõi hoặc Hủy theo dõi một người dùng
        /// </summary>
        public async Task<JObject> FollowUserAsync(
            TDMUEcoSystemEntities db,
            int targetUserId,
            int currentUserId)
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn cần đăng nhập để theo dõi người khác." };

            if (targetUserId == currentUserId)
                return new JObject { ["error"] = "Bạn không thể tự theo dõi chính mình." };

            var followCheck = await AgentGuardService.CheckFollowSpamAsync(db, currentUserId);
            if (!followCheck.allowed)
            {
                return new JObject { ["error"] = followCheck.reason };
            }

            var targetUser = await db.Users.FindAsync(targetUserId);
            if (targetUser == null)
                return new JObject { ["error"] = "Không tìm thấy người dùng có ID #" + targetUserId };

            var existingFollow = await db.Follows
                .FirstOrDefaultAsync(f => f.FollowerID == currentUserId && f.FollowingID == targetUserId);

            string actionTaken;
            if (existingFollow != null)
            {
                db.Follows.Remove(existingFollow);
                actionTaken = "unfollowed";
            }
            else
            {
                db.Follows.Add(new Follow
                {
                    FollowerID = currentUserId,
                    FollowingID = targetUserId,
                    CreatedAt = DateTimeHelper.VietnamNow
                });
                actionTaken = "followed";

                // Thông báo cho người được theo dõi
                var sender = await db.Users.FindAsync(currentUserId);
                string senderName = sender?.DisplayName ?? sender?.Username ?? "Một người bạn";
                db.Notifications.Add(new Notification
                {
                    UserID = targetUserId,
                    SenderID = currentUserId,
                    Type = "follow",
                    Message = senderName + " đã bắt đầu theo dõi bạn.",
                    Url = "/User/Profile/" + currentUserId,
                    IsRead = false,
                    CreatedAt = DateTimeHelper.VietnamNow,
                    RefID = currentUserId,
                    RefType = "user"
                });
            }

            await db.SaveChangesAsync();

            return new JObject
            {
                ["success"] = true,
                ["action"] = actionTaken,
                ["targetUserId"] = targetUserId,
                ["targetName"] = targetUser.DisplayName ?? targetUser.Username,
                ["message"] = actionTaken == "followed"
                    ? "Đã theo dõi " + (targetUser.DisplayName ?? targetUser.Username) + " thành công."
                    : "Đã hủy theo dõi " + (targetUser.DisplayName ?? targetUser.Username) + "."
            };
        }

        // ==========================================
        // 3. NHÓM QUẢN LÝ NỘI DUNG (CONTENT MANAGEMENT)
        // ==========================================

        /// <summary>
        /// Đăng bài viết mới lên bảng tin
        /// </summary>
        public async Task<JObject> CreatePostAsync(
            TDMUEcoSystemEntities db,
            int currentUserId,
            string content,
            string privacy = "Public")
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn cần đăng nhập để tạo bài viết." };

            string cleanContent = (content ?? "").Trim();
            if (string.IsNullOrEmpty(cleanContent))
                return new JObject { ["error"] = "Nội dung bài viết không được để trống." };

            using (await AgentGuardService.AcquireUserLockAsync(currentUserId))
            {
                var postCheck = await AgentGuardService.CheckPostSpamAsync(db, currentUserId, cleanContent);
                if (!postCheck.allowed)
                {
                    return new JObject { ["error"] = postCheck.reason };
                }

                // Chuẩn hóa Privacy
                string validPrivacy = "Public";
                if (string.Equals(privacy, "Friends", StringComparison.OrdinalIgnoreCase)) validPrivacy = "Friends";
                else if (string.Equals(privacy, "OnlyMe", StringComparison.OrdinalIgnoreCase)) validPrivacy = "OnlyMe";

                var post = new Post
                {
                    UserID = currentUserId,
                    Content = cleanContent,
                    Privacy = validPrivacy,
                    CreatedAt = DateTimeHelper.VietnamNow,
                    UpdatedAt = DateTimeHelper.VietnamNow
                };

                db.Posts.Add(post);
                await db.SaveChangesAsync();

                return new JObject
                {
                    ["success"] = true,
                    ["postId"] = post.PostID,
                    ["content"] = cleanContent,
                    ["privacy"] = validPrivacy,
                    ["createdAt"] = post.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm"),
                    ["message"] = "Đã đăng bài viết mới thành công!"
                };
            }
        }

        /// <summary>
        /// Chỉnh sửa nội dung bài viết của chính mình
        /// </summary>
        public async Task<JObject> EditPostAsync(
            TDMUEcoSystemEntities db,
            int postId,
            int currentUserId,
            string newContent,
            string newPrivacy = null)
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn chưa đăng nhập." };

            var post = await db.Posts.FirstOrDefaultAsync(p => p.PostID == postId);
            if (post == null)
                return new JObject { ["error"] = "Không tìm thấy bài viết ID #" + postId };

            if (post.UserID != currentUserId)
                return new JObject { ["error"] = "Bạn không có quyền chỉnh sửa bài viết của người khác." };

            string cleanContent = (newContent ?? "").Trim();
            if (!string.IsNullOrEmpty(cleanContent))
            {
                post.Content = cleanContent;
            }

            if (!string.IsNullOrEmpty(newPrivacy))
            {
                if (string.Equals(newPrivacy, "Public", StringComparison.OrdinalIgnoreCase)) post.Privacy = "Public";
                else if (string.Equals(newPrivacy, "Friends", StringComparison.OrdinalIgnoreCase)) post.Privacy = "Friends";
                else if (string.Equals(newPrivacy, "OnlyMe", StringComparison.OrdinalIgnoreCase)) post.Privacy = "OnlyMe";
            }

            post.UpdatedAt = DateTimeHelper.VietnamNow;
            await db.SaveChangesAsync();

            return new JObject
            {
                ["success"] = true,
                ["postId"] = post.PostID,
                ["content"] = post.Content,
                ["privacy"] = post.Privacy,
                ["updatedAt"] = post.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm"),
                ["message"] = "Đã cập nhật bài viết thành công!"
            };
        }

        /// <summary>
        /// Đăng tin mới lên Chợ sinh viên TDMU
        /// </summary>
        public async Task<JObject> CreateMarketPostAsync(
            TDMUEcoSystemEntities db,
            int currentUserId,
            string title,
            string description,
            decimal? price = null,
            string condition = "Còn dùng tốt",
            string location = "Khuôn viên trường TDMU",
            int? categoryId = null)
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn cần đăng nhập để đăng tin lên Chợ sinh viên." };

            string cleanTitle = (title ?? "").Trim();
            if (string.IsNullOrEmpty(cleanTitle))
                return new JObject { ["error"] = "Tiêu đề tin chợ không được để trống." };

            int targetCatId = categoryId ?? 0;
            if (targetCatId <= 0)
            {
                var firstCat = await db.MarketCategories.FirstOrDefaultAsync();
                targetCatId = firstCat != null ? firstCat.CategoryID : 1;
            }

            var marketItem = new MarketPost
            {
                UserID = currentUserId,
                CategoryID = targetCatId,
                Title = cleanTitle,
                Description = (description ?? "").Trim(),
                Price = price.HasValue ? Math.Max(0, price.Value) : (decimal?)null,
                Condition = string.IsNullOrWhiteSpace(condition) ? "Còn dùng tốt" : condition.Trim(),
                ExchangeLocation = string.IsNullOrWhiteSpace(location) ? "Khuôn viên trường TDMU" : location.Trim(),
                Status = 1, // Đang bán
                IsHide = false,
                ViewCount = 0,
                CreatedAt = DateTimeHelper.VietnamNow
            };

            db.MarketPosts.Add(marketItem);
            await db.SaveChangesAsync();

            return new JObject
            {
                ["success"] = true,
                ["postId"] = marketItem.PostID,
                ["title"] = marketItem.Title,
                ["price"] = marketItem.Price.HasValue ? string.Format("{0:N0} đ", marketItem.Price.Value) : "Thương lượng",
                ["url"] = "/Market/Details/" + marketItem.PostID,
                ["createdAt"] = marketItem.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm"),
                ["message"] = "Đã đăng tin lên Chợ sinh viên thành công! Mã tin #" + marketItem.PostID
            };
        }

        /// <summary>
        /// Đăng tin kết nối (tìm trọ, tìm bạn ở ghép, học tập)
        /// </summary>
        public async Task<JObject> CreateConnectPostAsync(
            TDMUEcoSystemEntities db,
            int currentUserId,
            string title,
            string description,
            int? categoryId = null)
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn cần đăng nhập để đăng bài lên mục Kết nối." };

            string cleanTitle = (title ?? "").Trim();
            if (string.IsNullOrEmpty(cleanTitle))
                return new JObject { ["error"] = "Tiêu đề tin kết nối không được để trống." };

            int targetCatId = categoryId ?? 0;
            if (targetCatId <= 0)
            {
                var firstCat = await db.ConnectCategories.FirstOrDefaultAsync();
                targetCatId = firstCat != null ? firstCat.CategoryID : 1;
            }

            var connectItem = new ConnectPost
            {
                UserID = currentUserId,
                CategoryID = targetCatId,
                Title = cleanTitle,
                Description = (description ?? "").Trim(),
                MainImage = StudentConnect.Helpers.DefaultImagePlaceholders.CoverPath,
                CreatedAt = DateTimeHelper.VietnamNow
            };

            db.ConnectPosts.Add(connectItem);
            await db.SaveChangesAsync();

            return new JObject
            {
                ["success"] = true,
                ["postId"] = connectItem.PostID,
                ["title"] = connectItem.Title,
                ["url"] = "/Connect/Details/" + connectItem.PostID,
                ["createdAt"] = connectItem.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm"),
                ["message"] = "Đã đăng tin lên mục Kết nối thành công! Mã tin #" + connectItem.PostID
            };
        }

        /// <summary>
        /// Kiểm tra quyền sở hữu bài viết (Post, Market, Connect, Story)
        /// </summary>
        public async Task<JObject> VerifyPostOwnershipAsync(
            TDMUEcoSystemEntities db,
            int postId,
            string itemType,
            int currentUserId)
        {
            string type = (itemType ?? "Post").Trim().ToLowerInvariant();

            if (type == "market" || type == "cho")
            {
                var m = await db.MarketPosts.Include(x => x.User).FirstOrDefaultAsync(x => x.PostID == postId);
                if (m == null)
                    return new JObject { ["exists"] = false, ["error"] = "Không tìm thấy tin Chợ sinh viên ID #" + postId };
                bool isOwner = m.UserID.HasValue && m.UserID.Value == currentUserId;
                return new JObject
                {
                    ["exists"] = true,
                    ["isOwner"] = isOwner,
                    ["itemType"] = "Market",
                    ["postId"] = m.PostID,
                    ["title"] = m.Title ?? "Tin chợ sinh viên",
                    ["author"] = m.User?.DisplayName ?? m.User?.Username ?? "Sinh viên",
                    ["authorId"] = m.UserID ?? 0
                };
            }
            else if (type == "connect" || type == "ketnoi")
            {
                var c = await db.ConnectPosts.Include(x => x.User).FirstOrDefaultAsync(x => x.PostID == postId);
                if (c == null)
                    return new JObject { ["exists"] = false, ["error"] = "Không tìm thấy tin Kết nối ID #" + postId };
                bool isOwner = c.UserID.HasValue && c.UserID.Value == currentUserId;
                return new JObject
                {
                    ["exists"] = true,
                    ["isOwner"] = isOwner,
                    ["itemType"] = "Connect",
                    ["postId"] = c.PostID,
                    ["title"] = c.Title ?? "Tin kết nối",
                    ["author"] = c.User?.DisplayName ?? c.User?.Username ?? "Sinh viên",
                    ["authorId"] = c.UserID ?? 0
                };
            }
            else if (type == "story" || type == "tin")
            {
                var s = await db.Stories.Include(x => x.User).FirstOrDefaultAsync(x => x.StoryID == postId);
                if (s == null)
                    return new JObject { ["exists"] = false, ["error"] = "Không tìm thấy tin Story ID #" + postId };
                bool isOwner = s.UserID == currentUserId;
                return new JObject
                {
                    ["exists"] = true,
                    ["isOwner"] = isOwner,
                    ["itemType"] = "Story",
                    ["postId"] = s.StoryID,
                    ["title"] = s.Caption ?? "Tin 24h",
                    ["author"] = s.User?.DisplayName ?? s.User?.Username ?? "Sinh viên",
                    ["authorId"] = s.UserID
                };
            }
            else
            {
                // Default: Post (Bảng tin mạng xã hội)
                var p = await db.Posts.Include(x => x.User).FirstOrDefaultAsync(x => x.PostID == postId);
                if (p == null)
                    return new JObject { ["exists"] = false, ["error"] = "Không tìm thấy bài viết ID #" + postId };
                bool isOwner = p.UserID == currentUserId;
                return new JObject
                {
                    ["exists"] = true,
                    ["isOwner"] = isOwner,
                    ["itemType"] = "Post",
                    ["postId"] = p.PostID,
                    ["title"] = (p.Content ?? "").Length > 50 ? (p.Content ?? "").Substring(0, 50) + "..." : (p.Content ?? "Bài viết"),
                    ["author"] = p.User?.DisplayName ?? p.User?.Username ?? "Sinh viên",
                    ["authorId"] = p.UserID
                };
            }
        }

        /// <summary>
        /// Xóa bài viết / tin chợ / tin kết nối / story của chính mình
        /// </summary>
        public async Task<JObject> DeletePostAsync(
            TDMUEcoSystemEntities db,
            int postId,
            string itemType,
            int currentUserId)
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn chưa đăng nhập." };

            string type = (itemType ?? "Post").Trim().ToLowerInvariant();

            // 1. Kiểm tra quyền sở hữu
            var verify = await VerifyPostOwnershipAsync(db, postId, type, currentUserId);
            if (!(bool)(verify["exists"] ?? false))
            {
                return new JObject { ["error"] = (string)verify["error"] ?? ("Không tìm thấy bài viết ID #" + postId) };
            }

            if (!(bool)(verify["isOwner"] ?? false))
            {
                string authorName = (string)verify["author"] ?? "người khác";
                return new JObject { ["error"] = "Bị từ chối: Bài viết ID #" + postId + " này thuộc về " + authorName + ". Bạn chỉ có quyền xóa nội dung của chính tài khoản mình." };
            }

            // 2. Cascade delete theo loại
            if (type == "market" || type == "cho")
            {
                var market = await db.MarketPosts
                    .Include(m => m.MarketImages)
                    .Include(m => m.MarketWishlists)
                    .Include(m => m.MarketChatRooms)
                    .FirstOrDefaultAsync(m => m.PostID == postId);

                if (market != null)
                {
                    if (market.MarketImages != null && market.MarketImages.Any()) db.MarketImages.RemoveRange(market.MarketImages);
                    if (market.MarketWishlists != null && market.MarketWishlists.Any()) db.MarketWishlists.RemoveRange(market.MarketWishlists);
                    if (market.MarketChatRooms != null && market.MarketChatRooms.Any()) db.MarketChatRooms.RemoveRange(market.MarketChatRooms);

                    var reviews = await db.Reviews.Where(r => r.PostID == postId && r.PostType == "Market").ToListAsync();
                    if (reviews.Any()) db.Reviews.RemoveRange(reviews);

                    var shares = await db.SharedPosts.Where(s => s.PostID == postId && s.ItemType == "market").ToListAsync();
                    if (shares.Any()) db.SharedPosts.RemoveRange(shares);

                    db.MarketPosts.Remove(market);
                    await db.SaveChangesAsync();

                    bool stillExists = await db.MarketPosts.AnyAsync(m => m.PostID == postId);
                    if (stillExists) return new JObject { ["error"] = "Lỗi hệ thống: Chưa xóa được tin Chợ ID #" + postId + " khỏi cơ sở dữ liệu." };

                    return new JObject
                    {
                        ["success"] = true,
                        ["postId"] = postId,
                        ["itemType"] = "Market",
                        ["message"] = "Đã xóa vĩnh viễn tin Chợ sinh viên #" + postId + " (\"" + (market.Title ?? "") + "\") thành công."
                    };
                }
            }
            else if (type == "connect" || type == "ketnoi")
            {
                var connect = await db.ConnectPosts.FirstOrDefaultAsync(c => c.PostID == postId);
                if (connect != null)
                {
                    var reviews = await db.Reviews.Where(r => r.PostID == postId && r.PostType == "Connect").ToListAsync();
                    if (reviews.Any()) db.Reviews.RemoveRange(reviews);

                    var shares = await db.SharedPosts.Where(s => s.PostID == postId && s.ItemType == "connect").ToListAsync();
                    if (shares.Any()) db.SharedPosts.RemoveRange(shares);

                    db.ConnectPosts.Remove(connect);
                    await db.SaveChangesAsync();

                    bool stillExists = await db.ConnectPosts.AnyAsync(c => c.PostID == postId);
                    if (stillExists) return new JObject { ["error"] = "Lỗi hệ thống: Chưa xóa được tin Kết nối ID #" + postId + " khỏi cơ sở dữ liệu." };

                    return new JObject
                    {
                        ["success"] = true,
                        ["postId"] = postId,
                        ["itemType"] = "Connect",
                        ["message"] = "Đã xóa vĩnh viễn tin Kết nối #" + postId + " (\"" + (connect.Title ?? "") + "\") thành công."
                    };
                }
            }
            else if (type == "story" || type == "tin")
            {
                var story = await db.Stories
                    .Include(s => s.StoryViews)
                    .FirstOrDefaultAsync(s => s.StoryID == postId);

                if (story != null)
                {
                    if (story.StoryViews != null && story.StoryViews.Any()) db.StoryViews.RemoveRange(story.StoryViews);

                    db.Stories.Remove(story);
                    await db.SaveChangesAsync();

                    bool stillExists = await db.Stories.AnyAsync(s => s.StoryID == postId);
                    if (stillExists) return new JObject { ["error"] = "Lỗi hệ thống: Chưa xóa được tin Story ID #" + postId + " khỏi cơ sở dữ liệu." };

                    return new JObject
                    {
                        ["success"] = true,
                        ["postId"] = postId,
                        ["itemType"] = "Story",
                        ["message"] = "Đã xóa vĩnh viễn tin Story #" + postId + " thành công."
                    };
                }
            }
            else
            {
                // Mặc định: Post
                var post = await db.Posts
                    .Include(p => p.PostComments)
                    .Include(p => p.PostLikes)
                    .Include(p => p.PostMedias)
                    .Include(p => p.PostShares)
                    .FirstOrDefaultAsync(p => p.PostID == postId);

                if (post != null)
                {
                    if (post.PostComments != null && post.PostComments.Any()) db.PostComments.RemoveRange(post.PostComments);
                    if (post.PostLikes != null && post.PostLikes.Any()) db.PostLikes.RemoveRange(post.PostLikes);
                    if (post.PostMedias != null && post.PostMedias.Any()) db.PostMedias.RemoveRange(post.PostMedias);
                    if (post.PostShares != null && post.PostShares.Any()) db.PostShares.RemoveRange(post.PostShares);

                    var shares = await db.SharedPosts.Where(s => s.PostID == postId && s.ItemType == "post").ToListAsync();
                    if (shares.Any()) db.SharedPosts.RemoveRange(shares);

                    db.Posts.Remove(post);
                    await db.SaveChangesAsync();

                    bool stillExists = await db.Posts.AnyAsync(p => p.PostID == postId);
                    if (stillExists) return new JObject { ["error"] = "Lỗi hệ thống: Chưa xóa được bài viết ID #" + postId + " khỏi cơ sở dữ liệu." };

                    return new JObject
                    {
                        ["success"] = true,
                        ["postId"] = postId,
                        ["itemType"] = "Post",
                        ["message"] = "Đã xóa vĩnh viễn bài viết #" + postId + " thành công."
                    };
                }
            }

            return new JObject { ["error"] = "Không thể xóa bài viết ID #" + postId };
        }

        /// <summary>
        /// Xóa bài viết của chính mình (Tương thích ngược gọi cho Post)
        /// </summary>
        public async Task<JObject> DeletePostAsync(
            TDMUEcoSystemEntities db,
            int postId,
            int currentUserId)
        {
            return await DeletePostAsync(db, postId, "Post", currentUserId);
        }

        // ==========================================
        // 4. NHÓM TỰ ĐỘNG HÓA TÁC VỤ (AUTOMATION TOOLS)
        // ==========================================

        /// <summary>
        /// Tạo một tác vụ tự động theo lịch trình cho Agent
        /// </summary>
        public async Task<JObject> CreateScheduledTaskAsync(
            TDMUEcoSystemEntities db,
            int currentUserId,
            string taskName,
            string userPrompt,
            string scheduleType,
            string scheduleConfig,
            string timeZone = "SE Asia Standard Time")
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn cần đăng nhập để thiết lập tác vụ tự động." };

            string cleanTaskName = (taskName ?? "").Trim();
            string cleanPrompt = (userPrompt ?? "").Trim();

            if (string.IsNullOrEmpty(cleanTaskName) || string.IsNullOrEmpty(cleanPrompt))
                return new JObject { ["error"] = "Tên tác vụ và yêu cầu prompt không được để trống." };

            using (await AgentGuardService.AcquireUserLockAsync(currentUserId))
            {
                var taskLimitCheck = await AgentGuardService.CheckCanCreateTaskAsync(db, currentUserId);
                if (!taskLimitCheck.allowed)
                {
                    return new JObject { ["error"] = taskLimitCheck.reason };
                }

                // Chuẩn hóa scheduleType
                string validSchedule = "Once";
                DateTime nowUtc = DateTime.UtcNow;
                DateTime? nextRun = null;

                string stLower = (scheduleType ?? "once").ToLowerInvariant();
                if (stLower.Contains("hour") || stLower == "hourly")
                {
                    validSchedule = "Hourly";
                    nextRun = nowUtc.AddHours(1);
                }
                else if (stLower.Contains("day") || stLower == "daily")
                {
                    validSchedule = "Daily";
                    nextRun = nowUtc.AddDays(1);
                }
                else if (stLower.Contains("week") || stLower == "weekly")
                {
                    validSchedule = "Weekly";
                    nextRun = nowUtc.AddDays(7);
                }
                else if (stLower.Contains("interval") || stLower.Contains("minute"))
                {
                    validSchedule = "IntervalMinutes";
                    int mins = 30;
                    int.TryParse(scheduleConfig, out mins);
                    if (mins < 5) mins = 5; // Tối thiểu 5 phút
                    nextRun = nowUtc.AddMinutes(mins);
                    scheduleConfig = mins.ToString();
                }
                else
                {
                    validSchedule = "Once";
                    // Chạy sau 5 phút nếu là Once, hoặc theo config
                    int delayMins = 5;
                    int.TryParse(scheduleConfig, out delayMins);
                    if (delayMins < 1) delayMins = 5;
                    nextRun = nowUtc.AddMinutes(delayMins);
                }

                var task = new AgentTask
                {
                    UserID = currentUserId,
                    TaskName = cleanTaskName,
                    UserPrompt = cleanPrompt,
                    TaskType = "Autonomous",
                    ScheduleType = validSchedule,
                    ScheduleConfig = scheduleConfig,
                    TimeZone = string.IsNullOrEmpty(timeZone) ? "SE Asia Standard Time" : timeZone,
                    Status = "Active",
                    NextRunAt = nextRun,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                db.AgentTasks.Add(task);
                await db.SaveChangesAsync();

                return new JObject
                {
                    ["success"] = true,
                    ["taskId"] = task.TaskID,
                    ["taskName"] = cleanTaskName,
                    ["scheduleType"] = validSchedule,
                    ["nextRunAt"] = nextRun.HasValue ? nextRun.Value.ToString("dd/MM/yyyy HH:mm:ss") + " UTC" : "",
                    ["message"] = "Đã khởi tạo tác vụ tự động thành công! Agent sẽ tự động chạy theo lịch."
                };
            }
        }

        /// <summary>
        /// Xem danh sách tác vụ tự động đã lên lịch của người dùng
        /// </summary>
        public async Task<JArray> ListScheduledTasksAsync(
            TDMUEcoSystemEntities db,
            int currentUserId)
        {
            if (currentUserId <= 0)
                return new JArray();

            var tasks = await db.AgentTasks
                .AsNoTracking()
                .Where(t => t.UserID == currentUserId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(15)
                .ToListAsync();

            var result = new JArray();
            foreach (var t in tasks)
            {
                result.Add(new JObject
                {
                    ["taskId"] = t.TaskID,
                    ["taskName"] = t.TaskName ?? "",
                    ["prompt"] = t.UserPrompt ?? "",
                    ["scheduleType"] = t.ScheduleType ?? "Once",
                    ["status"] = t.Status ?? "Active",
                    ["nextRunAt"] = t.NextRunAt.HasValue ? t.NextRunAt.Value.ToString("dd/MM/yyyy HH:mm") : "—",
                    ["lastRunAt"] = t.LastRunAt.HasValue ? t.LastRunAt.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa chạy",
                    ["createdAt"] = t.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                });
            }

            return result;
        }

        /// <summary>
        /// Cập nhật trạng thái tác vụ tự động (Tạm dừng, Tiếp tục, Hủy)
        /// </summary>
        public async Task<JObject> UpdateTaskStatusAsync(
            TDMUEcoSystemEntities db,
            long taskId,
            int currentUserId,
            string newStatus)
        {
            if (currentUserId <= 0)
                return new JObject { ["error"] = "Bạn chưa đăng nhập." };

            var task = await db.AgentTasks
                .FirstOrDefaultAsync(t => t.TaskID == taskId && t.UserID == currentUserId);

            if (task == null)
                return new JObject { ["error"] = "Không tìm thấy tác vụ ID #" + taskId };

            string validStatus = "Active";
            string sLower = (newStatus ?? "").ToLowerInvariant();
            if (sLower == "paused" || sLower == "tamdung" || sLower == "pause") validStatus = "Paused";
            else if (sLower == "cancelled" || sLower == "huy" || sLower == "cancel") validStatus = "Cancelled";
            else if (sLower == "active" || sLower == "tieptuc" || sLower == "resume") validStatus = "Active";

            task.Status = validStatus;
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return new JObject
            {
                ["success"] = true,
                ["taskId"] = taskId,
                ["status"] = validStatus,
                ["message"] = "Đã cập nhật trạng thái tác vụ thành: " + validStatus
            };
        }
    }
}