using StudentConnect.Models;
using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity; // Thêm thư viện này để dùng được hàm .Include()

namespace StudentConnect.Areas.Admin.Controllers
{
    // Bắt buộc kế thừa AdminBaseController để khóa những ai chưa đăng nhập
    public class AdminConnectController : AdminBaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (filterContext.Result != null) return;
            AuthorizeRole(filterContext, "AdminConnect");
        }

        // ==============================================================================
        // 1. GET: Admin/AdminConnect/Index (Load danh sách & Dữ liệu cho Modal)
        // ==============================================================================
        public ActionResult Index()
        {
            // Lấy toàn bộ bài viết Khám phá, sắp xếp bài mới nhất lên đầu tiên
            // Bao gồm luôn thông tin Category và User để hiển thị
            var posts = db.ConnectPosts.AsNoTracking().Include("ConnectCategory").Include("User").OrderByDescending(p => p.CreatedAt).Take(200).ToList();

            // Đẩy danh sách Danh mục sang ViewBag để làm Dropdown (Select list) trong Modal Thêm/Sửa
            ViewBag.Categories = new SelectList(db.ConnectCategories.AsNoTracking().ToList(), "CategoryID", "CategoryName");

            return View(posts);
        }

        // ==============================================================================
        // 2. POST: Admin/AdminConnect/Create (Xử lý form Thêm mới từ Modal)
        // ==============================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ConnectPost post, HttpPostedFileBase fileAnh)
        {
            try
            {
                // Xử lý upload ảnh
                if (fileAnh != null && fileAnh.ContentLength > 0)
                {
                    string ext = Path.GetExtension(fileAnh.FileName).ToLower();
                    string fileName = Guid.NewGuid().ToString() + ext; // Dùng Guid cho tên file giống Market
                    string directoryPath = Server.MapPath("~/Content/Images/Connect/");

                    // Tự động tạo thư mục nếu chưa có
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    string path = Path.Combine(directoryPath, fileName);
                    fileAnh.SaveAs(path);
                    post.MainImage = "/Content/Images/Connect/" + fileName;
                }
                else
                {
                    // Ảnh mặc định nếu không chọn
                    post.MainImage = "/Content/Images/default-image.jpg";
                }

                post.CreatedAt = DateTime.Now;

                // Gán UserID của người đăng (Admin)
                post.UserID = Session["UserID"] != null ? (int)Session["UserID"] : 1;

                db.ConnectPosts.Add(post);
                db.SaveChanges();

                TempData["Success"] = "Thêm bài viết mới thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ==============================================================================
        // 3. POST: Admin/AdminConnect/Edit (Xử lý form Cập nhật từ Modal)
        // ==============================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ConnectPost post, HttpPostedFileBase fileAnh)
        {
            try
            {
                var existingPost = db.ConnectPosts.Find(post.PostID);
                if (existingPost != null)
                {
                    existingPost.Title = post.Title;
                    existingPost.Description = post.Description;
                    existingPost.CategoryID = post.CategoryID;

                    // Xử lý upload ảnh mới
                    if (fileAnh != null && fileAnh.ContentLength > 0)
                    {
                        // 1. Xóa ảnh cũ vật lý đi cho đỡ rác server (trừ ảnh default)
                        if (!string.IsNullOrEmpty(existingPost.MainImage) && !existingPost.MainImage.Contains("default"))
                        {
                            try
                            {
                                string oldPath = Server.MapPath("~" + existingPost.MainImage);
                                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                            }
                            catch { }
                        }

                        // 2. Lưu ảnh mới
                        string ext = Path.GetExtension(fileAnh.FileName).ToLower();
                        string fileName = Guid.NewGuid().ToString() + ext;
                        string directoryPath = Server.MapPath("~/Content/Images/Connect/");

                        if (!Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        string path = Path.Combine(directoryPath, fileName);
                        fileAnh.SaveAs(path);
                        existingPost.MainImage = "/Content/Images/Connect/" + fileName;
                    }

                    db.SaveChanges();
                    TempData["Success"] = "Cập nhật bài viết thành công!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ==============================================================================
        // 4. POST: Admin/AdminConnect/Delete (Xóa dữ liệu bằng Entity Framework)
        // ==============================================================================
        [HttpPost]
        public ActionResult Delete(int id)
        {
            try
            {
                var post = db.ConnectPosts.Find(id);
                if (post != null)
                {
                    // 1. Tìm và xóa các dữ liệu con (Review) liên quan trước
                    // Điều kiện: Review thuộc PostID này và PostType là "Connect"
                    var relatedReviews = db.Reviews.Where(r => r.PostID == id && r.PostType == "Connect").ToList();
                    if (relatedReviews.Count > 0)
                    {
                        db.Reviews.RemoveRange(relatedReviews);
                    }

                    // 2. Dọn dẹp file ảnh trong thư mục để tránh rác ổ cứng
                    if (!string.IsNullOrEmpty(post.MainImage) && !post.MainImage.Contains("default"))
                    {
                        try
                        {
                            string fullPath = Server.MapPath("~" + post.MainImage);
                            if (System.IO.File.Exists(fullPath))
                            {
                                System.IO.File.Delete(fullPath);
                            }
                        }
                        catch { }
                    }

                    // 3. Xóa thông báo liên quan
                    DeleteRelatedNotifications("Connect", id);

                    // 4. Cuối cùng mới xóa bài viết chính
                    db.ConnectPosts.Remove(post);

                    // Lưu mọi thay đổi xuống database
                    db.SaveChanges();

                    TempData["Success"] = "Đã xóa bài viết và các dữ liệu liên quan thành công!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ==============================================================================
        // Dọn dẹp kết nối Database
        // ==============================================================================
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}