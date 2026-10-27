using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using StudentConnect.Helpers;
using StudentConnect.Models;

namespace StudentConnect.Controllers
{
    public class ShareController : BaseController
    {
        // POST: /Share/ShareItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ShareItem(string itemType, int postId, string content = null, string caption = null)
        {
            if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(caption))
            {
                content = caption;
            }

            if (Session["UserID"] == null)
            {
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để chia sẻ bài viết!" });
            }

            int currentUserId = (int)Session["UserID"];
            string normalizedType = (itemType ?? "").Trim().ToLower();

            int? authorId = null;
            string originalTitle = "";
            string targetUrl = "";
            string standardItemType = "";

            if (normalizedType == "market")
            {
                standardItemType = "Market";
                var marketPost = db.MarketPosts.Include(p => p.User).FirstOrDefault(p => p.PostID == postId);
                if (marketPost == null || marketPost.IsHide == true)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại hoặc đã bị ẩn!" });
                }
                authorId = marketPost.UserID;
                originalTitle = marketPost.Title ?? "Sản phẩm";
                targetUrl = Url.Action("Details", "Market", new { id = postId });
            }
            else if (normalizedType == "connect")
            {
                standardItemType = "Connect";
                var connectPost = db.ConnectPosts.Include(p => p.User).FirstOrDefault(p => p.PostID == postId);
                if (connectPost == null)
                {
                    return Json(new { success = false, message = "Bài viết khám phá không tồn tại!" });
                }
                authorId = connectPost.UserID;
                originalTitle = connectPost.Title ?? "Bài viết";
                targetUrl = Url.Action("Details", "Connect", new { id = postId });
            }
            else if (normalizedType == "post" || normalizedType == "feed" || normalizedType == "social")
            {
                standardItemType = "Post";
                var post = db.Posts.Include(p => p.User).FirstOrDefault(p => p.PostID == postId);
                if (post == null)
                {
                    return Json(new { success = false, message = "Bài viết không tồn tại hoặc đã bị xóa!" });
                }

                var friendIds = SocialHelper.GetFriendIds(currentUserId, db);
                if (!SocialHelper.CanViewPost(post, currentUserId, db, friendIds))
                {
                    return Json(new { success = false, message = "Bạn không có quyền chia sẻ bài viết này do cài đặt quyền riêng tư!" });
                }

                authorId = post.UserID;
                originalTitle = !string.IsNullOrWhiteSpace(post.Content)
                    ? (post.Content.Length > 60 ? post.Content.Substring(0, 57) + "..." : post.Content)
                    : "Bài viết";
                targetUrl = Url.Action("Profile", "User", new { id = post.UserID }) + "#post-" + postId;
            }
            else
            {
                return Json(new { success = false, message = "Loại nội dung chia sẻ không hợp lệ!" });
            }

            const int maxShareContentLength = 500;
            string cleanContent = (content ?? "").Trim();
            if (cleanContent.Length > maxShareContentLength)
            {
                return Json(new
                {
                    success = false,
                    message = $"Lời bình khi chia sẻ không được vượt quá {maxShareContentLength} ký tự."
                });
            }

            try
            {
                int shareId;
                int totalShares;

                if (standardItemType == "Post")
                {
                    var postShare = new PostShare
                    {
                        PostID = postId,
                        UserID = currentUserId,
                        Content = string.IsNullOrEmpty(cleanContent) ? null : cleanContent,
                        CreatedAt = DateTime.Now
                    };

                    db.PostShares.Add(postShare);
                    db.SaveChanges();
                    shareId = postShare.ShareID;

                    // Gửi thông báo cho tác giả bài viết nếu không phải chính người đăng chia sẻ
                    if (authorId.HasValue && authorId.Value != currentUserId)
                    {
                        var currentUser = db.Users.Find(currentUserId);
                        string senderName = currentUser?.DisplayName ?? currentUser?.Username ?? "Một người dùng";
                        string notifMessage = $"{senderName} đã chia sẻ bài viết của bạn lên Bảng tin.";
                        CreateNotification(authorId.Value, "share", notifMessage, targetUrl);
                    }

                    totalShares = db.PostShares.Count(s => s.PostID == postId);
                }
                else
                {
                    var share = new SharedPost
                    {
                        ItemType = standardItemType,
                        PostID = postId,
                        UserID = currentUserId,
                        Content = string.IsNullOrEmpty(cleanContent) ? null : cleanContent,
                        CreatedAt = DateTime.Now
                    };

                    db.SharedPosts.Add(share);
                    db.SaveChanges();
                    shareId = share.ShareID;

                    // Gửi thông báo cho tác giả bài viết nếu không phải chính người đăng chia sẻ
                    if (authorId.HasValue && authorId.Value != currentUserId)
                    {
                        var currentUser = db.Users.Find(currentUserId);
                        string senderName = currentUser?.DisplayName ?? currentUser?.Username ?? "Một người dùng";
                        string notifMessage = standardItemType == "Market"
                            ? $"{senderName} đã chia sẻ món đồ \"{originalTitle}\" của bạn lên Bảng tin."
                            : $"{senderName} đã chia sẻ bài viết \"{originalTitle}\" của bạn lên Bảng tin.";

                        CreateNotification(authorId.Value, "share", notifMessage, targetUrl);
                    }

                    totalShares = db.SharedPosts.Count(s => s.ItemType == standardItemType && s.PostID == postId);
                }

                return Json(new
                {
                    success = true,
                    shareId = shareId,
                    totalShares = totalShares,
                    newCount = totalShares,
                    message = "Đã chia sẻ bài viết lên Bảng tin của bạn thành công!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi chia sẻ: " + ex.Message });
            }
        }

        // GET: /Share/GetShareCount?itemType=Market&postId=123
        [HttpGet]
        public ActionResult GetShareCount(string itemType, int postId)
        {
            string normalizedType = (itemType ?? "").Trim().ToLower();
            int count;
            if (normalizedType == "post" || normalizedType == "feed" || normalizedType == "social")
            {
                count = db.PostShares.Count(s => s.PostID == postId);
            }
            else
            {
                string standardItemType = normalizedType == "market" ? "Market" : "Connect";
                count = db.SharedPosts.Count(s => s.ItemType == standardItemType && s.PostID == postId);
            }
            return Json(new { success = true, totalShares = count, shareCount = count }, JsonRequestBehavior.AllowGet);
        }
    }
}
