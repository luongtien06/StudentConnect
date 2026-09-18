using StudentConnect.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class ConnectController : BaseController
    {

        // ────────────────────────────────────────────────────────────────
        // 1. TRANG CHỦ KẾT NỐI (Hiển thị theo Block danh mục giống các trang Du lịch)
        // ────────────────────────────────────────────────────────────────
        public ActionResult Index(string searchString, string category = null)
        {
            // Đảm bảo danh mục "Hỗ trợ" luôn tồn tại trong Database
            if (!db.ConnectCategories.Any(c => c.CategoryName == "Hỗ trợ"))
            {
                try
                {
                    db.ConnectCategories.Add(new ConnectCategory { CategoryName = "Hỗ trợ" });
                    db.SaveChanges();
                }
                catch { }
            }

            var allCategories = db.ConnectCategories
                                  .Select(c => c.CategoryName)
                                  .Distinct()
                                  .ToList();

            if (!allCategories.Any())
            {
                allCategories = new List<string> { "Ẩm thực & Buffet", "Nước uống & Coffee", "Địa điểm vui chơi", "Hỗ trợ" };
            }

            List<string> targetCategories;
            bool isSpecificCategory = !string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase);

            if (isSpecificCategory)
            {
                // Khi tìm/lọc theo 1 danh mục, CHỈ hiển thị duy nhất danh mục đó!
                targetCategories = allCategories.Where(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
                if (!targetCategories.Any()) targetCategories = new List<string> { category };
            }
            else
            {
                targetCategories = allCategories;
            }

            var categorizedPosts = new Dictionary<string, List<ConnectPost>>();
            var query = db.ConnectPosts.AsNoTracking().Include("ConnectCategory").Include("User").AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                string searchLower = searchString.Trim().ToLower();
                query = query.Where(p => p.Title.ToLower().Contains(searchLower)
                                      || p.Description.ToLower().Contains(searchLower));
            }

            foreach (var catName in targetCategories)
            {
                var postsInCatQuery = query
                    .Where(p => p.ConnectCategory.CategoryName == catName)
                    .OrderByDescending(p => p.CreatedAt);

                var postsInCat = isSpecificCategory || !string.IsNullOrEmpty(searchString)
                    ? postsInCatQuery.Take(24).ToList()
                    : postsInCatQuery.Take(8).ToList();

                // Nếu đang lọc theo danh mục cụ thể hoặc đang tìm kiếm, hiển thị danh mục đó (kể cả khi chưa có bài để hiện empty state)
                if (isSpecificCategory || postsInCat.Any())
                {
                    categorizedPosts.Add(catName, postsInCat);
                }
            }

            ViewBag.AllCategories = allCategories;
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentCategory = isSpecificCategory ? category : null;

            if (Request.IsAjaxRequest())
            {
                return PartialView("_ConnectPostsPartial", categorizedPosts);
            }

            return View(categorizedPosts);
        }

        public ActionResult GetPostsAjax(string searchString, string category = null)
        {
            var allCategories = db.ConnectCategories
                                  .Select(c => c.CategoryName)
                                  .Distinct()
                                  .ToList();

            if (!allCategories.Any())
            {
                allCategories = new List<string> { "Ẩm thực & Buffet", "Nước uống & Coffee", "Địa điểm vui chơi", "Hỗ trợ" };
            }

            List<string> targetCategories;
            bool isSpecificCategory = !string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase);

            if (isSpecificCategory)
            {
                targetCategories = allCategories.Where(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
                if (!targetCategories.Any()) targetCategories = new List<string> { category };
            }
            else
            {
                targetCategories = allCategories;
            }

            var categorizedPosts = new Dictionary<string, List<ConnectPost>>();
            var query = db.ConnectPosts.AsNoTracking().Include("ConnectCategory").Include("User").AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                string searchLower = searchString.Trim().ToLower();
                query = query.Where(p => p.Title.ToLower().Contains(searchLower)
                                      || p.Description.ToLower().Contains(searchLower));
            }

            foreach (var catName in targetCategories)
            {
                var postsInCatQuery = query
                    .Where(p => p.ConnectCategory.CategoryName == catName)
                    .OrderByDescending(p => p.CreatedAt);

                var postsInCat = isSpecificCategory || !string.IsNullOrEmpty(searchString)
                    ? postsInCatQuery.Take(24).ToList()
                    : postsInCatQuery.Take(8).ToList();

                if (isSpecificCategory || postsInCat.Any())
                {
                    categorizedPosts.Add(catName, postsInCat);
                }
            }

            ViewBag.AllCategories = allCategories;
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentCategory = isSpecificCategory ? category : null;

            return PartialView("_ConnectPostsPartial", categorizedPosts);
        }

        // ────────────────────────────────────────────────────────────────
        // 2. TRANG CHI TIẾT & ĐÁNH GIÁ (Review)
        // ────────────────────────────────────────────────────────────────
        public ActionResult Details(int id)
        {
            var post = db.ConnectPosts.AsNoTracking().Include("ConnectCategory").Include("User").FirstOrDefault(p => p.PostID == id);
            if (post == null)
            {
                TempData["DeletedPost"] = true;
                return RedirectToAction("Index");
            }

            var reviews = db.Reviews
                            .AsNoTracking()
                            .Include("User")
                            .Where(r => r.PostID == id && r.PostType == "Connect")
                            .OrderByDescending(r => r.CreatedAt)
                            .ToList();

            ViewBag.Reviews = reviews;
            ViewBag.AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.RatingStar ?? 0), 1) : 0;
            ViewBag.ReviewCount = reviews.Count;

            bool hasReviewed = false;
            if (Session["UserID"] != null)
            {
                int currentUserId = (int)Session["UserID"];
                hasReviewed = reviews.Any(r => r.UserID == currentUserId);
            }
            ViewBag.HasReviewed = hasReviewed;

            return View(post);
        }

        // ────────────────────────────────────────────────────────────────
        // 3. XỬ LÝ THÊM BÌNH LUẬN / ĐÁNH GIÁ (SYNC & AJAX)
        // ────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddReview(int postId, int rating, string comment)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int userId = (int)Session["UserID"];

            if (rating < 1 || rating > 5)
                return RedirectToAction("Details", new { id = postId });

            bool hasReviewed = db.Reviews.Any(r => r.PostID == postId && r.PostType == "Connect" && r.UserID == userId);
            if (!hasReviewed)
            {
                var review = new Review
                {
                    PostID = postId,
                    PostType = "Connect",
                    UserID = userId,
                    RatingStar = rating,
                    Comment = comment,
                    CreatedAt = DateTime.Now
                };
                db.Reviews.Add(review);
                db.SaveChanges();

                var post = db.ConnectPosts.FirstOrDefault(p => p.PostID == postId);
                if (post != null && post.UserID.HasValue && post.UserID.Value != userId)
                {
                    string displayName = Session["Username"]?.ToString() ?? "Một bạn sinh viên";
                    string msg = $"{displayName} đã đánh giá {rating}⭐ bài \"{post.Title}\" của bạn.";
                    CreateNotification(
                        post.UserID.Value,
                        "connect",
                        msg,
                        $"/Connect/Details/{postId}"
                    );
                }
            }
            return RedirectToAction("Details", new { id = postId });
        }

        [HttpPost]
        public JsonResult AddReviewAjax(int postId, int rating, string comment)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập để đánh giá." });

            int userId = (int)Session["UserID"];
            string username = Session["Username"]?.ToString() ?? "Thành viên";

            if (rating < 1 || rating > 5)
                return Json(new { success = false, message = "Số sao đánh giá phải từ 1 đến 5 sao." });

            if (string.IsNullOrWhiteSpace(comment))
                return Json(new { success = false, message = "Nội dung nhận xét không được để trống." });

            bool hasReviewed = db.Reviews.Any(r => r.PostID == postId && r.PostType == "Connect" && r.UserID == userId);
            if (hasReviewed)
            {
                return Json(new { success = false, message = "Bạn đã đánh giá bài viết này rồi. Mỗi người chỉ được đánh giá 1 lần." });
            }

            var review = new Review
            {
                PostID = postId,
                PostType = "Connect",
                UserID = userId,
                RatingStar = rating,
                Comment = comment.Trim(),
                CreatedAt = DateTime.Now
            };
            db.Reviews.Add(review);
            db.SaveChanges();

            // Tìm chủ bài viết để gửi thông báo
            var post = db.ConnectPosts.FirstOrDefault(p => p.PostID == postId);
            if (post != null && post.UserID.HasValue && post.UserID.Value != userId)
            {
                string msg = $"{username} đã đánh giá {rating}⭐ bài \"{post.Title}\" của bạn.";
                CreateNotification(
                    post.UserID.Value,
                    "connect",
                    msg,
                    $"/Connect/Details/{postId}"
                );
            }

            var allReviews = db.Reviews.Where(r => r.PostID == postId && r.PostType == "Connect").ToList();
            double newAvg = allReviews.Any() ? Math.Round(allReviews.Average(r => r.RatingStar ?? 0), 1) : rating;
            int newCount = allReviews.Count;

            return Json(new
            {
                success = true,
                message = "Đánh giá của bạn đã được ghi nhận.",
                review = new
                {
                    reviewId = review.ReviewID,
                    username = username,
                    rating = rating,
                    comment = review.Comment,
                    createdAt = review.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm")
                },
                avgRating = newAvg,
                reviewCount = newCount
            });
        }

        // Nhắn tin với người đăng bài (Connect)
        public ActionResult ChatWithAuthor(int postId)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int myId = (int)Session["UserID"];

            var post = db.ConnectPosts.Find(postId);
            if (post == null || post.UserID == null) return HttpNotFound();

            int authorId = post.UserID.Value;
            if (authorId == myId)
            {
                TempData["Error"] = "Đây là bài viết của chính bạn.";
                return RedirectToAction("Details", new { id = postId });
            }

            // Tìm hoặc tạo phòng chat 1vs1
            var existingRoomId = db.ChatParticipants
                .Where(p => p.UserID == myId && p.ChatRoom.RoomType == "1vs1" && p.ChatRoom.IsActive == true)
                .Select(p => p.RoomID)
                .FirstOrDefault(rid => db.ChatParticipants.Any(p => p.RoomID == rid && p.UserID == authorId));

            int targetRoomId;
            if (existingRoomId != null && existingRoomId > 0)
            {
                targetRoomId = existingRoomId.Value;
                // Gửi tin nhắn trao đổi về bài viết vào cuộc trò chuyện
                db.ChatMessages.Add(new ChatMessage
                {
                    RoomID = targetRoomId,
                    SenderID = myId,
                    Content = $"Chào bạn! Mình đọc bài viết \"{post.Title}\" của bạn và muốn kết nối trao đổi.",
                    SentAt = DateTime.Now
                });
                db.SaveChanges();
            }
            else
            {
                var newRoom = new ChatRoom
                {
                    RoomType = "1vs1",
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };
                db.ChatRooms.Add(newRoom);
                db.SaveChanges();

                db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = myId, JoinedAt = DateTime.Now });
                db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = authorId, JoinedAt = DateTime.Now });

                // Gửi tin nhắn mở đầu về bài viết
                db.ChatMessages.Add(new ChatMessage
                {
                    RoomID = newRoom.RoomID,
                    SenderID = myId,
                    Content = $"Chào bạn! Mình đọc bài viết \"{post.Title}\" của bạn và muốn kết nối trao đổi.",
                    SentAt = DateTime.Now
                });

                db.SaveChanges();
                targetRoomId = newRoom.RoomID;
            }

            // Gửi thông báo cho tác giả bài viết
            string senderName = Session["Username"]?.ToString() ?? "Một bạn sinh viên";
            CreateNotification(
                authorId,
                "chat",
                $"{senderName} đã nhắn tin với bạn về bài \"{post.Title}\".",
                $"/Chat/Private/{targetRoomId}"
            );

            // Gửi SignalR invitation
            try
            {
                var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<StudentConnect.Hubs.ChatHub>();
                hubContext.Clients.Group("user_" + authorId).receiveChatInvitation(myId, senderName, targetRoomId, authorId);
            }
            catch { }

            return RedirectToAction("Private", "Chat", new { id = targetRoomId });
        }

        // ────────────────────────────────────────────────────────────────
        // 4. TRANG TẠO BÀI ĐĂNG MỚI (GET & POST)
        // ────────────────────────────────────────────────────────────────
        public ActionResult Create()
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");

            if (!db.ConnectCategories.Any(c => c.CategoryName == "Hỗ trợ"))
            {
                try
                {
                    db.ConnectCategories.Add(new ConnectCategory { CategoryName = "Hỗ trợ" });
                    db.SaveChanges();
                }
                catch { }
            }

            // Dòng này cực kỳ quan trọng để đổ dữ liệu vào Dropdown
            ViewBag.CategoryID = new SelectList(db.ConnectCategories, "CategoryID", "CategoryName");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ConnectPost post, HttpPostedFileBase ImageFile)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");

            if (ModelState.IsValid)
            {
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    string ext = System.IO.Path.GetExtension(ImageFile.FileName).ToLower();
                    if (ext == ".jpg" || ext == ".png" || ext == ".jpeg" || ext == ".webp")
                    {
                        string fileName = Guid.NewGuid().ToString() + ext;
                        string relativePath = "/Content/Uploads/Connect/" + fileName;
                        string fullPath = Server.MapPath("~" + relativePath);
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
                        ImageFile.SaveAs(fullPath);
                        post.MainImage = relativePath;
                    }
                }
                else
                {
                    post.MainImage = "/Content/Images/default-connect.jpg";
                }

                post.UserID = (int)Session["UserID"];
                post.CreatedAt = DateTime.Now;
                db.ConnectPosts.Add(post);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.CategoryID = new SelectList(db.ConnectCategories, "CategoryID", "CategoryName", post.CategoryID);
            return View(post);
        }

        // ────────────────────────────────────────────────────────────────
        // 5. TRANG SỬA BÀI VIẾT (GET & POST)
        // ────────────────────────────────────────────────────────────────
        public ActionResult Edit(int? id)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            if (id == null) return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);

            var post = db.ConnectPosts.Find(id);
            if (post == null) return HttpNotFound();

            int currentUserId = (int)Session["UserID"];
            if (post.UserID != currentUserId) return RedirectToAction("Index"); // Không phải chủ bài

            ViewBag.CategoryID = new SelectList(db.ConnectCategories, "CategoryID", "CategoryName", post.CategoryID);
            return View(post);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ConnectPost updatedPost, HttpPostedFileBase ImageFile)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int currentUserId = (int)Session["UserID"];

            if (ModelState.IsValid)
            {
                var existingPost = db.ConnectPosts.Find(updatedPost.PostID);
                if (existingPost == null || existingPost.UserID != currentUserId) return HttpNotFound();

                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    if (!string.IsNullOrEmpty(existingPost.MainImage) && existingPost.MainImage != "/Content/Images/default-connect.jpg")
                    {
                        string oldFilePath = Server.MapPath("~" + existingPost.MainImage);
                        if (System.IO.File.Exists(oldFilePath)) System.IO.File.Delete(oldFilePath);
                    }

                    string ext = System.IO.Path.GetExtension(ImageFile.FileName).ToLower();
                    if (ext == ".jpg" || ext == ".png" || ext == ".jpeg" || ext == ".webp")
                    {
                        string fileName = Guid.NewGuid().ToString() + ext;
                        string relativePath = "/Content/Uploads/Connect/" + fileName;
                        string fullPath = Server.MapPath("~" + relativePath);
                        ImageFile.SaveAs(fullPath);
                        existingPost.MainImage = relativePath;
                    }
                }

                existingPost.Title = updatedPost.Title;
                existingPost.Description = updatedPost.Description;
                existingPost.CategoryID = updatedPost.CategoryID;
                db.SaveChanges();
                return RedirectToAction("Details", new { id = existingPost.PostID });
            }
            ViewBag.CategoryID = new SelectList(db.ConnectCategories, "CategoryID", "CategoryName", updatedPost.CategoryID);
            return View(updatedPost);
        }

        // ────────────────────────────────────────────────────────────────
        // 6. XỬ LÝ XÓA BÀI VIẾT
        // ────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int currentUserId = (int)Session["UserID"];
            var post = db.ConnectPosts.Find(id);
            if (post != null && post.UserID == currentUserId)
            {
                var relatedReviews = db.Reviews.Where(r => r.PostID == id && r.PostType == "Connect").ToList();
                if (relatedReviews.Any())
                    db.Reviews.RemoveRange(relatedReviews);

                if (!string.IsNullOrEmpty(post.MainImage) && !post.MainImage.Contains("default"))
                {
                    string normalized = post.MainImage.StartsWith("/") ? "~" + post.MainImage : post.MainImage;
                    try
                    {
                        string fullPath = Server.MapPath(normalized);
                        if (System.IO.File.Exists(fullPath))
                            System.IO.File.Delete(fullPath);
                    }
                    catch { }
                }

                // ✅ Xóa thông báo liên quan
                DeleteRelatedNotifications("Connect", id);

                db.ConnectPosts.Remove(post);
                db.SaveChanges();
            }

            if (Request.UrlReferrer != null && Request.UrlReferrer.AbsolutePath.Contains("MyPosts"))
                return RedirectToAction("MyPosts");

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // bài đăng từ tk
        public ActionResult MyPosts()
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int userId = (int)Session["UserID"];

            // Phải có .Include("ConnectCategory") thì @item.ConnectCategory mới có dữ liệu
            var myPosts = db.ConnectPosts
                            .AsNoTracking()
                            .Include("ConnectCategory")
                            .Where(p => p.UserID == userId)
                            .OrderByDescending(p => p.CreatedAt)
                            .ToList();

            return View(myPosts);
        }
    }
}