using StudentConnect.Models;
using StudentConnect.Helpers;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace StudentConnect.Areas.Admin.Controllers
{
    public class AdminFeedStoryController : AdminBaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (filterContext.Result != null) return;
            AuthorizeRole(filterContext, "adminFeedStory", "AdminFeedStory");
        }

        public ActionResult Index(string q = "")
        {
            string query = (q ?? "").Trim();
            var postQuery = db.Posts.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.PostMedias)
                .OrderByDescending(p => p.CreatedAt);
            var storyQuery = db.Stories.AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.StoryViews)
                .OrderByDescending(s => s.CreatedAt);

            if (!string.IsNullOrEmpty(query))
            {
                postQuery = postQuery.Where(p => p.Content.Contains(query) || p.User.Username.Contains(query))
                    .OrderByDescending(p => p.CreatedAt);
                storyQuery = storyQuery.Where(s => s.Caption.Contains(query) || s.User.Username.Contains(query))
                    .OrderByDescending(s => s.CreatedAt);
            }

            var now = DateTime.Now;
            var model = new AdminFeedStoryViewModel
            {
                TotalPosts = db.Posts.Count(),
                TotalStories = db.Stories.Count(),
                StoriesActive = db.Stories.Count(s => s.ExpiresAt > now),
                StoriesArchived = db.Stories.Count(s => s.ExpiresAt <= now),
                Posts = postQuery.Take(200).ToList(),
                Stories = storyQuery.Take(200).ToList()
            };
            ViewBag.SearchQuery = query;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeletePost(int id)
        {
            try
            {
                var post = db.Posts.Include(p => p.PostMedias).FirstOrDefault(p => p.PostID == id);
                if (post == null)
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Không tìm thấy bài viết." });
                    TempData["ErrorMessage"] = "Không tìm thấy bài viết.";
                    return RedirectToAction("Index");
                }

                var media = post.PostMedias.ToList();
                foreach (var item in media)
                    DeletePhysicalFile(item.MediaUrl);

                var comments = db.PostComments.Where(c => c.PostID == id).ToList();
                foreach (var comment in comments)
                    DeletePhysicalFile(comment.ImageUrl);
                if (comments.Any()) db.PostComments.RemoveRange(comments);

                var likes = db.PostLikes.Where(l => l.PostID == id).ToList();
                if (likes.Any()) db.PostLikes.RemoveRange(likes);
                var shares = db.PostShares.Where(s => s.PostID == id).ToList();
                if (shares.Any()) db.PostShares.RemoveRange(shares);
                if (media.Any()) db.PostMedias.RemoveRange(media);

                db.Posts.Remove(post);
                db.SaveChanges();

                if (Request.IsAjaxRequest())
                    return Json(new { success = true, message = "Đã xóa bài viết và toàn bộ dữ liệu liên quan thành công!" });

                TempData["SuccessMessage"] = "Đã xóa bài viết và dữ liệu liên quan.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Lỗi khi xóa bài viết: " + ex.Message });
                TempData["ErrorMessage"] = "Lỗi khi xóa bài viết: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteStory(int id)
        {
            try
            {
                var story = db.Stories.Include(s => s.StoryViews).FirstOrDefault(s => s.StoryID == id);
                if (story == null)
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Không tìm thấy tin." });
                    TempData["ErrorMessage"] = "Không tìm thấy tin.";
                    return RedirectToAction("Index");
                }

                string mediaUrl = story.MediaUrl;
                string reactionsFile = Server.MapPath("~/App_Data/StoryData/reactions_" + story.StoryID + ".json");
                if (story.StoryViews != null && story.StoryViews.Any()) db.StoryViews.RemoveRange(story.StoryViews);
                db.Stories.Remove(story);
                db.SaveChanges();
                try
                {
                    StoryHelper.RemoveStoriesFromHighlights(story.UserID, new[] { story.StoryID });
                }
                catch { }

                DeletePhysicalFile(mediaUrl);
                try
                {
                    if (System.IO.File.Exists(reactionsFile)) System.IO.File.Delete(reactionsFile);
                }
                catch { }

                if (Request.IsAjaxRequest())
                    return Json(new { success = true, message = "Đã xóa tin khỏi hệ thống thành công!" });

                TempData["SuccessMessage"] = "Đã xóa tin và dữ liệu liên quan.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Lỗi khi xóa tin: " + ex.Message });
                TempData["ErrorMessage"] = "Lỗi khi xóa tin: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ==========================================
        // XEM CHI TIẾT BÀI VIẾT (AJAX)
        // ==========================================
        [HttpGet]
        public ActionResult GetPostDetail(int id)
        {
            try
            {
                var post = db.Posts.Include(p => p.User)
                    .Include(p => p.PostMedias)
                    .Include(p => p.PostComments)
                    .Include(p => p.PostLikes)
                    .Include(p => p.PostShares)
                    .FirstOrDefault(p => p.PostID == id);
                if (post == null)
                    return Json(new { success = false, message = "Không tìm thấy bài viết." }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    postId = post.PostID,
                    authorName = post.User?.DisplayName ?? post.User?.Username ?? "Người dùng",
                    authorAvatar = post.User?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,
                    authorId = post.UserID,
                    content = post.Content ?? "",
                    privacy = post.Privacy,
                    privacyLabel = PostPrivacy.GetLabel(post.Privacy),
                    createdAt = post.CreatedAt.HasValue ? post.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—",
                    mediaList = post.PostMedias.OrderBy(m => m.SortOrder).Select(m => new { m.MediaUrl, m.MediaType }).ToList(),
                    commentCount = post.PostComments.Count,
                    likeCount = post.PostLikes.Count,
                    shareCount = post.PostShares.Count
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải bài viết: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ==========================================
        // XEM CHI TIẾT TIN (AJAX)
        // ==========================================
        [HttpGet]
        public ActionResult GetStoryDetail(int id)
        {
            try
            {
                var story = db.Stories.Include(s => s.User).Include(s => s.StoryViews).FirstOrDefault(s => s.StoryID == id);
                if (story == null)
                    return Json(new { success = false, message = "Không tìm thấy tin." }, JsonRequestBehavior.AllowGet);

                return Json(new
                {
                    success = true,
                    storyId = story.StoryID,
                    authorName = story.User?.DisplayName ?? story.User?.Username ?? "Người dùng",
                    authorAvatar = story.User?.SafeAvatar ?? DefaultImagePlaceholders.AvatarPath,
                    authorId = story.UserID,
                    caption = story.Caption ?? "",
                    mediaUrl = story.MediaUrl ?? "",
                    isActive = story.ExpiresAt > DateTime.Now,
                    createdAt = story.CreatedAt.HasValue ? story.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—",
                    expiresAt = story.ExpiresAt.ToString("dd/MM/yyyy HH:mm"),
                    viewCount = story.StoryViews?.Count ?? 0
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải tin: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ==========================================
        // ADMIN CHỈNH SỬA BÀI VIẾT (AJAX)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AdminEditPost(int postId, string content, string privacy)
        {
            try
            {
                var post = db.Posts.FirstOrDefault(p => p.PostID == postId);
                if (post == null)
                    return Json(new { success = false, message = "Không tìm thấy bài viết." });

                string cleanContent = (content ?? string.Empty).Trim();
                bool hasMedia = db.PostMedias.Any(m => m.PostID == postId);
                if (string.IsNullOrWhiteSpace(cleanContent) && !hasMedia)
                    return Json(new { success = false, message = "Bài viết cần có nội dung hoặc ảnh/video." });

                post.Content = cleanContent;
                if (privacy == PostPrivacy.Public || privacy == PostPrivacy.Friends || privacy == PostPrivacy.OnlyMe)
                    post.Privacy = privacy;
                post.UpdatedAt = DateTime.Now;
                db.SaveChanges();

                return Json(new { success = true, message = "Đã cập nhật bài viết thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật bài viết: " + ex.Message });
            }
        }

        // ==========================================
        // ADMIN CHỈNH SỬA TIN (AJAX)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AdminEditStory(int storyId, string caption)
        {
            try
            {
                var story = db.Stories.FirstOrDefault(s => s.StoryID == storyId);
                if (story == null)
                    return Json(new { success = false, message = "Không tìm thấy tin." });

                story.Caption = (caption ?? string.Empty).Trim();
                db.SaveChanges();

                return Json(new { success = true, message = "Đã cập nhật tin thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật tin: " + ex.Message });
            }
        }

        // ==========================================
        // GỬI CẢNH BÁO VI PHẠM ĐẾN NGƯỜI DÙNG (AJAX)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendWarning(int targetUserId, string warningType, string warningMessage, int? postId, int? storyId)
        {
            try
            {
                string cleanMsg = (warningMessage ?? "").Trim();
                if (string.IsNullOrWhiteSpace(cleanMsg))
                    return Json(new { success = false, message = "Vui lòng nhập nội dung cảnh báo." });

                string notifMessage;
                string notifUrl;

                if (warningType == "post" && postId.HasValue)
                {
                    notifMessage = "⚠️ CẢNH BÁO TỪ BAN QUẢN TRỊ: Bài viết của bạn (ID #" + postId.Value + ") vi phạm quy tắc cộng đồng. Nội dung: " + cleanMsg;
                    notifUrl = "/User/Profile/" + targetUserId + "#post-" + postId.Value;
                }
                else if (warningType == "story" && storyId.HasValue)
                {
                    notifMessage = "⚠️ CẢNH BÁO TỪ BAN QUẢN TRỊ: Tin của bạn (ID #" + storyId.Value + ") vi phạm quy tắc cộng đồng. Nội dung: " + cleanMsg;
                    notifUrl = "/User/Profile/" + targetUserId;
                }
                else
                {
                    return Json(new { success = false, message = "Loại cảnh báo không hợp lệ." });
                }

                CreateNotification(targetUserId, "warning", notifMessage, notifUrl);

                var user = db.Users.Find(targetUserId);
                string userName = user?.DisplayName ?? user?.Username ?? "Người dùng #" + targetUserId;

                return Json(new { success = true, message = "Đã gửi cảnh báo thành công đến \"" + userName + "\"!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi gửi cảnh báo: " + ex.Message });
            }
        }

        private void DeletePhysicalFile(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;
            try
            {
                string fullPath = Server.MapPath("~" + relativePath);
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("AdminFeedStory file cleanup failed: " + ex.Message);
            }
        }
    }
}