using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StudentConnect.Helpers;
using StudentConnect.Models;

namespace StudentConnect.Controllers
{
    public class StoryController : BaseController
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".webm", ".mov" };
        private const int MaxFileSize = 25 * 1024 * 1024; // 25MB

        // GET: Story (Trang toàn màn hình hoặc danh sách)
        public ActionResult Index()
        {
            int currentUserId = Session["UserID"] != null ? (int)Session["UserID"] : 0;
            var groups = LoadActiveStoryGroups(currentUserId);
            return View(groups);
        }

        /// <summary>
        /// Child Action dùng để nhúng trực tiếp Khay Stories vào Trang chủ hoặc Bảng tin
        /// </summary>
        [ChildActionOnly]
        public ActionResult StoryTrayPartial()
        {
            int currentUserId = Session["UserID"] != null ? (int)Session["UserID"] : 0;
            var groups = LoadActiveStoryGroups(currentUserId);
            return PartialView("_StoryTrayPartial", groups);
        }

        /// <summary>
        /// API lấy dữ liệu khay Stories (dùng cho AJAX load trên Trang chủ / Bảng tin)
        /// </summary>
        [HttpGet]
        public JsonResult GetStoryTray()
        {
            int currentUserId = Session["UserID"] != null ? (int)Session["UserID"] : 0;
            var groups = LoadActiveStoryGroups(currentUserId);

            var result = groups.Select(g => new
            {
                userId = g.UserID,
                username = g.Username,
                avatar = g.Avatar,
                isCurrentUser = g.IsCurrentUser,
                hasUnviewedStories = g.HasUnviewedStories,
                storyCount = g.Stories.Count,
                latestThumbnail = g.LatestStory?.MediaUrl ?? "",
                latestCaption = g.LatestStory?.Caption ?? "",
                timeAgo = g.LatestStory?.TimeAgo ?? ""
            });

            return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// API lấy chi tiết các story của một người dùng để phát trong trình xem (Story Viewer)
        /// </summary>
        [HttpGet]
        public JsonResult GetUserStories(int userId)
        {
            int currentUserId = Session["UserID"] != null ? (int)Session["UserID"] : 0;
            var now = DateTime.Now;

            var user = db.Users
                .Include(u => u.UserProfiles)
                .FirstOrDefault(u => u.UserID == userId);

            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại!" }, JsonRequestBehavior.AllowGet);
            }

            var stories = db.Stories
                .Include(s => s.StoryViews)
                .Include("StoryViews.User")
                .Where(s => s.UserID == userId && s.ExpiresAt > now)
                .OrderBy(s => s.CreatedAt)
                .ToList();

            if (!stories.Any())
            {
                return Json(new { success = false, message = "Người dùng này chưa có tin nào trong 24 giờ qua!" }, JsonRequestBehavior.AllowGet);
            }

            bool isOwner = (currentUserId > 0 && currentUserId == userId);

            var storyList = stories.Select(s =>
            {
                string ext = Path.GetExtension(s.MediaUrl ?? "").ToLower();
                bool isVideo = ext == ".mp4" || ext == ".webm" || ext == ".mov";

                bool hasViewed = currentUserId > 0 && s.StoryViews.Any(v => v.ViewerID == currentUserId);

                // Nếu là chủ sở hữu tin, trả về danh sách người xem
                object viewers = null;
                if (isOwner)
                {
                    var storyReactions = StoryHelper.GetReactions(s.StoryID);
                    viewers = s.StoryViews
                        .OrderByDescending(v => v.ViewedAt)
                        .Select(v => new
                        {
                            userId = v.ViewerID,
                            username = v.User?.DisplayName ?? "Người dùng",
                            avatar = v.User?.SafeAvatar ?? "/Content/Images/default-avatar.png",
                            viewedAt = v.ViewedAt.HasValue ? SocialHelper.FormatTimeAgo(v.ViewedAt) : "Vừa xong",
                            reaction = storyReactions.FirstOrDefault(r => r.UserID == v.ViewerID)?.Emoji
                        }).ToList();
                }

                return new
                {
                    storyId = s.StoryID,
                    userId = s.UserID,
                    mediaUrl = s.MediaUrl,
                    mediaType = isVideo ? "video" : "image",
                    caption = s.Caption ?? "",
                    createdAt = s.CreatedAt.HasValue ? s.CreatedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : "",
                    timeAgo = SocialHelper.FormatTimeAgo(s.CreatedAt),
                    totalViews = s.StoryViews.Count,
                    hasViewed = hasViewed,
                    isOwner = isOwner,
                    viewers = viewers
                };
            }).ToList();

            return Json(new
            {
                success = true,
                user = new
                {
                    userId = user.UserID,
                    username = user.DisplayName,
                    avatar = user.SafeAvatar,
                    isCurrentUser = isOwner
                },
                stories = storyList
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Đăng tin mới (ảnh hoặc video) tự động biến mất sau 24 giờ
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateStoryInputModel model)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để đăng tin!" });
            }

            int currentUserId = (int)Session["UserID"];

            if (model.MediaFile == null || model.MediaFile.ContentLength == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn hình ảnh hoặc video cho tin của bạn!" });
            }

            if (model.MediaFile.ContentLength > MaxFileSize)
            {
                return Json(new { success = false, message = "Kích thước tập tin tối đa là 25MB!" });
            }

            string ext = Path.GetExtension(model.MediaFile.FileName)?.ToLower();
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            {
                return Json(new { success = false, message = "Định dạng tập tin không hỗ trợ. Vui lòng chọn ảnh (JPG, PNG, GIF, WEBP) hoặc video (MP4, WEBM)!" });
            }

            try
            {
                string uploadDir = Server.MapPath("~/Content/Uploads/Stories/");
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                string fileName = $"story_{currentUserId}_{DateTime.Now.Ticks}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
                string savePath = Path.Combine(uploadDir, fileName);
                model.MediaFile.SaveAs(savePath);

                string mediaUrl = "/Content/Uploads/Stories/" + fileName;

                // Kiểm duyệt từ ngữ trong caption nếu có
                string censoredCaption = null;
                if (!string.IsNullOrWhiteSpace(model.Caption))
                {
                    censoredCaption = WordModerationHelper.Moderate(model.Caption.Trim()).CleanText;
                    if (censoredCaption.Length > 500)
                    {
                        censoredCaption = censoredCaption.Substring(0, 500);
                    }
                }

                var now = DateTime.Now;
                var story = new Story
                {
                    UserID = currentUserId,
                    MediaUrl = mediaUrl,
                    Caption = censoredCaption,
                    CreatedAt = now,
                    ExpiresAt = now.AddHours(24) // Tự động hết hạn sau 24 giờ
                };

                db.Stories.Add(story);
                db.SaveChanges();

                // Gửi thông báo SignalR cập nhật Story Tray cho các người dùng đang online
                try
                {
                    var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<StudentConnect.Hubs.ChatHub>();
                    var currentUser = db.Users.Find(currentUserId);
                    hubContext.Clients.All.onNewStoryPosted(new
                    {
                        userId = currentUserId,
                        username = currentUser?.DisplayName,
                        avatar = currentUser?.SafeAvatar,
                        storyId = story.StoryID
                    });
                }
                catch { }

                return Json(new
                {
                    success = true,
                    message = "Đăng tin 24h thành công! Tin sẽ tự động biến mất sau 24 giờ.",
                    storyId = story.StoryID,
                    mediaUrl = story.MediaUrl
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi đăng tin: " + ex.Message });
            }
        }

        /// <summary>
        /// Ghi nhận lượt xem story (không ghi nhận trùng cho cùng một user)
        /// </summary>
        [HttpPost]
        public JsonResult MarkViewed(int storyId)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false });
            }

            int currentUserId = (int)Session["UserID"];
            var now = DateTime.Now;

            var story = db.Stories.FirstOrDefault(s => s.StoryID == storyId && s.ExpiresAt > now);
            if (story == null)
            {
                return Json(new { success = false, message = "Tin không tồn tại hoặc đã hết hạn" });
            }

            // Tự xem tin của mình thì không tính vào bảng StoryViews
            if (story.UserID == currentUserId)
            {
                return Json(new { success = true, isOwner = true });
            }

            bool alreadyViewed = db.StoryViews.Any(v => v.StoryID == storyId && v.ViewerID == currentUserId);
            if (!alreadyViewed)
            {
                var view = new StoryView
                {
                    StoryID = storyId,
                    ViewerID = currentUserId,
                    ViewedAt = now
                };
                db.StoryViews.Add(view);
                db.SaveChanges();
            }

            return Json(new { success = true });
        }

        /// <summary>
        /// Xóa tin của chính mình hoặc admin xóa
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Delete(int id)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            int currentUserId = (int)Session["UserID"];
            var currentUser = db.Users.Find(currentUserId);

            var story = db.Stories.Include(s => s.StoryViews).FirstOrDefault(s => s.StoryID == id);
            if (story == null)
            {
                return Json(new { success = false, message = "Tin không tồn tại hoặc đã bị xóa!" });
            }

            bool isOwner = story.UserID == currentUserId;
            bool isAdmin = currentUser != null && currentUser.IsAdmin;

            if (!isOwner && !isAdmin)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa tin này!" });
            }

            try
            {
                // Xóa file vật lý trên server nếu có
                if (!string.IsNullOrEmpty(story.MediaUrl))
                {
                    string filePath = Server.MapPath("~" + story.MediaUrl);
                    if (System.IO.File.Exists(filePath))
                    {
                        try { System.IO.File.Delete(filePath); } catch { }
                    }
                }

                // Xóa các lượt xem liên quan
                if (story.StoryViews != null && story.StoryViews.Any())
                {
                    db.StoryViews.RemoveRange(story.StoryViews);
                }

                db.Stories.Remove(story);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa tin thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xóa tin: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách người đã xem tin dành cho chủ sở hữu
        /// </summary>
        [HttpGet]
        public JsonResult GetStoryViewers(int id)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" }, JsonRequestBehavior.AllowGet);
            }

            int currentUserId = (int)Session["UserID"];
            var story = db.Stories.FirstOrDefault(s => s.StoryID == id);
            if (story == null || story.UserID != currentUserId)
            {
                return Json(new { success = false, message = "Không có quyền xem thông tin này!" }, JsonRequestBehavior.AllowGet);
            }

            var reactions = StoryHelper.GetReactions(id);
            var viewers = db.StoryViews
                .Include(v => v.User)
                .Include("User.UserProfiles")
                .Where(v => v.StoryID == id)
                .OrderByDescending(v => v.ViewedAt)
                .ToList()
                .Select(v => new
                {
                    userId = v.ViewerID,
                    username = v.User?.DisplayName ?? "Người dùng",
                    avatar = v.User?.SafeAvatar ?? "/Content/Images/default-avatar.png",
                    faculty = v.User?.Faculty ?? "",
                    viewedAt = v.ViewedAt.HasValue ? SocialHelper.FormatTimeAgo(v.ViewedAt) : "Vừa xem",
                    reaction = reactions.FirstOrDefault(r => r.UserID == v.ViewerID)?.Emoji
                });

            return Json(new { success = true, viewers = viewers }, JsonRequestBehavior.AllowGet);
        }

        // =========================================================================
        // TƯƠNG TÁC THẢ CẢM XÚC & TRẢ LỜI TIN QUA ĐOẠN CHAT
        // =========================================================================

        /// <summary>
        /// Thả cảm xúc lên tin 24h của người khác (đồng thời gửi tin nhắn thông báo vào đoạn chat 1vs1)
        /// </summary>
        [HttpPost]
        public JsonResult React(int storyId, string emoji)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để bày tỏ cảm xúc!" });
            }

            int currentUserId = (int)Session["UserID"];
            var story = db.Stories.Include(s => s.User).FirstOrDefault(s => s.StoryID == storyId);
            if (story == null)
            {
                return Json(new { success = false, message = "Tin không tồn tại!" });
            }

            var validEmojis = new[] { "❤️", "😂", "😮", "😢", "👏", "🔥" };
            if (!validEmojis.Contains(emoji))
            {
                emoji = "❤️";
            }

            var me = db.Users.Find(currentUserId);
            string myName = me?.DisplayName ?? "Người dùng";
            string myAvatar = me?.SafeAvatar;

            StoryHelper.SaveReaction(storyId, currentUserId, myName, myAvatar, emoji);

            // Nếu người thả cảm xúc không phải là tác giả tin -> gửi qua đoạn chat 1vs1 và tạo thông báo
            if (story.UserID != currentUserId)
            {
                try
                {
                    int roomId = GetOrCreate1vs1Room(currentUserId, story.UserID);
                    string chatText = $"Đã bày tỏ cảm xúc {emoji} về tin 24h của bạn";
                    SendDirectMessage(roomId, currentUserId, chatText, story.MediaUrl);

                    CreateNotification(story.UserID, "story_reaction", $"{myName} đã bày tỏ cảm xúc {emoji} về tin 24h của bạn.", $"/Chat/Private/{roomId}");
                }
                catch { }
            }

            return Json(new { success = true, emoji = emoji });
        }

        /// <summary>
        /// Gửi lời nhắn trả lời tin 24h qua đoạn chat riêng 1vs1
        /// </summary>
        [HttpPost]
        public JsonResult Reply(int storyId, string message)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để trả lời tin!" });
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, message = "Nội dung tin nhắn không được để trống!" });
            }

            int currentUserId = (int)Session["UserID"];
            var story = db.Stories.Include(s => s.User).FirstOrDefault(s => s.StoryID == storyId);
            if (story == null)
            {
                return Json(new { success = false, message = "Tin không tồn tại!" });
            }

            var mod = WordModerationHelper.Moderate(message);
            string cleanMessage = mod.CleanText;
            if (cleanMessage.Length > 1000) cleanMessage = cleanMessage.Substring(0, 1000);

            var me = db.Users.Find(currentUserId);
            string myName = me?.DisplayName ?? "Người dùng";

            int roomId = GetOrCreate1vs1Room(currentUserId, story.UserID);
            string chatText = $"💬 Trả lời tin 24h: \"{cleanMessage}\"";
            SendDirectMessage(roomId, currentUserId, chatText, story.MediaUrl);

            if (story.UserID != currentUserId)
            {
                CreateNotification(story.UserID, "story_reply", $"{myName} đã trả lời tin 24h của bạn: \"{cleanMessage}\"", $"/Chat/Private/{roomId}");
            }

            return Json(new { success = true, roomId = roomId, message = "Đã gửi tin nhắn!" });
        }

        // =========================================================================
        // KHO LƯU TRỮ TIN (STORY ARCHIVE)
        // =========================================================================

        /// <summary>
        /// Trang xem toàn bộ kho lưu trữ tin cá nhân của người dùng
        /// </summary>
        [HttpGet]
        public ActionResult Archive()
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "User");
            int currentUserId = (int)Session["UserID"];
            var stories = db.Stories
                .Include(s => s.StoryViews)
                .Where(s => s.UserID == currentUserId)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            ViewBag.Highlights = StoryHelper.GetHighlights(currentUserId);
            return View(stories);
        }

        /// <summary>
        /// API lấy toàn bộ tin trong kho lưu trữ để chọn đưa vào mục Tin nổi bật
        /// </summary>
        [HttpGet]
        public JsonResult GetArchive()
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" }, JsonRequestBehavior.AllowGet);
            }

            int currentUserId = (int)Session["UserID"];
            var now = DateTime.Now;

            var stories = db.Stories
                .Include(s => s.StoryViews)
                .Where(s => s.UserID == currentUserId)
                .OrderByDescending(s => s.CreatedAt)
                .ToList()
                .Select(s =>
                {
                    string ext = Path.GetExtension(s.MediaUrl ?? "").ToLower();
                    bool isVideo = ext == ".mp4" || ext == ".webm" || ext == ".mov";
                    return new
                    {
                        storyId = s.StoryID,
                        mediaUrl = s.MediaUrl,
                        mediaType = isVideo ? "video" : "image",
                        caption = s.Caption ?? "",
                        createdAt = s.CreatedAt.HasValue ? s.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
                        timeAgo = SocialHelper.FormatTimeAgo(s.CreatedAt),
                        isExpired = s.ExpiresAt <= now,
                        viewCount = s.StoryViews?.Count ?? 0,
                        yearMonth = s.CreatedAt.HasValue ? s.CreatedAt.Value.ToString("yyyy-MM") : "",
                        monthLabel = s.CreatedAt.HasValue ? $"Tháng {s.CreatedAt.Value.Month}/{s.CreatedAt.Value.Year}" : "Khác"
                    };
                }).ToList();

            return Json(new { success = true, stories = stories }, JsonRequestBehavior.AllowGet);
        }

        // =========================================================================
        // TIN NỔI BẬT (STORY HIGHLIGHTS)
        // =========================================================================

        /// <summary>
        /// Lấy danh sách tin nổi bật của một người dùng
        /// </summary>
        [HttpGet]
        public JsonResult GetUserHighlights(int userId)
        {
            var highlights = StoryHelper.GetHighlights(userId);
            return Json(new { success = true, highlights = highlights }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Tạo hoặc cập nhật bộ sưu tập tin nổi bật
        /// </summary>
        [HttpPost]
        public JsonResult CreateHighlight(CreateHighlightInputModel model)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            if (model == null || string.IsNullOrWhiteSpace(model.Title))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên cho mục tin nổi bật!" });
            }

            if (model.StoryIDs == null || !model.StoryIDs.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 tin để đưa vào mục nổi bật!" });
            }

            int currentUserId = (int)Session["UserID"];

            // Kiểm tra các story thuộc quyền sở hữu của user
            var validStoryIds = db.Stories
                .Where(s => s.UserID == currentUserId && model.StoryIDs.Contains(s.StoryID))
                .Select(s => s.StoryID)
                .ToList();

            if (!validStoryIds.Any())
            {
                return Json(new { success = false, message = "Không tìm thấy tin hợp lệ của bạn!" });
            }

            model.StoryIDs = validStoryIds;

            // Nếu chưa có ảnh bìa thì tự lấy ảnh của tin đầu tiên
            if (string.IsNullOrEmpty(model.CoverUrl))
            {
                var firstStory = db.Stories.Find(validStoryIds.First());
                model.CoverUrl = firstStory?.MediaUrl;
            }

            var saved = StoryHelper.SaveHighlight(currentUserId, model);
            return Json(new { success = true, highlight = saved, message = "Đã lưu tin nổi bật thành công!" });
        }

        /// <summary>
        /// Xóa mục tin nổi bật
        /// </summary>
        [HttpPost]
        public JsonResult DeleteHighlight(string highlightId)
        {
            if (Session["UserID"] == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });
            }

            int currentUserId = (int)Session["UserID"];
            bool ok = StoryHelper.DeleteHighlight(currentUserId, highlightId);
            return Json(new { success = ok, message = ok ? "Đã xóa tin nổi bật!" : "Không tìm thấy tin nổi bật để xóa!" });
        }

        /// <summary>
        /// Lấy các story trong mục tin nổi bật để phát trong Story Viewer
        /// </summary>
        [HttpGet]
        public JsonResult GetHighlightStories(string highlightId)
        {
            var highlight = StoryHelper.FindHighlight(highlightId);
            if (highlight == null || highlight.StoryIDs == null || !highlight.StoryIDs.Any())
            {
                return Json(new { success = false, message = "Không tìm thấy tin nổi bật này!" }, JsonRequestBehavior.AllowGet);
            }

            var user = db.Users.Find(highlight.UserID);
            if (user == null)
            {
                return Json(new { success = false, message = "Người dùng không tồn tại!" }, JsonRequestBehavior.AllowGet);
            }

            var stories = db.Stories
                .Include(s => s.StoryViews)
                .Where(s => highlight.StoryIDs.Contains(s.StoryID))
                .OrderBy(s => s.CreatedAt)
                .ToList();

            int currentUserId = Session["UserID"] != null ? (int)Session["UserID"] : 0;
            bool isOwner = (currentUserId > 0 && currentUserId == user.UserID);

            var storyList = stories.Select(s =>
            {
                string ext = Path.GetExtension(s.MediaUrl ?? "").ToLower();
                bool isVideo = ext == ".mp4" || ext == ".webm" || ext == ".mov";
                return new
                {
                    storyId = s.StoryID,
                    userId = s.UserID,
                    mediaUrl = s.MediaUrl,
                    mediaType = isVideo ? "video" : "image",
                    caption = s.Caption ?? "",
                    createdAt = s.CreatedAt.HasValue ? s.CreatedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : "",
                    timeAgo = SocialHelper.FormatTimeAgo(s.CreatedAt),
                    totalViews = s.StoryViews?.Count ?? 0,
                    hasViewed = true,
                    isOwner = isOwner,
                    isHighlight = true,
                    highlightTitle = highlight.Title
                };
            }).ToList();

            return Json(new
            {
                success = true,
                user = new
                {
                    userId = user.UserID,
                    username = user.DisplayName + " • " + highlight.Title,
                    avatar = user.SafeAvatar,
                    isCurrentUser = isOwner
                },
                stories = storyList
            }, JsonRequestBehavior.AllowGet);
        }

        private int GetOrCreate1vs1Room(int myId, int targetId)
        {
            var existingRoomId = db.ChatParticipants
                .Where(p => p.UserID == myId && p.ChatRoom.RoomType == "1vs1" && p.ChatRoom.IsActive == true)
                .Select(p => p.RoomID)
                .FirstOrDefault(rid => db.ChatParticipants.Any(p => p.RoomID == rid && p.UserID == targetId));

            if (existingRoomId.HasValue && existingRoomId.Value > 0)
                return existingRoomId.Value;

            var newRoom = new ChatRoom { RoomType = "1vs1", CreatedAt = DateTime.Now, IsActive = true };
            db.ChatRooms.Add(newRoom);
            db.SaveChanges();

            db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = myId, JoinedAt = DateTime.Now });
            db.ChatParticipants.Add(new ChatParticipant { RoomID = newRoom.RoomID, UserID = targetId, JoinedAt = DateTime.Now });
            db.SaveChanges();

            try
            {
                var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<StudentConnect.Hubs.ChatHub>();
                hubContext.Clients.All.triggerReloadChatList(newRoom.RoomID);
            }
            catch { }

            return newRoom.RoomID;
        }

        private void SendDirectMessage(int roomId, int senderId, string content, string imageUrl)
        {
            var sender = db.Users.Find(senderId);
            string senderName = sender?.DisplayName ?? "Người dùng";
            string senderAvatar = sender?.SafeAvatar;

            var msg = new ChatMessage
            {
                RoomID = roomId,
                SenderID = senderId,
                Content = "[SHOW_ID]:" + content,
                ImageUrl = imageUrl,
                SentAt = DateTime.Now
            };
            db.ChatMessages.Add(msg);
            db.SaveChanges();

            try
            {
                var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<StudentConnect.Hubs.ChatHub>();
                hubContext.Clients.Group(roomId.ToString())
                    .addNewMessageToPage(senderId, content, imageUrl, msg.MessageID, null, null, false, senderName, senderAvatar);
                hubContext.Clients.Group(roomId.ToString()).triggerReloadChatList(roomId);
            }
            catch { }
        }

        // =========================================================================
        // PRIVATE HELPER METHODS
        // =========================================================================

        private List<UserStoryGroupViewModel> LoadActiveStoryGroups(int currentUserId)
        {
            var now = DateTime.Now;

            // Lấy tất cả tin còn hiệu lực (< 24 giờ kể từ lúc tạo)
            var activeStories = db.Stories
                .Include(s => s.User)
                .Include("User.UserProfiles")
                .Include(s => s.StoryViews)
                .Where(s => s.ExpiresAt > now)
                .OrderBy(s => s.CreatedAt)
                .ToList();

            var groups = new List<UserStoryGroupViewModel>();

            // Nhóm theo UserID
            var groupedByUser = activeStories
                .GroupBy(s => s.UserID)
                .ToList();

            // Nếu người dùng hiện tại đã đăng nhập, xử lý nhóm tin của họ trước tiên
            UserStoryGroupViewModel currentUserGroup = null;
            if (currentUserId > 0)
            {
                var currentUser = db.Users.Include(u => u.UserProfiles).FirstOrDefault(u => u.UserID == currentUserId);
                var myStories = activeStories.Where(s => s.UserID == currentUserId).ToList();

                currentUserGroup = new UserStoryGroupViewModel
                {
                    UserID = currentUserId,
                    Username = currentUser?.DisplayName ?? "Bạn",
                    Avatar = currentUser?.SafeAvatar ?? "/Content/Images/default-avatar.png",
                    IsCurrentUser = true,
                    HasUnviewedStories = false,
                    Stories = myStories.Select(s => new StoryItemViewModel
                    {
                        StoryID = s.StoryID,
                        UserID = s.UserID,
                        MediaUrl = s.MediaUrl,
                        Caption = s.Caption,
                        CreatedAt = s.CreatedAt,
                        ExpiresAt = s.ExpiresAt,
                        TimeAgo = SocialHelper.FormatTimeAgo(s.CreatedAt),
                        ViewCount = s.StoryViews.Count,
                        HasViewed = true,
                        IsOwner = true
                    }).ToList()
                };

                groups.Add(currentUserGroup);
            }

            // Xử lý các nhóm tin của người khác
            var otherGroups = new List<UserStoryGroupViewModel>();
            foreach (var group in groupedByUser)
            {
                if (group.Key == currentUserId) continue; // Đã thêm ở trên

                var author = group.First().User;
                if (author == null) continue;

                // Kiểm tra xem người dùng hiện tại đã xem hết tất cả tin trong nhóm này chưa
                bool hasUnviewed = false;
                if (currentUserId > 0)
                {
                    hasUnviewed = group.Any(s => !s.StoryViews.Any(v => v.ViewerID == currentUserId));
                }
                else
                {
                    hasUnviewed = true; // Khách vãng lai xem như chưa xem
                }

                var groupVm = new UserStoryGroupViewModel
                {
                    UserID = author.UserID,
                    Username = author.DisplayName,
                    Avatar = author.SafeAvatar,
                    IsCurrentUser = false,
                    HasUnviewedStories = hasUnviewed,
                    Stories = group.Select(s => new StoryItemViewModel
                    {
                        StoryID = s.StoryID,
                        UserID = s.UserID,
                        MediaUrl = s.MediaUrl,
                        Caption = s.Caption,
                        CreatedAt = s.CreatedAt,
                        ExpiresAt = s.ExpiresAt,
                        TimeAgo = SocialHelper.FormatTimeAgo(s.CreatedAt),
                        ViewCount = s.StoryViews.Count,
                        HasViewed = currentUserId > 0 && s.StoryViews.Any(v => v.ViewerID == currentUserId),
                        IsOwner = false
                    }).ToList()
                };

                otherGroups.Add(groupVm);
            }

            // Sắp xếp nhóm tin của người khác: Tin chưa xem lên trước, sau đó xếp theo thời gian tin mới nhất giảm dần
            var sortedOtherGroups = otherGroups
                .OrderByDescending(g => g.HasUnviewedStories)
                .ThenByDescending(g => g.LatestStory?.CreatedAt)
                .ToList();

            groups.AddRange(sortedOtherGroups);

            return groups;
        }
    }
}
