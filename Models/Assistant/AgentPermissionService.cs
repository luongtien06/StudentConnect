using System;
using System.Data.Entity;
using System.Threading.Tasks;

namespace StudentConnect.Models.Assistant
{
    public class AgentPermissionService
    {
        /// <summary>
        /// Lấy quyền của người dùng đối với Agent. Nếu chưa có, tự động khởi tạo mặc định.
        /// </summary>
        public async Task<AgentPermission> GetOrCreatePermissionsAsync(TDMUEcoSystemEntities db, int userId)
        {
            if (userId <= 0)
                return null;

            var perm = await db.AgentPermissions.FirstOrDefaultAsync(p => p.UserID == userId);
            if (perm == null)
            {
                perm = new AgentPermission
                {
                    UserID = userId,
                    CanReadData = true,         // Mặc định cho phép đọc dữ liệu công khai & thông tin bản thân
                    CanManageContent = true,    // Mặc định cho phép tạo bài viết (hành động xóa sẽ qua bước xác nhận)
                    CanInteract = true,         // Mặc định cho phép tương tác thích / bình luận theo chỉ đạo người dùng
                    CanAutomation = false,       // Mặc định chưa bật tự động lên lịch chạy ngầm
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.AgentPermissions.Add(perm);
                await db.SaveChangesAsync();
            }

            return perm;
        }

        /// <summary>
        /// Cập nhật các quyền của người dùng đối với Agent
        /// </summary>
        public async Task<AgentPermission> UpdatePermissionsAsync(
            TDMUEcoSystemEntities db,
            int userId,
            bool canRead,
            bool canManage,
            bool canInteract,
            bool canAutomation,
            bool isEnabled)
        {
            var perm = await GetOrCreatePermissionsAsync(db, userId);
            if (perm != null)
            {
                perm.CanReadData = canRead;
                perm.CanManageContent = canManage;
                perm.CanInteract = canInteract;
                perm.CanAutomation = canAutomation;
                perm.IsEnabled = isEnabled;
                perm.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();
            }

            return perm;
        }
    }
}
