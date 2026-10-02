using StudentConnect.Helpers;
using StudentConnect.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace StudentConnect.Controllers
{
    public class UserController : BaseController
    {

        // ==========================================
        // 1. ĐĂNG KÝ TÀI KHOẢN
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User taiKhoanMoi, string confirmPassword)
        {
            if (ModelState.IsValid)
            {
                // 1. Kiểm tra xác nhận mật khẩu
                if (taiKhoanMoi.PasswordHash != confirmPassword)
                {
                    ViewBag.ErrorRegister = "Mật khẩu xác nhận không khớp!";
                    return View(taiKhoanMoi);
                }

                // 2. Kiểm tra mật khẩu mạnh
                var hasNumber = new Regex(@"[0-9]+");
                var hasChar = new Regex(@"[a-zA-Z]+");
                var hasSymbols = new Regex(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]");

                if (taiKhoanMoi.PasswordHash.Length < 8 ||
                    !hasNumber.IsMatch(taiKhoanMoi.PasswordHash) ||
                    !hasChar.IsMatch(taiKhoanMoi.PasswordHash) ||
                    !hasSymbols.IsMatch(taiKhoanMoi.PasswordHash))
                {
                    ViewBag.ErrorRegister = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm cả chữ cái, số và ký tự đặc biệt!";
                    return View(taiKhoanMoi);
                }

                if (!taiKhoanMoi.Email.EndsWith("@student.tdmu.edu.vn"))
                {
                    ViewBag.ErrorRegister = "Chỉ chấp nhận email sinh viên TDMU (@student.tdmu.edu.vn).";
                    return View(taiKhoanMoi);
                }

                var checkEmail = db.Users.FirstOrDefault(u => u.Email == taiKhoanMoi.Email);
                if (checkEmail != null)
                {
                    ViewBag.ErrorRegister = "Email này đã được sử dụng! Vui lòng dùng Email khác.";
                    return View(taiKhoanMoi);
                }

                taiKhoanMoi.PasswordHash = PasswordHelper.HashPassword(taiKhoanMoi.PasswordHash);
                taiKhoanMoi.CreatedAt = DateTime.Now;
                db.Users.Add(taiKhoanMoi);
                db.SaveChanges();

                Session["UserID"] = taiKhoanMoi.UserID;
                Session["Username"] = taiKhoanMoi.Username;
                Session["Avatar"] = taiKhoanMoi.SafeAvatar;
                return RedirectToAction("Index", "Home");
            }
            return View(taiKhoanMoi);
        }

        // ĐĂNG NHẬP
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            if (ModelState.IsValid)
            {
                var checkUser = db.Users.FirstOrDefault(u => u.Email == email);

                if (checkUser != null && PasswordHelper.VerifyPassword(password, checkUser.PasswordHash))
                {
                    // Tự động nâng cấp hash MD5 cũ lên PBKDF2 nếu đăng nhập thành công
                    if (PasswordHelper.IsLegacyMD5(checkUser.PasswordHash))
                    {
                        checkUser.PasswordHash = PasswordHelper.HashPassword(password);
                        db.SaveChanges();
                    }

                    Session["UserID"] = checkUser.UserID;
                    Session["Username"] = checkUser.Username;
                    Session["Avatar"] = checkUser.SafeAvatar;
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ViewBag.ErrorLogin = "Email hoặc Mật khẩu không chính xác!";
                }
            }
            return View();
        }

        // ĐĂNG XUẤT
        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // QUÊN MẬT KHẨU
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            if (!email.EndsWith("@student.tdmu.edu.vn"))
            {
                ViewBag.ErrorMessage = "Chỉ chấp nhận email sinh viên TDMU (@student.tdmu.edu.vn).";
                return View();
            }
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user != null)
            {
                Random rd = new Random();
                string otp = rd.Next(100000, 999999).ToString();

                Session["ResetEmail"] = email;
                Session["OTP"] = otp;

                try
                {
                    string fromEmail = "nguyenphan2006tpk@gmail.com";
                    string appPassword = "gaibfkwazndlbuod";

                    MailMessage mail = new MailMessage();
                    mail.To.Add(email);
                    mail.From = new MailAddress(fromEmail, "Nền tảng kết nối cộng đồng sinh viên TDMU");
                    mail.Subject = "Mã xác minh khôi phục mật khẩu";
                    mail.Body = $"Xin chào <b>{user.Username}</b>,<br><br>" +
                                $"Bạn vừa yêu cầu đặt lại mật khẩu. Đây là mã xác minh (OTP) của bạn:<br><br>" +
                                $"<h2 style='color:blue; letter-spacing: 5px;'>{otp}</h2><br>" +
                                $"Vui lòng không chia sẻ mã này cho bất kỳ ai. Mã này dùng để xác minh tài khoản của bạn.<br><br>" +
                                $"Trân trọng,<br>Đội ngũ phát triển Nền tảng kết nối cộng đồng sinh viên TDMU.";
                    mail.IsBodyHtml = true;

                    SmtpClient smtp = new SmtpClient("smtp.gmail.com");
                    smtp.EnableSsl = true;
                    smtp.Port = 587;
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Credentials = new NetworkCredential(fromEmail, appPassword);

                    smtp.Send(mail);

                    return RedirectToAction("VerifyOTP");
                }
                catch (Exception ex)
                {
                    ViewBag.ErrorMessage = "Lỗi khi gửi email: " + ex.Message;
                }
            }
            else
            {
                ViewBag.ErrorMessage = "Email này chưa được đăng ký trong hệ thống!";
            }

            return View();
        }

        // QUÊN MẬT KHẨU 
        public ActionResult VerifyOTP()
        {
            if (Session["ResetEmail"] == null)
            {
                return RedirectToAction("ForgotPassword");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyOTP(string otp)
        {
            if (Session["OTP"] != null && Session["OTP"].ToString() == otp)
            {
                return RedirectToAction("ResetPassword");
            }
            else
            {
                ViewBag.ErrorMessage = "Mã OTP không chính xác. Vui lòng kiểm tra lại email!";
                return View();
            }
        }

        // QUÊN MẬT KHẨU 
        public ActionResult ResetPassword()
        {
            if (Session["ResetEmail"] == null || Session["OTP"] == null)
            {
                return RedirectToAction("ForgotPassword");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.ErrorMessage = "Mật khẩu xác nhận không khớp!";
                return View();
            }

            string email = Session["ResetEmail"].ToString();
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user != null)
            {
                user.PasswordHash = PasswordHelper.HashPassword(newPassword);
                db.SaveChanges();

                Session.Remove("ResetEmail");
                Session.Remove("OTP");

                TempData["SuccessMessage"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại với mật khẩu mới.";
                return RedirectToAction("Login");
            }

            ViewBag.ErrorMessage = "Đã xảy ra lỗi, vui lòng thử lại!";
            return View();
        }

        // PROFILE CÁ NHÂN VÀ MẠNG XÃ HỘI
        public new ActionResult Profile(int? id)
        {
            int currentUserId = Session["UserID"] as int? ?? 0;
            if (currentUserId == 0 && (!id.HasValue || id.Value <= 0))
            {
                return RedirectToAction("Login", "User");
            }

            int targetUserId = id.HasValue && id.Value > 0 ? id.Value : currentUserId;
            var targetUser = db.Users
                .Include(u => u.UserProfiles)
                .Include(u => u.MarketPosts.Select(p => p.MarketImages))
                .Include(u => u.MarketPosts.Select(p => p.MarketCategory))
                .Include(u => u.ConnectPosts.Select(p => p.ConnectCategory))
                .FirstOrDefault(u => u.UserID == targetUserId);

            if (targetUser == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var userProfile = targetUser.UserProfiles?.FirstOrDefault();

            // Quan hệ với người đang xem
            int? friendshipId = null;
            var relStatus = currentUserId > 0 
                ? SocialHelper.GetRelationshipStatus(currentUserId, targetUserId, db, out friendshipId)
                : UserRelationshipStatus.None;

            bool isFollowing = currentUserId > 0 && db.Follows.Any(f => f.FollowerID == currentUserId && f.FollowingID == targetUserId);

            // Thống kê bạn bè & tương tác
            var friendIds = SocialHelper.GetFriendIds(targetUserId, db);
            int friendCount = friendIds.Count;
            int followerCount = db.Follows.Count(f => f.FollowingID == targetUserId);
            int followingCount = db.Follows.Count(f => f.FollowerID == targetUserId);
            int postCount = db.Posts.Count(p => p.UserID == targetUserId);

            // Bạn bè xem trước (tối đa 9 người)
            var friendsPreview = db.Users
                .Where(u => friendIds.Contains(u.UserID))
                .Take(9)
                .ToList()
                .Select(u => new UserProfilePreviewItem
                {
                    UserID = u.UserID,
                    Username = u.DisplayName,
                    Avatar = u.SafeAvatar,
                    Faculty = u.UserProfiles?.FirstOrDefault()?.Faculty,
                    MutualFriendsCount = (currentUserId > 0 && currentUserId != u.UserID) 
                        ? SocialHelper.GetMutualFriendsCount(currentUserId, u.UserID, db) 
                        : 0
                })
                .ToList();

            // Tin 24h đang còn hiệu lực
            var now = DateTime.Now;
            var activeStories = db.Stories
                .Where(s => s.UserID == targetUserId && s.ExpiresAt > now)
                .OrderBy(s => s.CreatedAt)
                .ToList()
                .Select(s => new StoryItemViewModel
                {
                    StoryID = s.StoryID,
                    UserID = s.UserID,
                    MediaUrl = s.MediaUrl,
                    Caption = s.Caption,
                    CreatedAt = s.CreatedAt,
                    ExpiresAt = s.ExpiresAt,
                    TimeAgo = SocialHelper.FormatTimeAgo(s.CreatedAt),
                    ViewCount = s.StoryViews?.Count ?? 0,
                    HasViewed = currentUserId > 0 && s.StoryViews.Any(v => v.ViewerID == currentUserId),
                    IsOwner = currentUserId == targetUserId
                })
                .ToList();

            // Danh sách ảnh gần đây từ bài viết
            var recentPhotos = db.PostMedias
                .Where(m => m.Post.UserID == targetUserId && m.MediaType == "image")
                .OrderByDescending(m => m.Post.CreatedAt)
                .Take(9)
                .Select(m => m.MediaUrl)
                .ToList();

            // Dòng thời gian bài viết (kiểm tra tài khoản riêng tư)
            var postsVm = new List<PostItemViewModel>();
            bool isOwner = currentUserId == targetUserId;
            bool isFriend = relStatus == UserRelationshipStatus.Friends;
            bool canViewTimeline = isOwner || !targetUser.IsPrivate || isFriend;

            if (canViewTimeline)
            {
                var rawPosts = db.Posts
                    .Include(p => p.User)
                    .Include(p => p.User.UserProfiles)
                    .Include(p => p.PostMedias)
                    .Include(p => p.PostLikes)
                    .Include(p => p.PostComments.Select(c => c.User))
                    .Where(p => p.UserID == targetUserId)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                postsVm = rawPosts
                    .Where(p => SocialHelper.CanViewPost(p, currentUserId, db, friendIds))
                    .Select(p => SocialHelper.MapToPostViewModel(p, currentUserId))
                    .ToList();

                // Lấy các bài viết mà người này đã chia sẻ
                var rawShares = db.PostShares
                    .Include(s => s.User)
                    .Include(s => s.User.UserProfiles)
                    .Include(s => s.Post)
                    .Include(s => s.Post.User)
                    .Include(s => s.Post.User.UserProfiles)
                    .Include(s => s.Post.PostMedias)
                    .Include(s => s.Post.PostLikes)
                    .Include(s => s.Post.PostComments.Select(c => c.User))
                    .Where(s => s.UserID == targetUserId)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToList();

                var sharedPostsVm = rawShares
                    .Where(s => s.Post != null && SocialHelper.CanViewPost(s.Post, currentUserId, db, friendIds))
                    .Select(s => SocialHelper.MapShareToPostViewModel(s, currentUserId))
                    .Where(vmItem => vmItem != null)
                    .ToList();

                // Trộn bài viết tự đăng và bài viết đã chia sẻ theo thứ tự thời gian
                postsVm = postsVm.Concat(sharedPostsVm)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();
            }

            if (currentUserId > 0)
            {
                try
                {
                    var hiddenPostIds = db.Database.SqlQuery<int>(
                        "SELECT PostID FROM UserHiddenPosts WHERE UserID = @uid",
                        new System.Data.SqlClient.SqlParameter("@uid", currentUserId)).ToList();
                    postsVm = postsVm.Where(p => !hiddenPostIds.Contains(p.PostID)).ToList();
                }
                catch { }
            }

            var vm = new SocialProfileViewModel
            {
                UserID = targetUser.UserID,
                Username = targetUser.DisplayName,
                Email = targetUser.Email,
                PhoneNumber = targetUser.PhoneNumber,
                Avatar = targetUser.SafeAvatar,
                IsTDMUStudent = targetUser.IsTDMUStudent,
                IsPrivate = targetUser.IsPrivate,
                CreatedAt = targetUser.CreatedAt,

                ProfileID = userProfile?.ProfileID,
                CoverPhoto = !string.IsNullOrEmpty(userProfile?.CoverPhoto) ? userProfile.CoverPhoto : "/Content/Images/default-cover.jpg",
                Bio = userProfile?.Bio,
                Faculty = userProfile?.Faculty,
                Major = userProfile?.Major,
                StudentYear = userProfile?.StudentYear,
                Hometown = userProfile?.Hometown,

                RelationshipStatus = isOwner ? UserRelationshipStatus.Self : relStatus,
                FriendshipID = friendshipId,
                IsFollowing = isFollowing,

                FriendCount = friendCount,
                MutualFriendCount = (currentUserId > 0 && currentUserId != targetUserId) 
                    ? SocialHelper.GetMutualFriendsCount(currentUserId, targetUserId, db) 
                    : 0,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                PostCount = postCount,

                FriendsPreview = friendsPreview,
                RecentPhotos = recentPhotos,
                Posts = postsVm,
                ActiveStories = activeStories,
                Highlights = StoryHelper.GetHighlights(targetUserId)
            };

            ViewBag.MarketPosts = targetUser.MarketPosts?.OrderByDescending(p => p.CreatedAt).ToList() ?? new List<MarketPost>();
            ViewBag.ConnectPosts = targetUser.ConnectPosts?.OrderByDescending(p => p.CreatedAt).ToList() ?? new List<ConnectPost>();
            ViewBag.IsOwner = isOwner;

            return View(vm);
        }

        // ==========================================
        // QUẢN LÝ KẾT BẠN & THEO DÕI
        // ==========================================

        [HttpPost]
        public ActionResult SendFriendRequest(int targetId, string message = null)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập để kết bạn!" });

            int currentUserId = (int)Session["UserID"];
            if (currentUserId == targetId)
                return Json(new { success = false, message = "Bạn không thể tự kết bạn với chính mình!" });

            var existing = db.Friendships.FirstOrDefault(f =>
                (f.RequesterID == currentUserId && f.ReceiverID == targetId) ||
                (f.RequesterID == targetId && f.ReceiverID == currentUserId));

            if (existing != null)
            {
                if (existing.Status == FriendshipStatus.Accepted)
                    return Json(new { success = false, message = "Hai bạn đã là bạn bè rồi!" });
                if (existing.Status == FriendshipStatus.Pending)
                    return Json(new { success = false, message = "Đang chờ chấp nhận lời mời kết bạn!" });

                existing.RequesterID = currentUserId;
                existing.ReceiverID = targetId;
                existing.Status = FriendshipStatus.Pending;
                existing.CreatedAt = DateTime.Now;
            }
            else
            {
                existing = new Friendship
                {
                    RequesterID = currentUserId,
                    ReceiverID = targetId,
                    Status = FriendshipStatus.Pending,
                    CreatedAt = DateTime.Now
                };
                db.Friendships.Add(existing);
            }

            db.SaveChanges();

            var sender = db.Users.Find(currentUserId);
            var senderName = sender?.DisplayName ?? "Ai đó";
            var notifContent = string.IsNullOrWhiteSpace(message)
                ? $"{senderName} đã gửi cho bạn một lời mời kết bạn."
                : $"{senderName} đã gửi cho bạn một lời mời kết bạn: \"{message}\"";
            CreateNotification(targetId, "friend_request", notifContent, $"/User/Profile/{currentUserId}");

            return Json(new { success = true, status = "PendingSent", friendshipId = existing.FriendshipID, message = "Đã gửi lời mời kết bạn!" });
        }

        [HttpPost]
        public ActionResult AcceptFriendRequest(int? friendshipId, int? targetId)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            int currentUserId = (int)Session["UserID"];
            Friendship friendship = null;

            if (friendshipId.HasValue && friendshipId.Value > 0)
            {
                friendship = db.Friendships.Find(friendshipId.Value);
            }
            else if (targetId.HasValue)
            {
                friendship = db.Friendships.FirstOrDefault(f => f.RequesterID == targetId.Value && f.ReceiverID == currentUserId);
            }

            if (friendship == null || friendship.ReceiverID != currentUserId)
                return Json(new { success = false, message = "Không tìm thấy lời mời kết bạn hợp lệ!" });

            friendship.Status = FriendshipStatus.Accepted;
            db.SaveChanges();

            var user = db.Users.Find(currentUserId);
            CreateNotification(friendship.RequesterID, "friend_accepted", $"{user?.DisplayName ?? "Ai đó"} đã chấp nhận lời mời kết bạn của bạn.", $"/User/Profile/{currentUserId}");

            return Json(new { success = true, status = "Friends", message = "Đã trở thành bạn bè!" });
        }

        [HttpPost]
        public ActionResult DeclineFriendRequest(int? friendshipId, int? targetId)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            int currentUserId = (int)Session["UserID"];
            Friendship friendship = null;

            if (friendshipId.HasValue && friendshipId.Value > 0)
            {
                friendship = db.Friendships.Find(friendshipId.Value);
            }
            else if (targetId.HasValue)
            {
                friendship = db.Friendships.FirstOrDefault(f => 
                    (f.RequesterID == targetId.Value && f.ReceiverID == currentUserId) ||
                    (f.RequesterID == currentUserId && f.ReceiverID == targetId.Value));
            }

            if (friendship != null)
            {
                db.Friendships.Remove(friendship);
                db.SaveChanges();
            }

            return Json(new { success = true, status = "None", message = "Đã từ chối/hủy lời mời kết bạn." });
        }

        [HttpPost]
        public ActionResult Unfriend(int targetId)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            int currentUserId = (int)Session["UserID"];
            var friendship = db.Friendships.FirstOrDefault(f =>
                (f.RequesterID == currentUserId && f.ReceiverID == targetId) ||
                (f.RequesterID == targetId && f.ReceiverID == currentUserId));

            if (friendship != null)
            {
                db.Friendships.Remove(friendship);
                db.SaveChanges();
            }

            return Json(new { success = true, status = "None", message = "Đã hủy kết bạn." });
        }

        [HttpPost]
        public ActionResult ToggleFollow(int targetId)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            int currentUserId = (int)Session["UserID"];
            if (currentUserId == targetId)
                return Json(new { success = false, message = "Không thể theo dõi chính mình!" });

            var follow = db.Follows.FirstOrDefault(f => f.FollowerID == currentUserId && f.FollowingID == targetId);
            bool isFollowing;

            if (follow != null)
            {
                db.Follows.Remove(follow);
                isFollowing = false;
            }
            else
            {
                follow = new Follow
                {
                    FollowerID = currentUserId,
                    FollowingID = targetId,
                    CreatedAt = DateTime.Now
                };
                db.Follows.Add(follow);
                isFollowing = true;

                var sender = db.Users.Find(currentUserId);
                CreateNotification(targetId, "follow", $"{sender?.DisplayName ?? "Ai đó"} đã bắt đầu theo dõi bạn.", $"/User/Profile/{currentUserId}");
            }

            db.SaveChanges();
            int newFollowerCount = db.Follows.Count(f => f.FollowingID == targetId);

            return Json(new { success = true, isFollowing = isFollowing, followerCount = newFollowerCount, message = isFollowing ? "Đã theo dõi!" : "Đã bỏ theo dõi." });
        }

        [HttpPost]
        public ActionResult TogglePrivacy()
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            int currentUserId = (int)Session["UserID"];
            var user = db.Users.Find(currentUserId);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy người dùng!" });

            user.IsPrivate = !user.IsPrivate;
            db.SaveChanges();

            return Json(new { success = true, isPrivate = user.IsPrivate, message = user.IsPrivate ? "Đã bật chế độ tài khoản Riêng tư." : "Đã tắt chế độ Riêng tư (Công khai)." });
        }

        // ==========================================
        // CẬP NHẬT TRANG CÁ NHÂN MẠNG XÃ HỘI
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateSocialProfile(UpdateProfileInputModel model, HttpPostedFileBase avatarFile, HttpPostedFileBase coverFile)
        {
            if (Session["UserID"] == null)
                return Json(new { success = false, message = "Vui lòng đăng nhập!" });

            int currentUserId = (int)Session["UserID"];
            var user = db.Users.Find(currentUserId);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy người dùng!" });

            try
            {
                bool isFullForm = Request.Form["isProfileFormSubmit"] == "true";

                // Chỉ cập nhật thông tin cá nhân và IsPrivate khi submit từ form chỉnh sửa hồ sơ
                if (isFullForm)
                {
                    user.IsPrivate = model.IsPrivate;

                    var profile = db.UserProfiles.FirstOrDefault(p => p.UserID == currentUserId);
                    if (profile == null)
                    {
                        profile = new UserProfile { UserID = currentUserId };
                        db.UserProfiles.Add(profile);
                    }

                    profile.Bio = model.Bio?.Trim();
                    profile.Faculty = model.Faculty?.Trim();
                    profile.Major = model.Major?.Trim();
                    profile.StudentYear = model.StudentYear;
                    profile.Hometown = model.Hometown?.Trim();
                }

                // Avatar
                var effectiveAvatarFile = avatarFile ?? model.AvatarFile;
                if (effectiveAvatarFile != null && effectiveAvatarFile.ContentLength > 0)
                {
                    string dir = Server.MapPath("~/Content/Uploads/Avatars/");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    string ext = Path.GetExtension(effectiveAvatarFile.FileName).ToLower();
                    string fileName = $"avatar_{currentUserId}_{DateTime.Now.Ticks}{ext}";
                    effectiveAvatarFile.SaveAs(Path.Combine(dir, fileName));
                    user.Avatar = "/Content/Uploads/Avatars/" + fileName;
                }

                // Cover photo
                var effectiveCoverFile = coverFile ?? model.CoverFile;
                if (effectiveCoverFile != null && effectiveCoverFile.ContentLength > 0)
                {
                    var profile = db.UserProfiles.FirstOrDefault(p => p.UserID == currentUserId);
                    if (profile == null)
                    {
                        profile = new UserProfile { UserID = currentUserId };
                        db.UserProfiles.Add(profile);
                    }

                    string dir = Server.MapPath("~/Content/Uploads/Covers/");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    string ext = Path.GetExtension(effectiveCoverFile.FileName).ToLower();
                    string fileName = $"cover_{currentUserId}_{DateTime.Now.Ticks}{ext}";
                    effectiveCoverFile.SaveAs(Path.Combine(dir, fileName));
                    profile.CoverPhoto = "/Content/Uploads/Covers/" + fileName;
                }

                db.SaveChanges();
                Session["Avatar"] = user.SafeAvatar;

                var currentProfile = db.UserProfiles.FirstOrDefault(p => p.UserID == currentUserId);

                return Json(new { 
                    success = true, 
                    message = "Cập nhật trang cá nhân thành công!",
                    avatar = user.SafeAvatar,
                    coverPhoto = currentProfile?.CoverPhoto ?? "/Content/Images/default-cover.jpg",
                    isPrivate = user.IsPrivate
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật: " + ex.Message });
            }
        }
    }
}