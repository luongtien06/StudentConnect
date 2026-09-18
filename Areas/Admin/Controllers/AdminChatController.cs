using StudentConnect.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace StudentConnect.Areas.Admin.Controllers
{
    public class AdminChatController : AdminBaseController
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            if (filterContext.Result != null) return;
            AuthorizeRole(filterContext, "SuperAdmin");
        }

        // 1. Trang danh sách quản lý các phòng chat ẩn danh
        public ActionResult Index()
        {
            System.Web.Hosting.HostingEnvironment.QueueBackgroundWorkItem(_ =>
            {
                try { CleanupInactiveRooms(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("CleanupInactiveRooms failed: " + ex.Message);
                }
            });

            var rooms = db.ChatRooms
                          .Where(r => r.RoomType != "Public")
                          .OrderByDescending(r => r.CreatedAt)
                          .ToList();

            return View(rooms);
        }

        // 2. Action xóa phòng chat (Gọi từ Ajax)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteRoom(int id)
        {
            try
            {
                var room = db.ChatRooms.Find(id);
                if (room == null) return Json(new { success = false, message = "Phòng không tồn tại." });

                // Xóa dữ liệu liên quan (Nếu DB không tự Cascade)
                var msgs = db.ChatMessages.Where(m => m.RoomID == id);
                db.ChatMessages.RemoveRange(msgs);

                var parts = db.ChatParticipants.Where(p => p.RoomID == id);
                db.ChatParticipants.RemoveRange(parts);

                db.ChatRooms.Remove(room);
                db.SaveChanges();

                return Json(new { success = true, message = "Xóa phòng thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
        // 2b. Action xóa nhiều phòng chat cùng lúc (Gọi từ Ajax)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteMultipleRooms(List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "Không có phòng nào được chọn." });

            ids = ids.Where(id => id > 0).Distinct().Take(100).ToList();

            try
            {
                // 1. Lấy danh sách phòng cần xóa
                var rooms = db.ChatRooms.Where(r => ids.Contains(r.RoomID)).ToList();
                if (!rooms.Any())
                    return Json(new { success = false, message = "Không tìm thấy phòng hợp lệ." });

                // 2. Xóa dữ liệu liên quan trước (Nếu DB chưa cài Cascade Delete)
                var messages = db.ChatMessages.Where(m => ids.Contains(m.RoomID.Value)); // Dùng trực tiếp RoomID
                db.ChatMessages.RemoveRange(messages);

                var participants = db.ChatParticipants.Where(p => ids.Contains(p.RoomID.Value));
                db.ChatParticipants.RemoveRange(participants);

                // 3. Xóa phòng
                db.ChatRooms.RemoveRange(rooms);

                db.SaveChanges();

                return Json(new { success = true, message = $"Hệ thống đã dọn dẹp xong {rooms.Count} phòng." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        // 3. Hàm dọn dẹp các phòng không hoạt động (Private/Group)
        private void CleanupInactiveRooms()
        {
            using (var context = new TDMUEcoSystemEntities())
            {
                var cutoff = DateTime.Now.AddDays(-1);

                var oldRoomIds = context.ChatRooms
                    .Where(r => r.IsActive == false
                             && r.RoomType != "Public"
                             && r.CreatedAt < cutoff)
                    .Select(r => r.RoomID)
                    .ToList();

                if (!oldRoomIds.Any()) return;

                context.ChatMessages.RemoveRange(
     context.ChatMessages.Where(m => m.RoomID.HasValue && oldRoomIds.Contains(m.RoomID.Value)));

                context.ChatParticipants.RemoveRange(
                    context.ChatParticipants.Where(p => p.RoomID.HasValue && oldRoomIds.Contains(p.RoomID.Value)));

                context.ChatRooms.RemoveRange(
                    context.ChatRooms.Where(r => oldRoomIds.Contains(r.RoomID)));

                context.SaveChanges();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}