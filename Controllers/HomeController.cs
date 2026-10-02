using System;
using System.Collections.Generic;
using StudentConnect.Helpers;
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
            int currentUserId = Session["UserID"] != null ? (int)Session["UserID"] : 0;
            var currentUser = currentUserId > 0 ? db.Users.Find(currentUserId) : null;
            ViewBag.CurrentUser = currentUser;

            var friendIds = currentUserId > 0 ? SocialHelper.GetFriendIds(currentUserId, db) : new List<int>();

            // 1. Bài viết cá nhân (Social Posts)
            var rawPosts = db.Posts
                .Include(p => p.User)
                .Include(p => p.User.UserProfiles)
                .Include(p => p.PostMedias)
                .Include(p => p.PostLikes)
                .Include(p => p.PostComments.Select(c => c.User))
                .OrderByDescending(p => p.CreatedAt)
                .Take(40)
                .ToList();

            // Exclude posts that the current user has hidden (per-user hide)
            List<int> hiddenIds = new List<int>();
            if (currentUserId > 0)
            {
                try
                {
                    hiddenIds = db.Database.SqlQuery<int>("SELECT PostID FROM UserHiddenPosts WHERE UserID = @uid", new System.Data.SqlClient.SqlParameter("@uid", currentUserId)).ToList();
                }
                catch { }
            }

            var socialPosts = rawPosts
                .Where(p => SocialHelper.CanViewPost(p, currentUserId, db, friendIds) && !hiddenIds.Contains(p.PostID))
                .Select(p => SocialHelper.MapToPostViewModel(p, currentUserId))
                .ToList();

            // Mark hidden flag on viewmodels
            if (currentUserId > 0 && hiddenIds.Any())
            {
                foreach (var vm in socialPosts)
                {
                    vm.IsHidden = hiddenIds.Contains(vm.PostID);
                }
            }

            // 1b. Bài viết được chia sẻ (Shared Posts)
            var rawShares = db.PostShares
                .Include(s => s.User)
                .Include(s => s.User.UserProfiles)
                .Include(s => s.Post)
                .Include(s => s.Post.User)
                .Include(s => s.Post.User.UserProfiles)
                .Include(s => s.Post.PostMedias)
                .Include(s => s.Post.PostLikes)
                .Include(s => s.Post.PostComments.Select(c => c.User))
                .OrderByDescending(s => s.CreatedAt)
                .Take(30)
                .ToList();

            var sharedPosts = rawShares
                .Where(s => s.Post != null && SocialHelper.CanViewPost(s.Post, currentUserId, db, friendIds) && !hiddenIds.Contains(s.Post.PostID))
                .Select(s => SocialHelper.MapShareToPostViewModel(s, currentUserId))
                .Where(vm => vm != null)
                .ToList();

            if (currentUserId > 0 && hiddenIds.Any())
            {
                foreach (var vm in sharedPosts)
                {
                    vm.IsHidden = hiddenIds.Contains(vm.PostID);
                }
            }

            // 2. Bài đăng Chợ sinh viên (Market Posts)
            var marketPosts = db.MarketPosts
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.MarketCategory)
                .Include(p => p.MarketImages)
                .Where(p => p.IsHide == false || p.IsHide == null)
                .OrderByDescending(p => p.CreatedAt)
                .Take(30)
                .ToList();

            if (currentUserId > 0 && hiddenIds.Any())
            {
                marketPosts = marketPosts.Where(p => !hiddenIds.Contains(p.PostID)).ToList();
            }

            // 3. Bài đăng Kết nối sinh viên (Connect Posts)
            var connectPosts = db.ConnectPosts
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.ConnectCategory)
                .OrderByDescending(p => p.CreatedAt)
                .Take(30)
                .ToList();

            // 4. Trộn lộn xộn các loại bài viết theo thời gian thực (như Facebook Newsfeed)
            var feedItems = new List<HomeFeedItemViewModel>();

            foreach (var p in socialPosts)
            {
                feedItems.Add(new HomeFeedItemViewModel
                {
                    ItemType = HomeFeedItemType.SocialPost,
                    CreatedAt = p.CreatedAt ?? DateTime.MinValue,
                    SocialPost = p
                });
            }

            foreach (var sp in sharedPosts)
            {
                feedItems.Add(new HomeFeedItemViewModel
                {
                    ItemType = HomeFeedItemType.SocialPost,
                    CreatedAt = sp.CreatedAt ?? DateTime.MinValue,
                    SocialPost = sp
                });
            }

            foreach (var m in marketPosts)
            {
                feedItems.Add(new HomeFeedItemViewModel
                {
                    ItemType = HomeFeedItemType.MarketPost,
                    CreatedAt = m.CreatedAt ?? DateTime.MinValue,
                    MarketPost = m
                });
            }

            foreach (var c in connectPosts)
            {
                feedItems.Add(new HomeFeedItemViewModel
                {
                    ItemType = HomeFeedItemType.ConnectPost,
                    CreatedAt = c.CreatedAt ?? DateTime.MinValue,
                    ConnectPost = c
                });
            }

            // Sắp xếp bài mới nhất lên trước
            var sortedFeed = feedItems
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .ToList();

            return View(sortedFeed);
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
