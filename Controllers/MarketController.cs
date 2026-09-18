using PagedList;
using StudentConnect.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class MarketController : BaseController
    {
        // ────────────────────────────────────────────────────────────────
        // 1. TRANG CHỦ CHỢ
        // ────────────────────────────────────────────────────────────────
        public ActionResult Index(string searchString, int? categoryId, int? page)
        {
            ViewBag.CategoryID = new SelectList(db.MarketCategories.AsNoTracking(), "CategoryID", "CategoryName", categoryId);
            ViewBag.CategoriesList = db.MarketCategories.AsNoTracking().ToList();
            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentCategory = categoryId;

            // Include thêm User và MarketCategory để hiện tên người đăng & danh mục
            var posts = db.MarketPosts
                          .AsNoTracking()
                          .Include("MarketImages")
                          .Include("User")
                          .Include("MarketCategory")
                          .Where(p => p.IsHide == false || p.IsHide == null)
                          .OrderByDescending(p => p.CreatedAt)
                          .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                posts = posts.Where(p =>
                    p.Title.Contains(searchString) ||
                    p.Description.Contains(searchString));

            if (categoryId.HasValue && categoryId.Value > 0)
                posts = posts.Where(p => p.CategoryID == categoryId.Value);

            int pageSize = 9;
            int pageNumber = page ?? 1;
            var pagedPosts = posts.ToPagedList(pageNumber, pageSize);

            if (Request.IsAjaxRequest())
            {
                return PartialView("_MarketPostsPartial", pagedPosts);
            }

            return View(pagedPosts);
        }

        public ActionResult GetPostsAjax(string searchString, int? categoryId, int? page)
        {
            ViewBag.CategoryID = new SelectList(db.MarketCategories.AsNoTracking(), "CategoryID", "CategoryName", categoryId);
            ViewBag.CategoriesList = db.MarketCategories.AsNoTracking().ToList();
            ViewBag.CurrentFilter = searchString;
            ViewBag.CurrentCategory = categoryId;

            var posts = db.MarketPosts
                          .AsNoTracking()
                          .Include("MarketImages")
                          .Include("User")
                          .Include("MarketCategory")
                          .Where(p => p.IsHide == false || p.IsHide == null)
                          .OrderByDescending(p => p.CreatedAt)
                          .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
                posts = posts.Where(p =>
                    p.Title.Contains(searchString) ||
                    p.Description.Contains(searchString));

            if (categoryId.HasValue && categoryId.Value > 0)
                posts = posts.Where(p => p.CategoryID == categoryId.Value);

            int pageSize = 9;
            int pageNumber = page ?? 1;

            return PartialView("_MarketPostsPartial", posts.ToPagedList(pageNumber, pageSize));
        }

        // ────────────────────────────────────────────────────────────────
        // 2. ĐĂNG TIN MỚI
        // ────────────────────────────────────────────────────────────────
        public ActionResult Create()
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            ViewBag.CategoryID = new SelectList(db.MarketCategories.AsNoTracking(), "CategoryID", "CategoryName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(MarketPost baiDangMoi, HttpPostedFileBase hinhAnh)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");

            if (ModelState.IsValid)
            {
                baiDangMoi.UserID = (int)Session["UserID"];
                baiDangMoi.CreatedAt = DateTime.Now;
                baiDangMoi.Status = 1;

                // Lưu ảnh nếu có
                if (hinhAnh != null && hinhAnh.ContentLength > 0)
                {
                    string savedPath = SaveImage(hinhAnh, "Market");
                    if (savedPath != null)
                        baiDangMoi.MainImage = savedPath;
                }

                db.MarketPosts.Add(baiDangMoi);
                db.SaveChanges();

                // Thêm vào MarketImages nếu có ảnh
                if (!string.IsNullOrEmpty(baiDangMoi.MainImage))
                {
                    db.MarketImages.Add(new MarketImage
                    {
                        PostID = baiDangMoi.PostID,
                        ImageUrl = baiDangMoi.MainImage
                    });
                    db.SaveChanges();
                }

                return RedirectToAction("Index");
            }

            ViewBag.CategoryID = new SelectList(db.MarketCategories.AsNoTracking(), "CategoryID", "CategoryName", baiDangMoi.CategoryID);
            return View(baiDangMoi);
        }
        // ────────────────────────────────────────────────────────────────
        // 3. CHI TIẾT SẢN PHẨM & BÌNH LUẬN
        // ────────────────────────────────────────────────────────────────
        public ActionResult Details(int id)
        {

            var monDo = db.MarketPosts
                          .Include("MarketImages")
                          .Include("User")
                          .Include("MarketCategory")
                          .FirstOrDefault(x => x.PostID == id);

            if (monDo == null)
            {
                TempData["DeletedPost"] = true;
                return RedirectToAction("Index");
            }

            monDo.ViewCount = (monDo.ViewCount ?? 0) + 1;
            db.SaveChanges();

            ViewBag.Comments = db.Reviews
                                 .AsNoTracking()
                                 .Include("User")
                                 .Where(r => r.PostID == id)
                                 .OrderBy(r => r.CreatedAt)
                                 .ToList();

            return View(monDo);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddComment(int PostID, string Content)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int currentUserId = (int)Session["UserID"];

            if (!string.IsNullOrWhiteSpace(Content))
            {
                var review = new Review
                {
                    PostID = PostID,
                    UserID = currentUserId,
                    Comment = Content,
                    CreatedAt = DateTime.Now,
                    PostType = "Market",
                    RatingStar = 5
                };
                db.Reviews.Add(review);
                db.SaveChanges();

                // 3. Xử lý thông báo cho chủ bài viết
                var post = db.MarketPosts.FirstOrDefault(p => p.PostID == PostID);

                // Kiểm tra: Bài viết tồn tại, có chủ và chủ không phải mình
                if (post != null && post.UserID.HasValue && post.UserID.Value != currentUserId)
                {
                    string commenterName = Session["Username"]?.ToString() ?? "Một thành viên";

                    // SỬA TẠI ĐÂY: Truyền post.UserID.Value thay vì post.PostID
                    CreateNotification(
                        post.UserID.Value, // ID người nhận (Khóa ngoại bảng Users)
                        "market",
                        $"{commenterName} đã bình luận bài \"{post.Title}\" của bạn.",
                        $"/Market/Details/{PostID}"
                    );
                }
            }

            return RedirectToAction("Details", new { id = PostID });
        }

        [HttpPost]
        public JsonResult AddCommentAjax(int postId, string content)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập để bình luận." });

            if (string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Nội dung bình luận không được để trống." });

            int currentUserId = (int)Session["UserID"];
            string username = Session["Username"]?.ToString() ?? "Thành viên";
            var user = db.Users.Find(currentUserId);
            string avatar = user?.Avatar;

            var post = db.MarketPosts.Include("User").FirstOrDefault(p => p.PostID == postId);
            if (post == null)
                return Json(new { success = false, message = "Bài viết không tồn tại." });

            var review = new Review
            {
                PostID = postId,
                UserID = currentUserId,
                Comment = content.Trim(),
                CreatedAt = DateTime.Now,
                PostType = "Market",
                RatingStar = 5
            };
            db.Reviews.Add(review);
            db.SaveChanges();

            // Gửi thông báo cho chủ bài viết
            if (post.UserID.HasValue && post.UserID.Value != currentUserId)
            {
                CreateNotification(
                    post.UserID.Value,
                    "market",
                    $"{username} đã bình luận bài \"{post.Title}\" của bạn.",
                    $"/Market/Details/{postId}"
                );
            }

            var commentData = new
            {
                reviewId = review.ReviewID,
                postId = postId,
                userId = currentUserId,
                username = username,
                avatar = avatar,
                comment = review.Comment,
                createdAt = review.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm"),
                isSeller = (post.UserID.HasValue && post.UserID.Value == currentUserId)
            };

            // SignalR real-time broadcast to anyone viewing this post
            try
            {
                var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<StudentConnect.Hubs.ChatHub>();
                hubContext.Clients.Group("market_post_" + postId).onNewMarketComment(commentData);
            }
            catch { }

            return Json(new { success = true, comment = commentData });
        }

        // ────────────────────────────────────────────────────────────────
        // 4. CHỈNH SỬA TIN
        // ────────────────────────────────────────────────────────────────
        public ActionResult Edit(int id)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");

            var monDo = db.MarketPosts.Find(id);
            if (monDo == null) return HttpNotFound();

            if (monDo.UserID != (int)Session["UserID"])
                return new HttpStatusCodeResult(403, "Bạn không có quyền sửa bài này.");

            ViewBag.CategoryID = new SelectList(db.MarketCategories, "CategoryID", "CategoryName", monDo.CategoryID);
            return View(monDo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(MarketPost monDoSua)
        {
            // ✅ FIX: Kiểm tra session
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");

            if (ModelState.IsValid)
            {
                var target = db.MarketPosts.Find(monDoSua.PostID);
                if (target == null) return HttpNotFound();

                // ✅ Kiểm tra quyền lần 2 ở server
                if (target.UserID != (int)Session["UserID"])
                    return new HttpStatusCodeResult(403);

                target.Title = monDoSua.Title;
                target.Price = monDoSua.Price;
                target.Description = monDoSua.Description;
                target.CategoryID = monDoSua.CategoryID;
                target.ExchangeLocation = monDoSua.ExchangeLocation;

                db.SaveChanges();
                return RedirectToAction("Details", new { id = target.PostID });
            }

            ViewBag.CategoryID = new SelectList(db.MarketCategories, "CategoryID", "CategoryName", monDoSua.CategoryID);
            return View(monDoSua);
        }

        // ────────────────────────────────────────────────────────────────
        // 5. XÓA TIN
        // ✅ FIX: Dùng POST thay GET để tránh xóa nhầm do crawler/link preview
        // ────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            var monDo = db.MarketPosts.Find(id);
            if (monDo == null) return HttpNotFound();
            if (monDo.UserID != (int)Session["UserID"])
                return new HttpStatusCodeResult(403);

            var anhs = db.MarketImages.Where(x => x.PostID == id).ToList();
            foreach (var img in anhs)
            {
                string fullPath = Server.MapPath("~" + img.ImageUrl);
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
                db.MarketImages.Remove(img);
            }

            // ✅ Xóa thông báo liên quan
            DeleteRelatedNotifications("Market", id);

            db.MarketPosts.Remove(monDo);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // ────────────────────────────────────────────────────────────────
        // HELPER: Lưu ảnh
        // ────────────────────────────────────────────────────────────────
        private string SaveImage(HttpPostedFileBase file, string folder)
        {
            try
            {
                var allowed = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                if (!allowed.Contains(file.ContentType.ToLower())) return null;
                if (file.ContentLength > 5 * 1024 * 1024) return null; // 5MB

                string fileName = Guid.NewGuid() + Path.GetExtension(file.FileName).ToLower();
                string dir = Server.MapPath($"~/Content/Uploads/{folder}/");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                file.SaveAs(Path.Combine(dir, fileName));
                return $"/Content/Uploads/{folder}/" + fileName;
            }
            catch { return null; }
        }

        // ────────────────────────────────────────────────────────────────
        // 6. WISHLIST & TRAO ĐỔI (Chức năng mở rộng)
        // ────────────────────────────────────────────────────────────────
        [HttpPost]
        public JsonResult ToggleWishlist(int postId)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập để lưu tin." });

            int myId = (int)Session["UserID"];
            var existing = db.MarketWishlists.FirstOrDefault(w => w.UserID == myId && w.PostID == postId);

            if (existing != null)
            {
                db.MarketWishlists.Remove(existing);
                db.SaveChanges();
                return Json(new { success = true, isSaved = false, message = "Đã bỏ lưu tin." });
            }
            else
            {
                db.MarketWishlists.Add(new MarketWishlist
                {
                    UserID = myId,
                    PostID = postId,
                    SavedAt = DateTime.Now
                });
                db.SaveChanges();
                return Json(new { success = true, isSaved = true, message = "Đã lưu tin vào danh sách yêu thích!" });
            }
        }

        public ActionResult MyWishlists(int? page)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int myId = (int)Session["UserID"];

            var postIds = db.MarketWishlists.Where(w => w.UserID == myId).Select(w => w.PostID).ToList();
            var savedPosts = db.MarketPosts
                               .Include("MarketCategory")
                               .Include("MarketImages")
                               .Include("User")
                               .Where(p => postIds.Contains(p.PostID) && (p.IsHide == false || p.IsHide == null))
                               .OrderByDescending(p => p.CreatedAt)
                               .AsQueryable();

            int pageSize = 9;
            int pageNumber = page ?? 1;
            return View("Index", savedPosts.ToPagedList(pageNumber, pageSize));
        }

        // Nhắn tin với người bán
        public ActionResult ChatWithSeller(int postId)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int myId = (int)Session["UserID"];

            var post = db.MarketPosts.Find(postId);
            if (post == null || post.UserID == null) return HttpNotFound();

            int sellerId = post.UserID.Value;
            if (sellerId == myId)
            {
                TempData["Error"] = "Đây là bài đăng của chính bạn.";
                return RedirectToAction("Details", new { id = postId });
            }

            // Ghi nhận MarketChatRoom
            var marketRoom = db.MarketChatRooms.FirstOrDefault(m => m.PostID == postId && m.BuyerID == myId && m.SellerID == sellerId);
            if (marketRoom == null)
            {
                marketRoom = new MarketChatRoom
                {
                    PostID = postId,
                    BuyerID = myId,
                    SellerID = sellerId,
                    CreatedAt = DateTime.Now
                };
                db.MarketChatRooms.Add(marketRoom);
                db.SaveChanges();
            }

            // Tìm hoặc tạo phòng chat 1vs1
            var existingRoomId = db.ChatParticipants
                .Where(p => p.UserID == myId && p.ChatRoom.RoomType == "1vs1" && p.ChatRoom.IsActive == true)
                .Select(p => p.RoomID)
                .FirstOrDefault(rid => db.ChatParticipants.Any(p => p.RoomID == rid && p.UserID == sellerId));

            int targetRoomId;
            if (existingRoomId != null && existingRoomId > 0)
            {
                targetRoomId = existingRoomId.Value;
                // Gửi tin nhắn hỏi về món đồ vào cuộc trò chuyện
                db.ChatMessages.Add(new ChatMessage
                {
                    RoomID = targetRoomId,
                    SenderID = myId,
                    Content = $"Chào bạn! Mình quan tâm đến món đồ: \"{post.Title}\" ({(post.Price.HasValue ? post.Price.Value.ToString("N0") + "đ" : "Thỏa thuận")}).",
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
                db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = sellerId, JoinedAt = DateTime.Now });

                // Gửi tin nhắn mở đầu về món đồ
                db.ChatMessages.Add(new ChatMessage
                {
                    RoomID = newRoom.RoomID,
                    SenderID = myId,
                    Content = $"Chào bạn! Mình quan tâm đến món đồ: \"{post.Title}\" ({post.Price?.ToString("N0")}đ).",
                    SentAt = DateTime.Now
                });

                db.SaveChanges();
                targetRoomId = newRoom.RoomID;
            }

            // Gửi thông báo cho người bán kèm link dẫn trực tiếp đến đoạn chat
            string buyerName = Session["Username"]?.ToString() ?? "Một người mua";
            CreateNotification(
                sellerId,
                "chat",
                $"{buyerName} đã nhắn tin quan tâm đến món đồ \"{post.Title}\" của bạn.",
                $"/Chat/Private/{targetRoomId}"
            );

            // Gửi SignalR notification/invitation
            try
            {
                var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<StudentConnect.Hubs.ChatHub>();
                hubContext.Clients.Group("user_" + sellerId).receiveChatInvitation(myId, buyerName, targetRoomId, sellerId);
            }
            catch { }

            return RedirectToAction("Private", "Chat", new { id = targetRoomId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // bài đăng của tôi
        public ActionResult MyMarketPosts(int? page)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            ViewBag.CategoryID = new SelectList(db.MarketCategories, "CategoryID", "CategoryName");

            int userId = (int)Session["UserID"];

            var myPosts = db.MarketPosts
                            .Include("MarketCategory")
                            .Include("MarketImages")
                            .Where(p => p.UserID == userId)
                            .OrderByDescending(p => p.CreatedAt)
                            .AsQueryable();

            int pageSize = 10;
            int pageNumber = page ?? 1;

            return View(myPosts.ToPagedList(pageNumber, pageSize));
        }
    }
}

