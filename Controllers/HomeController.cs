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
            int currentUserId = (Session["UserID"] != null && Session["Username"] != null) ? (int)Session["UserID"] : 0;
            
            // 1. Tải thông tin người dùng hiện tại kèm Profile (AsNoTracking để giảm overhead tracking)
            var currentUser = currentUserId > 0 
                ? db.Users.AsNoTracking().Include(u => u.UserProfiles).FirstOrDefault(u => u.UserID == currentUserId) 
                : null;
            ViewBag.CurrentUser = currentUser;

            var userProfile = currentUser?.UserProfiles?.FirstOrDefault();
            string myFaculty = userProfile?.Faculty?.Trim();
            string myMajor = userProfile?.Major?.Trim();
            bool isCurrentUserAdmin = currentUser != null && (currentUser.IsAdmin == true);

            // 2. Gom nhóm quan hệ bạn bè trong 1 truy vấn duy nhất (AsNoTracking)
            List<int> friendIds = new List<int>();
            List<int> pendingIds = new List<int>();
            List<int> blockedIds = new List<int>();

            if (currentUserId > 0)
            {
                SocialHelper.GetFriendshipOverview(currentUserId, db, out friendIds, out pendingIds, out blockedIds);
            }

            // 3. Lấy gợi ý bạn bè (tái sử dụng friendIds, pendingIds, blockedIds, myFaculty, myMajor - 0 truy vấn lặp)
            ViewBag.FriendSuggestions = SocialHelper.GetFriendSuggestions(
                currentUserId, db, 12, 0,
                cachedFriendIds: friendIds,
                cachedPendingIds: pendingIds,
                cachedBlockedIds: blockedIds,
                myFaculty: myFaculty,
                myMajor: myMajor);

            // 4. Bài viết cá nhân (Social Posts) - Bổ sung AsNoTracking() và Include PostShares
            var rawPosts = db.Posts
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.User.UserProfiles)
                .Include(p => p.PostMedias)
                .Include(p => p.PostLikes)
                .Include(p => p.PostShares)
                .Include(p => p.PostComments.Select(c => c.User))
                .Include(p => p.PostComments.Select(c => c.PostComments1.Select(r => r.User)))
                .OrderByDescending(p => p.CreatedAt)
                .Take(20)
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

            // 1b. Bài viết được chia sẻ (Shared Posts) - Bổ sung AsNoTracking() và Include PostShares
            var rawShares = db.PostShares
                .AsNoTracking()
                .Include(s => s.User)
                .Include(s => s.User.UserProfiles)
                .Include(s => s.Post)
                .Include(s => s.Post.User)
                .Include(s => s.Post.User.UserProfiles)
                .Include(s => s.Post.PostMedias)
                .Include(s => s.Post.PostLikes)
                .Include(s => s.Post.PostComments.Select(c => c.User))
                .Include(s => s.Post.PostComments.Select(c => c.PostComments1.Select(r => r.User)))
                .OrderByDescending(s => s.CreatedAt)
                .Take(15)
                .ToList();

            var sharedPosts = rawShares
                .Where(s => s.Post != null && SocialHelper.CanViewPost(s.Post, currentUserId, db, friendIds) && !hiddenIds.Contains(s.Post.PostID))
                .Select(s => SocialHelper.MapShareToPostViewModel(s, currentUserId))
                .Where(vm => vm != null)
                .ToList();

            // Tính số lượt chia sẻ cho các bài gốc an toàn (tránh circular Include gây crash EF6 RelatedEnd)
            var sharedOriginalPostIds = sharedPosts
                .Where(vm => vm.SharedPost != null && vm.SharedPost.OriginalPostID > 0)
                .Select(vm => vm.SharedPost.OriginalPostID)
                .Distinct()
                .ToList();

            if (sharedOriginalPostIds.Any())
            {
                var shareCounts = db.PostShares
                    .AsNoTracking()
                    .Where(s => sharedOriginalPostIds.Contains(s.PostID))
                    .GroupBy(s => s.PostID)
                    .Select(g => new { PostID = g.Key, Count = g.Count() })
                    .ToDictionary(x => x.PostID, x => x.Count);

                foreach (var vm in sharedPosts)
                {
                    int sCount;
                    if (vm.SharedPost != null && shareCounts.TryGetValue(vm.SharedPost.OriginalPostID, out sCount))
                    {
                        vm.ShareCount = sCount;
                    }
                }
            }

            // Tái sử dụng cờ isCurrentUserAdmin tươi từ currentUser DB, không bắn thêm query Users.Any
            SocialHelper.ApplyCommentModeration(socialPosts.Concat(sharedPosts), db, currentUserId, isCurrentUserAdmin);

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

        [HttpGet]
        public JsonResult SearchFriends(string q, int page = 1, int pageSize = 8)
        {
            int currentUserId = (Session["UserID"] != null && Session["Username"] != null) ? (int)Session["UserID"] : 0;
            if (pageSize <= 0 || pageSize > 20) pageSize = 8;
            if (page <= 0) page = 1;

            var result = SocialHelper.SearchUsers(currentUserId, q, db, page, pageSize);
            bool hasMore = (page * pageSize) < result.Item2;

            return Json(new
            {
                success = true,
                users = result.Item1,
                total = result.Item2,
                hasMore = hasMore,
                page = page
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetFriendSuggestions(int take = 10, int skip = 0)
        {
            int currentUserId = (Session["UserID"] != null && Session["Username"] != null) ? (int)Session["UserID"] : 0;
            if (take <= 0 || take > 20) take = 10;
            if (skip < 0) skip = 0;

            var suggestions = SocialHelper.GetFriendSuggestions(currentUserId, db, take, skip);

            return Json(new
            {
                success = true,
                users = suggestions
            }, JsonRequestBehavior.AllowGet);
        }
    }
}
