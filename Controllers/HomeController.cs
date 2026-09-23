using StudentConnect.Models;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class HomeController : BaseController
    {
        public ActionResult Index()
        {
            ViewBag.LatestMarket = db.MarketPosts
                .AsNoTracking()
                .Include("User")
                .Include("MarketCategory")
                .Include("MarketImages")
                .Where(p => p.IsHide == false || p.IsHide == null)
                .OrderByDescending(p => p.CreatedAt)
                .Take(4)
                .ToList();

            ViewBag.LatestConnect = db.ConnectPosts
                .AsNoTracking()
                .Include("User")
                .Include("ConnectCategory")
                .OrderByDescending(p => p.CreatedAt)
                .Take(4)
                .ToList();

            ViewBag.TotalMarket = db.MarketPosts.Count(p => p.IsHide == false || p.IsHide == null);
            ViewBag.TotalConnect = db.ConnectPosts.Count();

            return View();
        }

        [HttpGet]
        public JsonResult GetLatestMarket(int skip = 0, int take = 4)
        {
            if (take > 12) take = 12;
            var total = db.MarketPosts.Count(p => p.IsHide == false || p.IsHide == null);
            var items = db.MarketPosts
                .AsNoTracking()
                .Include("User")
                .Include("MarketCategory")
                .Include("MarketImages")
                .Where(p => p.IsHide == false || p.IsHide == null)
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList()
                .Select(p => new
                {
                    PostID = p.PostID,
                    Title = p.Title ?? "",
                    Price = p.Price ?? 0,
                    Category = p.MarketCategory?.CategoryName ?? "Chợ",
                    Image = (p.MarketImages != null && p.MarketImages.Any())
                        ? p.MarketImages.First().ImageUrl
                        : (p.MainImage ?? ""),
                    Username = p.User?.Username ?? "Ẩn danh",
                    CreatedAt = p.CreatedAt.HasValue ? p.CreatedAt.Value.ToString("dd/MM") : ""
                });

            return Json(new { success = true, total = total, items = items }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetLatestConnect(int skip = 0, int take = 4)
        {
            if (take > 12) take = 12;
            var total = db.ConnectPosts.Count();
            var items = db.ConnectPosts
                .AsNoTracking()
                .Include("User")
                .Include("ConnectCategory")
                .OrderByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList()
                .Select(p => new
                {
                    PostID = p.PostID,
                    Title = p.Title ?? "",
                    Category = p.ConnectCategory?.CategoryName ?? "Kết nối",
                    Image = p.MainImage ?? "",
                    Username = p.User?.Username ?? "Ẩn danh",
                    CreatedAt = p.CreatedAt.HasValue ? p.CreatedAt.Value.ToString("dd/MM") : ""
                });

            return Json(new { success = true, total = total, items = items }, JsonRequestBehavior.AllowGet);
        }
    }
}
