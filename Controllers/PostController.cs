using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.SqlClient;

namespace StudentConnect.Controllers
{
    public class PostController : BaseController
    {
        // ==========================================
        // 1. TẠO BÀI VIẾT MỚI
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string content, string privacy, List<HttpPostedFileBase> mediaFiles)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để đăng bài!" });
            }



            int currentUserId = (int)Session["UserID"];
            string cleanContent = (content ?? "").Trim();

            bool hasMedia = mediaFiles != null && mediaFiles.Any(f => f != null && f.ContentLength > 0);

            if (string.IsNullOrEmpty(cleanContent) && !hasMedia)
            {
                return Json(new { success = false, message = "Vui lòng nhập nội dung hoặc đính kèm ảnh/video!" });
            }

            try
            {
                string validPrivacy = PostPrivacy.Public;
                if (privacy == PostPrivacy.Friends || privacy == PostPrivacy.OnlyMe)
                {
                    validPrivacy = privacy;
                }

                var post = new Post
                {
                    UserID = currentUserId,
                    Content = cleanContent,
                    Privacy = validPrivacy,
                    CreatedAt = DateTime.Now
                };

                db.Posts.Add(post);
                db.SaveChanges();

                // Lưu các file đính kèm
                if (hasMedia)
                {
                    string uploadDir = Server.MapPath("~/Content/Uploads/Posts/");
                    if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                    int sortOrder = 0;
                    foreach (var file in mediaFiles.Where(f => f != null && f.ContentLength > 0))
                    {
                        string ext = Path.GetExtension(file.FileName).ToLower();
                        string mediaType = (ext == ".mp4" || ext == ".webm" || ext == ".mov") ? "video" : "image";
                        string fileName = $"post_{post.PostID}_{sortOrder}_{DateTime.Now.Ticks}{ext}";
                        string fullPath = Path.Combine(uploadDir, fileName);

                        file.SaveAs(fullPath);

                        var media = new PostMedia
                        {
                            PostID = post.PostID,
                            MediaUrl = "/Content/Uploads/Posts/" + fileName,
                            MediaType = mediaType,
                            SortOrder = sortOrder++
                        };
                        db.PostMedias.Add(media);
                    }
                    db.SaveChanges();
                }

                return Json(new { 
                    success = true, 
                    postId = post.PostID, 
                    message = "Đăng bài viết thành công!" 
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi đăng bài: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int postId, string content, string privacy)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            int currentUserId = (int)Session["UserID"];
            var post = db.Posts.FirstOrDefault(p => p.PostID == postId);
            if (post == null)
                return Json(new { success = false, message = "Bài viết không tồn tại!" });
            if (post.UserID != currentUserId)
                return Json(new { success = false, message = "Bạn không có quyền sửa bài viết này!" });

            string cleanContent = (content ?? string.Empty).Trim();
            bool hasMedia = db.PostMedias.Any(m => m.PostID == postId);
            if (string.IsNullOrWhiteSpace(cleanContent) && !hasMedia)
                return Json(new { success = false, message = "Bài viết cần có nội dung hoặc ảnh/video." });

            post.Content = cleanContent;
            if (privacy == PostPrivacy.Public || privacy == PostPrivacy.Friends || privacy == PostPrivacy.OnlyMe)
                post.Privacy = privacy;
            post.UpdatedAt = DateTime.Now;
            db.SaveChanges();

            return Json(new { success = true, content = post.Content, privacy = post.Privacy, message = "Đã cập nhật bài viết." });
        }

        // ==========================================
        // 2. XÓA BÀI VIẾT
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            int currentUserId = (int)Session["UserID"];
            var post = db.Posts.FirstOrDefault(p => p.PostID == id);
            if (post == null)
            {
                return Json(new { success = false, message = "Bài viết không tồn tại!" });
            }

            // Only owner may delete (admins have no extra delete privilege)
            if (post.UserID != currentUserId)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa bài viết này!" });
            }

            try
            {
                // Remove related PostMedias (physical files + db rows)
                var medias = db.PostMedias.Where(m => m.PostID == id).ToList();
                foreach (var m in medias)
                {
                    try
                    {
                        string localPath = Server.MapPath(m.MediaUrl);
                        if (System.IO.File.Exists(localPath)) System.IO.File.Delete(localPath);
                    }
                    catch { }
                }
                if (medias.Any()) db.PostMedias.RemoveRange(medias);

                // Remove comment images and comments
                var comments = db.PostComments.Where(c => c.PostID == id).ToList();
                foreach (var c in comments)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(c.ImageUrl))
                        {
                            string full = Server.MapPath(c.ImageUrl.StartsWith("/") ? "~" + c.ImageUrl : c.ImageUrl);
                            if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
                        }
                    }
                    catch { }
                }
                if (comments.Any()) db.PostComments.RemoveRange(comments);

                // Remove likes
                var likes = db.PostLikes.Where(l => l.PostID == id).ToList();
                if (likes.Any()) db.PostLikes.RemoveRange(likes);

                // Remove shares
                var shares = db.PostShares.Where(s => s.PostID == id).ToList();
                if (shares.Any()) db.PostShares.RemoveRange(shares);

                // Remove notifications related to this post (anchor-based or details link)
                try
                {
                    string anchor = "#post-" + id;
                    string detail = "/post/details/" + id;
                    var relatedNotifs = db.Notifications.Where(n => n.Url != null && (n.Url.ToLower().Contains(anchor) || n.Url.ToLower().Contains(detail))).ToList();
                    if (relatedNotifs.Any()) db.Notifications.RemoveRange(relatedNotifs);
                }
                catch { }

                // Finally remove the post entity
                db.Posts.Remove(post);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa bài viết!" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Post.Delete error: " + ex);
                return Json(new { success = false, message = "Lỗi khi xóa bài viết: " + ex.Message });
            }
        }

        // ==========================================
        // 4. ẨN / BỎ ẨN BÀI VIẾT (per-user hide)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Hide(int postId)
        {
            if (Session["UserID"] == null) return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            int currentUserId = (int)Session["UserID"];

            var post = db.Posts.Find(postId);
            if (post == null) return Json(new { success = false, message = "Bài viết không tồn tại!" });

            try
            {
                var exists = db.Database.SqlQuery<int>("SELECT COUNT(1) FROM UserHiddenPosts WHERE UserID = @uid AND PostID = @pid",
                    new SqlParameter("@uid", currentUserId), new SqlParameter("@pid", postId)).FirstOrDefault();
                if (exists == 0)
                {
                    db.Database.ExecuteSqlCommand("INSERT INTO UserHiddenPosts (UserID, PostID, HiddenAt) VALUES (@uid, @pid, @when)",
                        new SqlParameter("@uid", currentUserId), new SqlParameter("@pid", postId), new SqlParameter("@when", DateTime.Now));
                }

                return Json(new { success = true, message = "Đã ẩn bài viết." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi ẩn bài: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Unhide(int postId)
        {
            if (Session["UserID"] == null) return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            int currentUserId = (int)Session["UserID"];

            try
            {
                db.Database.ExecuteSqlCommand("DELETE FROM UserHiddenPosts WHERE UserID = @uid AND PostID = @pid",
                    new SqlParameter("@uid", currentUserId), new SqlParameter("@pid", postId));
                return Json(new { success = true, message = "Đã hiện lại bài viết." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi bỏ ẩn: " + ex.Message });
            }
        }

        // ==========================================
        // 3. THẢ CẢM XÚC / BỎ THẢ CẢM XÚC (REACTION)
        // ==========================================
        [HttpPost]
        public ActionResult ToggleReaction(int postId, string likeType)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            int currentUserId = (int)Session["UserID"];
            var post = db.Posts.Find(postId);
            if (post == null)
            {
                return Json(new { success = false, message = "Bài viết không tồn tại!" });
            }

            string validLikeType = ReactionType.IsValid(likeType) ? likeType : ReactionType.Like;

            var existingLike = db.PostLikes.FirstOrDefault(l => l.PostID == postId && l.UserID == currentUserId);
            string userReaction = null;

            if (existingLike != null)
            {
                if (existingLike.LikeType == validLikeType)
                {
                    // Bỏ cảm xúc
                    db.PostLikes.Remove(existingLike);
                    userReaction = null;
                }
                else
                {
                    // Đổi loại cảm xúc
                    existingLike.LikeType = validLikeType;
                    existingLike.CreatedAt = DateTime.Now;
                    userReaction = validLikeType;
                }
            }
            else
            {
                // Thêm cảm xúc mới
                var newLike = new PostLike
                {
                    PostID = postId,
                    UserID = currentUserId,
                    LikeType = validLikeType,
                    CreatedAt = DateTime.Now
                };
                db.PostLikes.Add(newLike);
                userReaction = validLikeType;

                // Gửi thông báo cho chủ bài viết (nếu không phải tự like bài mình)
                if (post.UserID != currentUserId)
                {
                    var sender = db.Users.Find(currentUserId);
                    var meta = ReactionType.GetMeta(validLikeType);
                    CreateNotification(post.UserID, "like", $"{sender?.DisplayName ?? "Ai đó"} đã bày tỏ cảm xúc {meta.Emoji} về bài viết của bạn.", $"/User/Profile/{post.UserID}#post-{postId}");
                }
            }

            db.SaveChanges();

            // Tính toán lại thống kê
            var likes = db.PostLikes.Where(l => l.PostID == postId).ToList();
            int totalLikes = likes.Count;
            var topReactions = SocialHelper.GetTopReactions(likes);
            var reactionMeta = !string.IsNullOrEmpty(userReaction) ? ReactionType.GetMeta(userReaction) : null;

            return Json(new
            {
                success = true,
                currentReaction = userReaction,
                reactionEmoji = reactionMeta?.Emoji,
                reactionLabel = reactionMeta?.Label,
                reactionColor = reactionMeta?.Color,
                totalLikes = totalLikes,
                topReactions = topReactions
            });
        }

        // ==========================================
        // 4. BÌNH LUẬN BÀI VIẾT
        // ==========================================
        [HttpPost]
        public ActionResult AddComment(int postId, string content, int? parentId = null, HttpPostedFileBase imageFile = null)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            int currentUserId = (int)Session["UserID"];
            var post = db.Posts.Find(postId);
            if (post == null)
            {
                return Json(new { success = false, message = "Bài viết không tồn tại!" });
            }

            string cleanContent = (content ?? "").Trim();
            string imageUrl = null;

            if (imageFile != null && imageFile.ContentLength > 0)
            {
                string uploadDir = Server.MapPath("~/Content/Uploads/Comments/");
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                string ext = Path.GetExtension(imageFile.FileName).ToLower();
                string fileName = $"comment_{postId}_{currentUserId}_{DateTime.Now.Ticks}{ext}";
                imageFile.SaveAs(Path.Combine(uploadDir, fileName));
                imageUrl = "/Content/Uploads/Comments/" + fileName;
            }

            if (string.IsNullOrEmpty(cleanContent) && string.IsNullOrEmpty(imageUrl))
            {
                return Json(new { success = false, message = "Nội dung bình luận không được để trống!" });
            }

            var comment = new PostComment
            {
                PostID = postId,
                UserID = currentUserId,
                Content = cleanContent,
                ImageUrl = imageUrl,
                ParentID = (parentId.HasValue && parentId.Value > 0) ? parentId : null,
                CreatedAt = DateTime.Now
            };

            db.PostComments.Add(comment);
            db.SaveChanges();

            var sender = db.Users.Find(currentUserId);

            // Gửi thông báo cho chủ bài viết
            if (post.UserID != currentUserId)
            {
                CreateNotification(post.UserID, "comment", $"{sender?.DisplayName ?? "Ai đó"} đã bình luận về bài viết của bạn: \"{(cleanContent.Length > 40 ? cleanContent.Substring(0, 40) + "..." : cleanContent)}\"", $"/User/Profile/{post.UserID}#post-{postId}");
            }

            // Nếu là trả lời bình luận, gửi thông báo cho tác giả bình luận cha
            if (comment.ParentID.HasValue)
            {
                var parentComment = db.PostComments.Find(comment.ParentID.Value);
                if (parentComment != null && parentComment.UserID != currentUserId && parentComment.UserID != post.UserID)
                {
                    CreateNotification(parentComment.UserID, "comment", $"{sender?.DisplayName ?? "Ai đó"} đã trả lời bình luận của bạn.", $"/User/Profile/{post.UserID}#post-{postId}");
                }
            }

            bool canDeleteFlag = false;
            try
            {
                // Người tạo bình luận có thể xóa, chủ bài viết có thể xóa, hoặc admin
                canDeleteFlag = (comment.UserID == currentUserId) || (post != null && post.UserID == currentUserId) || (sender != null && sender.IsAdmin);
            }
            catch { }

            return Json(new
            {
                success = true,
                commentId = comment.CommentID,
                authorUsername = sender?.DisplayName ?? "Người dùng",
                authorAvatar = sender?.SafeAvatar ?? "/Content/Images/default-avatar.png",
                isTDMUStudent = sender?.IsTDMUStudent ?? false,
                content = comment.Content,
                imageUrl = comment.ImageUrl,
                timeAgo = "Vừa xong",
                canDelete = canDeleteFlag,
                message = "Đã gửi bình luận!"
            });
        }

        // ==========================================
        // 5. XÓA BÌNH LUẬN
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteComment(int id)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            int currentUserId = (int)Session["UserID"];
            var comment = db.PostComments.Find(id);
            if (comment == null)
            {
                return Json(new { success = false, message = "Bình luận không tồn tại!" });
            }

            var currentUser = db.Users.Find(currentUserId);
            bool isPostOwner = comment.Post != null && comment.Post.UserID == currentUserId;
            bool isCommentOwner = comment.UserID == currentUserId;
            bool isAdmin = currentUser != null && currentUser.IsAdmin;

            if (!isCommentOwner && !isPostOwner && !isAdmin)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa bình luận này!" });
            }

            try
            {
                db.PostComments.Remove(comment);
                db.SaveChanges();
                return Json(new { success = true, message = "Đã xóa bình luận!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa bình luận: " + ex.Message });
            }
        }

        // ==========================================
        // 6. CHIA SẺ BÀI VIẾT (SHARE POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Share(int postId, string content)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để chia sẻ bài viết!" });
            }

            int currentUserId = (int)Session["UserID"];
            var post = db.Posts.Include(p => p.User).FirstOrDefault(p => p.PostID == postId);
            if (post == null)
            {
                return Json(new { success = false, message = "Bài viết không tồn tại!" });
            }

            var friendIds = SocialHelper.GetFriendIds(currentUserId, db);
            if (!SocialHelper.CanViewPost(post, currentUserId, db, friendIds))
            {
                return Json(new { success = false, message = "Bạn không có quyền chia sẻ bài viết này!" });
            }

            try
            {
                var postShare = new PostShare
                {
                    PostID = postId,
                    UserID = currentUserId,
                    Content = (content ?? "").Trim(),
                    CreatedAt = DateTime.Now
                };

                db.PostShares.Add(postShare);
                db.SaveChanges();

                // Gửi thông báo cho tác giả bài viết gốc nếu không phải tự chia sẻ bài của mình
                if (post.UserID != currentUserId)
                {
                    var sender = db.Users.Find(currentUserId);
                    CreateNotification(post.UserID, "share", $"{sender?.DisplayName ?? "Ai đó"} đã chia sẻ bài viết của bạn lên Bảng tin.", $"/User/Profile/{currentUserId}#post-{postId}");
                }

                int totalShares = db.PostShares.Count(s => s.PostID == postId);

                return Json(new
                {
                    success = true,
                    shareId = postShare.ShareID,
                    totalShares = totalShares,
                    message = "Đã chia sẻ bài viết lên Bảng tin của bạn thành công!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi chia sẻ bài viết: " + ex.Message });
            }
        }

        // ==========================================
        // 7. XÓA BÀI CHIA SẺ (DELETE SHARE)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteShare(int shareId)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            int currentUserId = (int)Session["UserID"];
            var share = db.PostShares.Find(shareId);
            if (share == null)
            {
                return Json(new { success = false, message = "Bài chia sẻ không tồn tại!" });
            }

            var currentUser = db.Users.Find(currentUserId);
            bool isAdmin = currentUser != null && currentUser.IsAdmin;

            if (share.UserID != currentUserId && !isAdmin)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa bài chia sẻ này!" });
            }

            try
            {
                int postId = share.PostID;
                db.PostShares.Remove(share);
                db.SaveChanges();

                int totalShares = db.PostShares.Count(s => s.PostID == postId);

                return Json(new
                {
                    success = true,
                    totalShares = totalShares,
                    message = "Đã xóa bài chia sẻ!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa bài chia sẻ: " + ex.Message });
            }
        }
    }
}
