using StudentConnect.Models;
using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StudentConnect.Areas.Admin.Controllers
{
    public class AdminMarketController : AdminBaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (filterContext.Result != null) return;
            AuthorizeRole(filterContext, "AdminMarket");
        }

        private static readonly string[] AllowedImageExts = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB

        // ── helpers ──────────────────────────────────────────────────
        private bool IsValidImage(HttpPostedFileBase file, out string error)
        {
            error = null;
            var ext = Path.GetExtension(file.FileName ?? "").ToLower();
            if (!AllowedImageExts.Contains(ext))
            { error = "Chỉ chấp nhận ảnh JPG, PNG, WEBP, GIF."; return false; }
            if (file.ContentLength > MaxFileSizeBytes)
            { error = "Ảnh không được vượt quá 5MB."; return false; }
            return true;
        }

        private string SaveImage(HttpPostedFileBase file)
        {
            string ext = Path.GetExtension(file.FileName).ToLower();
            string fileName = Guid.NewGuid() + ext;
            string dir = Server.MapPath("~/Content/Uploads/Market/");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            file.SaveAs(Path.Combine(dir, fileName));
            return "/Content/Uploads/Market/" + fileName;
        }

        private void DeletePhysicalFile(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains("default")) return;
            try
            {
                string full = Server.MapPath("~" + relativePath);
                if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("DeletePhysicalFile failed: " + ex.Message);
            }
        }

        private int? GetAdminUserId()
        {
            return Session["UserID"] as int?;
        }

        // ── INDEX ─────────────────────────────────────────────────────
        public ActionResult Index()
        {
            ViewBag.MarketCategories = new SelectList(
                db.MarketCategories.AsNoTracking(), "CategoryID", "CategoryName");

            var posts = db.MarketPosts
                          .AsNoTracking()
                          .Include("MarketCategory")
                          .Include("User")
                          .OrderByDescending(p => p.CreatedAt)
                          .Take(200)
                          .ToList();

            return View(posts);
        }

        // ── CREATE ────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(MarketPost post, HttpPostedFileBase fileAnh)
        {
            // Validate session
            var adminId = GetAdminUserId();
            if (adminId == null)
            {
                TempData["Error"] = "Phiên đăng nhập hết hạn, vui lòng đăng nhập lại.";
                return RedirectToAction("Index");
            }

            // Validate ModelState
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ: " +
                    string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                return RedirectToAction("Index");
            }

            try
            {
                if (fileAnh != null && fileAnh.ContentLength > 0)
                {
                    if (!IsValidImage(fileAnh, out string imgErr))
                    { TempData["Error"] = imgErr; return RedirectToAction("Index"); }

                    post.MainImage = SaveImage(fileAnh);
                }
                else
                {
                    post.MainImage = "/Content/Images/default-market.jpg";
                }

                post.CreatedAt = DateTime.Now;
                post.UserID = adminId.Value;

                db.MarketPosts.Add(post);
                db.SaveChanges();

                TempData["Success"] = $"Đã thêm bài viết \"{post.Title}\" thành công.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("AdminMarket.Create: " + ex.Message);
                TempData["Error"] = "Lỗi hệ thống, vui lòng thử lại.";
            }

            return RedirectToAction("Index");
        }

        // ── EDIT ──────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(MarketPost post, HttpPostedFileBase fileAnh)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ: " +
                    string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                return RedirectToAction("Index");
            }

            try
            {
                var existing = db.MarketPosts.Find(post.PostID);
                if (existing == null)
                {
                    TempData["Error"] = "Không tìm thấy bài viết!";
                    return RedirectToAction("Index");
                }

                existing.Title = post.Title;
                existing.CategoryID = post.CategoryID;
                existing.Price = post.Price;
                existing.Description = post.Description;
                existing.ExchangeLocation = post.ExchangeLocation;

                if (fileAnh != null && fileAnh.ContentLength > 0)
                {
                    if (!IsValidImage(fileAnh, out string imgErr))
                    { TempData["Error"] = imgErr; return RedirectToAction("Index"); }

                    DeletePhysicalFile(existing.MainImage);
                    existing.MainImage = SaveImage(fileAnh);
                }

                db.SaveChanges();
                TempData["Success"] = $"Đã cập nhật bài viết \"{existing.Title}\" thành công.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("AdminMarket.Edit: " + ex.Message);
                TempData["Error"] = "Lỗi hệ thống, vui lòng thử lại.";
            }

            return RedirectToAction("Index");
        }

        // ── DELETE ────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            try
            {
                var post = db.MarketPosts.Find(id);
                if (post == null)
                {
                    TempData["Error"] = "Không tìm thấy bài viết!";
                    return RedirectToAction("Index");
                }

                string title = post.Title;

                DeletePhysicalFile(post.MainImage);

                // Xóa ảnh phụ kèm file vật lý
                var images = db.MarketImages.Where(i => i.PostID == id).ToList();
                foreach (var img in images) DeletePhysicalFile(img.ImageUrl);
                if (images.Any()) db.MarketImages.RemoveRange(images);

                // Xóa wishlist & chat rooms
                var wishlists = db.MarketWishlists.Where(w => w.PostID == id).ToList();
                if (wishlists.Any()) db.MarketWishlists.RemoveRange(wishlists);

                var chatRooms = db.MarketChatRooms.Where(c => c.PostID == id).ToList();
                if (chatRooms.Any()) db.MarketChatRooms.RemoveRange(chatRooms);

                // Xóa thông báo liên quan
                DeleteRelatedNotifications("Market", id);

                db.MarketPosts.Remove(post);
                db.SaveChanges();

                TempData["Success"] = $"Đã xóa bài viết \"{title}\" thành công.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("AdminMarket.Delete: " + ex.Message);
                TempData["Error"] = "Lỗi hệ thống khi xóa, vui lòng thử lại.";
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}