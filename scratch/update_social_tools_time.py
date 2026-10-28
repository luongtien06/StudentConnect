with open('Models/Assistant/SocialTools.cs', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. GetStartDateForTimeRange
target1 = '''            string tr = timeRange.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;
            if (tr == "today" || tr == "homnay") return now.Date;'''
repl1 = '''            string tr = timeRange.Trim().ToLowerInvariant();
            var now = DateTime.Now;
            if (tr == "today" || tr == "homnay") return now.Date;'''

# 2. SearchStoriesAsync
target2 = '''            keyword = (keyword ?? "").Trim();
            limit = Math.Max(1, Math.Min(limit, 10));
            var now = DateTime.UtcNow;'''
repl2 = '''            keyword = (keyword ?? "").Trim();
            limit = Math.Max(1, Math.Min(limit, 10));
            var now = DateTime.Now;'''

# 3. ReactToPostAsync: newLike and newNotification
target3 = '''                var newLike = new PostLike
                {
                    PostID = postId,
                    UserID = currentUserId,
                    LikeType = reactionType ?? "Like",
                    CreatedAt = DateTime.UtcNow
                };'''
repl3 = '''                var newLike = new PostLike
                {
                    PostID = postId,
                    UserID = currentUserId,
                    LikeType = reactionType ?? "Like",
                    CreatedAt = DateTime.Now
                };'''

target4 = '''                        Url = "/Post/Detail/" + postId,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        RefID = postId,'''
repl4 = '''                        Url = "/Post/Detail/" + postId,
                        IsRead = false,
                        CreatedAt = DateTime.Now,
                        RefID = postId,'''

# 4. AddCommentAsync: comment and notification
target5 = '''            var comment = new PostComment
            {
                PostID = postId,
                UserID = currentUserId,
                Content = cleanContent,
                CreatedAt = DateTime.UtcNow
            };'''
repl5 = '''            var comment = new PostComment
            {
                PostID = postId,
                UserID = currentUserId,
                Content = cleanContent,
                CreatedAt = DateTime.Now
            };'''

target6 = '''                    Url = "/Post/Detail/" + postId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RefID = postId,'''
repl6 = '''                    Url = "/Post/Detail/" + postId,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    RefID = postId,'''

# 5. FollowUserAsync: follow and notification
target7 = '''                db.Follows.Add(new Follow
                {
                    FollowerID = currentUserId,
                    FollowingID = targetUserId,
                    CreatedAt = DateTime.UtcNow
                });'''
repl7 = '''                db.Follows.Add(new Follow
                {
                    FollowerID = currentUserId,
                    FollowingID = targetUserId,
                    CreatedAt = DateTime.Now
                });'''

target8 = '''                    Url = "/User/Profile/" + currentUserId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RefID = currentUserId,'''
repl8 = '''                    Url = "/User/Profile/" + currentUserId,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                    RefID = currentUserId,'''

# 6. CreatePostAsync: post CreatedAt and UpdatedAt
target9 = '''                var post = new Post
                {
                    UserID = currentUserId,
                    Content = cleanContent,
                    Privacy = validPrivacy,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };'''
repl9 = '''                var post = new Post
                {
                    UserID = currentUserId,
                    Content = cleanContent,
                    Privacy = validPrivacy,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };'''

# 7. EditPostAsync: post.UpdatedAt
target10 = '''            post.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();'''
repl10 = '''            post.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();'''

# 8. CreateMarketItemAsync: marketItem CreatedAt
target11 = '''                IsHide = false,
                ViewCount = 0,
                CreatedAt = DateTime.UtcNow
            };'''
repl11 = '''                IsHide = false,
                ViewCount = 0,
                CreatedAt = DateTime.Now
            };'''

# 9. CreateConnectItemAsync: connectItem CreatedAt
target12 = '''            var connectItem = new ConnectPost
            {
                UserID = currentUserId,
                CategoryID = targetCatId,
                Title = cleanTitle,
                Description = (description ?? "").Trim(),
                CreatedAt = DateTime.UtcNow
            };'''
repl12 = '''            var connectItem = new ConnectPost
            {
                UserID = currentUserId,
                CategoryID = targetCatId,
                Title = cleanTitle,
                Description = (description ?? "").Trim(),
                CreatedAt = DateTime.Now
            };'''

subs = [
    (target1, repl1, "GetStartDateForTimeRange"),
    (target2, repl2, "SearchStoriesAsync"),
    (target3, repl3, "ReactToPostAsync: like"),
    (target4, repl4, "ReactToPostAsync: notif"),
    (target5, repl5, "AddCommentAsync: comment"),
    (target6, repl6, "AddCommentAsync: notif"),
    (target7, repl7, "FollowUserAsync: follow"),
    (target8, repl8, "FollowUserAsync: notif"),
    (target9, repl9, "CreatePostAsync: post"),
    (target10, repl10, "EditPostAsync: post.UpdatedAt"),
    (target11, repl11, "CreateMarketItemAsync"),
    (target12, repl12, "CreateConnectItemAsync")
]

for t, r, name in subs:
    if t in code:
        code = code.replace(t, r, 1)
        print(f"Replaced: {name}")
    else:
        print(f"FAILED to find target: {name}")

with open('Models/Assistant/SocialTools.cs', 'w', encoding='utf-8') as f:
    f.write(code)

print("Saved Models/Assistant/SocialTools.cs")
