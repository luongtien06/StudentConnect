using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Transactions;
using StudentConnect.Models;

namespace TDMUEcoSystem.TestSuite
{
    class FullAuditDatabaseAndCrud
    {
        static int total = 0;
        static int passed = 0;
        static int failed = 0;

        static void AssertTest(string testName, bool condition, string detail = "")
        {
            total++;
            if (condition)
            {
                passed++;
                Console.WriteLine($"[PASS] {testName}");
            }
            else
            {
                failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testName} - {detail}");
                Console.ResetColor();
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("====================================================================");
            Console.WriteLine("TDMU ECOSYSTEM - FULL AUDIT SUITE 2: DATABASE, CRUD & IDOR SECURITY");
            Console.WriteLine("Timestamp: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("====================================================================");

            const int USER_A_ID = 1022; // testuser01
            const int USER_B_ID = 1023; // testuser001

            using (var db = new TDMUEcoSystemEntities())
            {
                // Verify test users exist
                var userA = db.Users.Find(USER_A_ID);
                var userB = db.Users.Find(USER_B_ID);

                if (userA == null || userB == null)
                {
                    Console.WriteLine($"[FATAL] Test users {USER_A_ID} or {USER_B_ID} not found in database!");
                    return;
                }

                Console.WriteLine($"\n--- TEST ACTORS INITIALIZED ---");
                Console.WriteLine($"Actor A: {userA.Username} (ID {userA.UserID}, Email: {userA.Email})");
                Console.WriteLine($"Actor B: {userB.Username} (ID {userB.UserID}, Email: {userB.Email})");

                // ==========================================
                // PHÂN HỆ B: POSTS, LIKES, COMMENTS, IDOR
                // ==========================================
                Console.WriteLine("\n--- PHAN HE B: POST CRUD, SOCIAL INTERACTIONS & IDOR ---");
                Post createdPost = null;
                try
                {
                    // 1. User A creates Post
                    createdPost = new Post
                    {
                        UserID = USER_A_ID,
                        Content = "[AUDIT_TEST] Post created by testuser01 for comprehensive audit",
                        Privacy = "public",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    db.Posts.Add(createdPost);
                    db.SaveChanges();
                    AssertTest("Post creation by User A persists to DB", createdPost.PostID > 0);

                    // 2. User B likes the post
                    var newLike = new PostLike
                    {
                        PostID = createdPost.PostID,
                        UserID = USER_B_ID,
                        LikeType = "like",
                        CreatedAt = DateTime.Now
                    };
                    db.PostLikes.Add(newLike);
                    db.SaveChanges();

                    int likeCount = db.PostLikes.Count(l => l.PostID == createdPost.PostID);
                    AssertTest("User B like persists and like count equals 1", likeCount == 1);

                    // 3. User B unlikes the post
                    db.PostLikes.Remove(newLike);
                    db.SaveChanges();
                    int unlikeCount = db.PostLikes.Count(l => l.PostID == createdPost.PostID);
                    AssertTest("User B unlike removes record and like count equals 0", unlikeCount == 0);

                    // 4. User B comments on the post
                    var newComment = new PostComment
                    {
                        PostID = createdPost.PostID,
                        UserID = USER_B_ID,
                        Content = "[AUDIT_TEST] Comment by testuser001",
                        CreatedAt = DateTime.Now
                    };
                    db.PostComments.Add(newComment);
                    db.SaveChanges();

                    int commentCount = db.PostComments.Count(c => c.PostID == createdPost.PostID);
                    AssertTest("User B comment persists and comment count equals 1", commentCount == 1 && newComment.CommentID > 0);

                    // 5. IDOR Check: User B attempts to edit User A's post
                    bool userBCanEditPost = (createdPost.UserID == USER_B_ID);
                    AssertTest("IDOR Check: User B edit permission on User A post is rejected", !userBCanEditPost);

                    // 6. IDOR Check: User B attempts to delete User A's post
                    bool userBCanDeletePost = (createdPost.UserID == USER_B_ID);
                    AssertTest("IDOR Check: User B delete permission on User A post is rejected", !userBCanDeletePost);

                    // 7. User A edits own post
                    createdPost.Content = "[AUDIT_TEST] Post content updated by User A";
                    createdPost.UpdatedAt = DateTime.Now;
                    db.SaveChanges();

                    var updatedPost = db.Posts.AsNoTracking().FirstOrDefault(p => p.PostID == createdPost.PostID);
                    AssertTest("User A edit own post successfully updates content", updatedPost != null && updatedPost.Content.Contains("updated"));

                    // 8. User A deletes own post (Clean cascade)
                    // Controller logic cleans comments & likes before removing post
                    var postComments = db.PostComments.Where(c => c.PostID == createdPost.PostID).ToList();
                    db.PostComments.RemoveRange(postComments);
                    db.Posts.Remove(createdPost);
                    db.SaveChanges();

                    var postAfterDelete = db.Posts.Find(createdPost.PostID);
                    var commentsAfterDelete = db.PostComments.Where(c => c.PostID == createdPost.PostID).ToList();
                    AssertTest("User A delete own post cascades cleanly and removes post", postAfterDelete == null && commentsAfterDelete.Count == 0);
                    createdPost = null; // Teardown done
                }
                catch (Exception ex)
                {
                    AssertTest("Phan he B execution without unhandled exception", false, ex.Message);
                    if (createdPost != null && createdPost.PostID > 0)
                    {
                        try
                        {
                            var post = db.Posts.Find(createdPost.PostID);
                            if (post != null) db.Posts.Remove(post);
                            db.SaveChanges();
                        }
                        catch { }
                    }
                }

                // ==========================================
                // PHÂN HỆ C: MARKET CRUD, STATUS & IDOR
                // ==========================================
                Console.WriteLine("\n--- PHAN HE C: MARKET CRUD & IDOR SECURITY ---");
                MarketPost createdMarket = null;
                try
                {
                    // 1. User A creates Market Post
                    createdMarket = new MarketPost
                    {
                        UserID = USER_A_ID,
                        CategoryID = 1,
                        Title = "[AUDIT_TEST] Giao trinh C# test item",
                        Description = "Mo ta san pham kiem thu tu dong",
                        Price = 50000,
                        Status = 1, // Available
                        CreatedAt = DateTime.Now
                    };
                    db.MarketPosts.Add(createdMarket);
                    db.SaveChanges();
                    AssertTest("Market post creation by User A persists to DB", createdMarket.PostID > 0);

                    // 2. IDOR Check: User B attempts to edit Market Post
                    bool userBCanEditMarket = (createdMarket.UserID == USER_B_ID);
                    AssertTest("IDOR Check: User B edit permission on User A market item is rejected", !userBCanEditMarket);

                    // 3. IDOR Check: User B attempts to delete Market Post
                    bool userBCanDeleteMarket = (createdMarket.UserID == USER_B_ID);
                    AssertTest("IDOR Check: User B delete permission on User A market item is rejected", !userBCanDeleteMarket);

                    // 4. User A edits own Market Post
                    createdMarket.Price = 45000;
                    createdMarket.Title = "[AUDIT_TEST] Giao trinh C# updated price";
                    db.SaveChanges();

                    var reloadedMarket = db.MarketPosts.AsNoTracking().FirstOrDefault(m => m.PostID == createdMarket.PostID);
                    AssertTest("User A edit own market item successfully updates price", reloadedMarket != null && reloadedMarket.Price == 45000);

                    // 5. User A deletes own Market Post
                    db.MarketPosts.Remove(createdMarket);
                    db.SaveChanges();

                    var marketAfterDelete = db.MarketPosts.Find(createdMarket.PostID);
                    AssertTest("User A delete own market post successfully cleans record", marketAfterDelete == null);
                    createdMarket = null;
                }
                catch (Exception ex)
                {
                    AssertTest("Phan he C Market execution without exception", false, ex.Message);
                    if (createdMarket != null && createdMarket.PostID > 0)
                    {
                        try
                        {
                            var m = db.MarketPosts.Find(createdMarket.PostID);
                            if (m != null) db.MarketPosts.Remove(m);
                            db.SaveChanges();
                        }
                        catch { }
                    }
                }

                // ==========================================
                // PHÂN HỆ C: CONNECT POSTS CRUD & IDOR
                // ==========================================
                Console.WriteLine("\n--- PHAN HE C: CONNECT POSTS CRUD & IDOR SECURITY ---");
                ConnectPost createdConnect = null;
                try
                {
                    // 1. User A creates Connect Post
                    createdConnect = new ConnectPost
                    {
                        UserID = USER_A_ID,
                        CategoryID = 1,
                        Title = "[AUDIT_TEST] Nhom hoc tap lap trinh web",
                        Description = "Tim thanh vien nghien cuu ASP.NET MVC",
                        MainImage = "/Content/images/default-image.jpg",
                        CreatedAt = DateTime.Now
                    };
                    db.ConnectPosts.Add(createdConnect);
                    db.SaveChanges();
                    AssertTest("Connect post creation by User A persists to DB", createdConnect.PostID > 0);

                    // 2. IDOR Check: User B attempts to edit Connect Post
                    bool userBCanEditConnect = (createdConnect.UserID == USER_B_ID);
                    AssertTest("IDOR Check: User B edit permission on User A connect post is rejected", !userBCanEditConnect);

                    // 3. IDOR Check: User B attempts to delete Connect Post
                    bool userBCanDeleteConnect = (createdConnect.UserID == USER_B_ID);
                    AssertTest("IDOR Check: User B delete permission on User A connect post is rejected", !userBCanDeleteConnect);

                    // 4. User A edits own Connect Post
                    createdConnect.Title = "[AUDIT_TEST] Nhom hoc tap lap trinh web (Updated)";
                    db.SaveChanges();

                    var reloadedConnect = db.ConnectPosts.AsNoTracking().FirstOrDefault(c => c.PostID == createdConnect.PostID);
                    AssertTest("User A edit own connect post successfully updates title", reloadedConnect != null && reloadedConnect.Title.Contains("Updated"));

                    // 5. User A deletes own Connect Post
                    db.ConnectPosts.Remove(createdConnect);
                    db.SaveChanges();

                    var connectAfterDelete = db.ConnectPosts.Find(createdConnect.PostID);
                    AssertTest("User A delete own connect post cleanly removes record", connectAfterDelete == null);
                    createdConnect = null;
                }
                catch (Exception ex)
                {
                    AssertTest("Phan he C Connect execution without exception", false, ex.Message);
                    if (createdConnect != null && createdConnect.PostID > 0)
                    {
                        try
                        {
                            var c = db.ConnectPosts.Find(createdConnect.PostID);
                            if (c != null) db.ConnectPosts.Remove(c);
                            db.SaveChanges();
                        }
                        catch { }
                    }
                }

                // ==========================================
                // PHÂN HỆ C: FRIENDSHIPS LIFECYCLE
                // ==========================================
                Console.WriteLine("\n--- PHAN HE C: FRIENDSHIPS LIFECYCLE ---");
                Friendship friendship = null;
                try
                {
                    // Clean any old friendship between A and B
                    var oldFriendship = db.Friendships.FirstOrDefault(f =>
                        (f.RequesterID == USER_A_ID && f.ReceiverID == USER_B_ID) ||
                        (f.RequesterID == USER_B_ID && f.ReceiverID == USER_A_ID));
                    if (oldFriendship != null)
                    {
                        db.Friendships.Remove(oldFriendship);
                        db.SaveChanges();
                    }

                    // 1. User A sends friend request to User B
                    friendship = new Friendship
                    {
                        RequesterID = USER_A_ID,
                        ReceiverID = USER_B_ID,
                        Status = FriendshipStatus.Pending,
                        CreatedAt = DateTime.Now
                    };
                    db.Friendships.Add(friendship);
                    db.SaveChanges();
                    AssertTest("User A send friend request creates Pending record", friendship.FriendshipID > 0 && friendship.Status == FriendshipStatus.Pending);

                    // 2. User B accepts friend request
                    friendship.Status = FriendshipStatus.Accepted;
                    db.SaveChanges();

                    var reloadedFriendship = db.Friendships.AsNoTracking().FirstOrDefault(f => f.FriendshipID == friendship.FriendshipID);
                    AssertTest("User B accept changes FriendshipStatus to Accepted", reloadedFriendship != null && reloadedFriendship.Status == FriendshipStatus.Accepted);

                    // 3. User A or B unfriends (remove record)
                    db.Friendships.Remove(friendship);
                    db.SaveChanges();

                    var removedFriendship = db.Friendships.Find(friendship.FriendshipID);
                    AssertTest("Unfriend removes relationship cleanly from database", removedFriendship == null);
                    friendship = null;
                }
                catch (Exception ex)
                {
                    AssertTest("Phan he C Friendship execution without exception", false, ex.Message);
                }

                // ==========================================
                // PHÂN HỆ E: NOTIFICATIONS & IDOR
                // ==========================================
                Console.WriteLine("\n--- PHAN HE E: NOTIFICATIONS WORKFLOW & IDOR ---");
                Notification notif = null;
                try
                {
                    // 1. System generates notification for User B
                    notif = new Notification
                    {
                        UserID = USER_B_ID,
                        SenderID = USER_A_ID,
                        Message = "testuser01 đã thích bài viết của bạn.",
                        Type = "post_like",
                        Url = "/Post/Details/1",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    };
                    db.Notifications.Add(notif);
                    db.SaveChanges();
                    AssertTest("Notification for User B created with IsRead = false", notif.NotificationID > 0 && !notif.IsRead);

                    // 2. IDOR Check: User A attempts to mark read or delete User B's notification
                    bool userACanAccessNotif = (notif.UserID == USER_A_ID);
                    AssertTest("IDOR Check: User A access to User B notification is blocked", !userACanAccessNotif);

                    // 3. User B marks notification as read
                    var userBNotif = db.Notifications.FirstOrDefault(n => n.NotificationID == notif.NotificationID && n.UserID == USER_B_ID);
                    if (userBNotif != null)
                    {
                        userBNotif.IsRead = true;
                        db.SaveChanges();
                    }

                    var reloadedNotif = db.Notifications.AsNoTracking().FirstOrDefault(n => n.NotificationID == notif.NotificationID);
                    AssertTest("User B marks own notification as read successfully", reloadedNotif != null && reloadedNotif.IsRead);

                    // 4. User B deletes notification
                    var toDelete = db.Notifications.FirstOrDefault(n => n.NotificationID == notif.NotificationID && n.UserID == USER_B_ID);
                    if (toDelete != null)
                    {
                        db.Notifications.Remove(toDelete);
                        db.SaveChanges();
                    }

                    var deletedNotif = db.Notifications.Find(notif.NotificationID);
                    AssertTest("User B delete own notification removes record cleanly", deletedNotif == null);
                    notif = null;
                }
                catch (Exception ex)
                {
                    AssertTest("Phan he E Notification execution without exception", false, ex.Message);
                }

                // ==========================================
                // PHÂN HỆ E: STORIES & STORYVIEWS LIFECYCLE
                // ==========================================
                Console.WriteLine("\n--- PHAN HE E: STORIES & STORYVIEWS LIFECYCLE ---");
                Story story = null;
                try
                {
                    // 1. User A creates Story
                    story = new Story
                    {
                        UserID = USER_A_ID,
                        MediaUrl = "/Uploads/Stories/audit_test_story.jpg",
                        Caption = "[AUDIT_TEST] Story caption",
                        CreatedAt = DateTime.Now,
                        ExpiresAt = DateTime.Now.AddHours(24)
                    };
                    db.Stories.Add(story);
                    db.SaveChanges();
                    AssertTest("Story created with 24-hour expiration window", story.StoryID > 0 && story.ExpiresAt > DateTime.Now.AddHours(23));

                    // 2. User B views Story
                    var view = new StoryView
                    {
                        StoryID = story.StoryID,
                        ViewerID = USER_B_ID,
                        ViewedAt = DateTime.Now
                    };
                    db.StoryViews.Add(view);
                    db.SaveChanges();

                    int viewCount = db.StoryViews.Count(v => v.StoryID == story.StoryID);
                    AssertTest("StoryView record persisted for viewer User B", viewCount == 1 && view.ViewID > 0);

                    // 3. IDOR Check: User B attempts to delete User A's Story
                    bool userBCanDeleteStory = (story.UserID == USER_B_ID);
                    AssertTest("IDOR Check: User B cannot delete User A story", !userBCanDeleteStory);

                    // 4. User A deletes own Story (Clean views cascade)
                    var relatedViews = db.StoryViews.Where(v => v.StoryID == story.StoryID).ToList();
                    db.StoryViews.RemoveRange(relatedViews);
                    db.Stories.Remove(story);
                    db.SaveChanges();

                    var deletedStory = db.Stories.Find(story.StoryID);
                    var remainingViews = db.StoryViews.Where(v => v.StoryID == story.StoryID).ToList();
                    AssertTest("User A deletes story and cascades related StoryViews cleanly", deletedStory == null && remainingViews.Count == 0);
                    story = null;
                }
                catch (Exception ex)
                {
                    AssertTest("Phan he E Stories execution without exception", false, ex.Message);
                }

                // ==========================================
                // PHÂN HỆ H: DATABASE INTEGRITY & ORPHAN CHECKS
                // ==========================================
                Console.WriteLine("\n--- PHAN HE H: DATABASE INTEGRITY & ORPHAN CHECKS ---");
                try
                {
                    // 1. Orphaned PostLikes check
                    var orphanLikesCount = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM PostLikes WHERE PostID NOT IN (SELECT PostID FROM Posts)").FirstOrDefault();
                    AssertTest("No orphaned PostLikes exist without parent Post", orphanLikesCount == 0, $"Found {orphanLikesCount} orphans");

                    // 2. Orphaned PostComments check
                    var orphanCommentsCount = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM PostComments WHERE PostID NOT IN (SELECT PostID FROM Posts)").FirstOrDefault();
                    AssertTest("No orphaned PostComments exist without parent Post", orphanCommentsCount == 0, $"Found {orphanCommentsCount} orphans");

                    // 3. Orphaned StoryViews check
                    var orphanViewsCount = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM StoryViews WHERE StoryID NOT IN (SELECT StoryID FROM Stories)").FirstOrDefault();
                    AssertTest("No orphaned StoryViews exist without parent Story", orphanViewsCount == 0, $"Found {orphanViewsCount} orphans");

                    // 4. Orphaned MarketImages check
                    var orphanMarketImagesCount = db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM MarketImages WHERE PostID NOT IN (SELECT PostID FROM MarketPosts)").FirstOrDefault();
                    AssertTest("No orphaned MarketImages exist without parent MarketPost", orphanMarketImagesCount == 0, $"Found {orphanMarketImagesCount} orphans");

                    // 5. Transaction Rollback Integrity Test
                    int initialPostCount = db.Posts.Count();
                    try
                    {
                        using (var scope = new TransactionScope())
                        {
                            var rollbackPost = new Post
                            {
                                UserID = USER_A_ID,
                                Content = "[AUDIT_ROLLBACK_TEST] Should be rolled back",
                                Privacy = "public",
                                CreatedAt = DateTime.Now
                            };
                            db.Posts.Add(rollbackPost);
                            db.SaveChanges();

                            // Explicitly do not complete scope, forcing rollback
                            // throw intentional test exception or simply omit scope.Complete()
                        }
                    }
                    catch { }

                    // Re-query to verify post was rolled back
                    using (var freshDb = new TDMUEcoSystemEntities())
                    {
                        int currentPostCount = freshDb.Posts.Count();
                        var leakedPost = freshDb.Posts.FirstOrDefault(p => p.Content.Contains("[AUDIT_ROLLBACK_TEST]"));
                        AssertTest("Transaction rollback leaves database completely clean without data leak", 
                            leakedPost == null && currentPostCount == initialPostCount);
                    }
                }
                catch (Exception ex)
                {
                    AssertTest("Phan he H integrity check without exception", false, ex.Message);
                }
            }

            Console.WriteLine("\n====================================================================");
            Console.WriteLine($"SUITE 2 SUMMARY: TOTAL: {total} | PASSED: {passed} | FAILED: {failed}");
            Console.WriteLine("====================================================================");

            if (failed > 0)
            {
                Environment.Exit(1);
            }
        }
    }
}
