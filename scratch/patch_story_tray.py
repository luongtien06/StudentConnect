import os

path = 'Views/Story/_StoryTrayPartial.cshtml'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Update lines 4-9
old_top = """@{
    int currentUserId = Session["UserID"] != null ? (int)Session["UserID"] : 0;
    string currentAvatar = Session["Avatar"] as string ?? "/Content/Images/default-avatar.png";
    var currentUserGroup = Model?.FirstOrDefault(g => g.IsCurrentUser);
    var otherGroups = Model?.Where(g => !g.IsCurrentUser).ToList() ?? new List<UserStoryGroupViewModel>();
}"""

new_top = """@{
    bool isLoggedIn = Session["UserID"] != null && Session["Username"] != null && (int)Session["UserID"] > 0;
    int currentUserId = isLoggedIn ? (int)Session["UserID"] : 0;
    string currentAvatar = Session["Avatar"] as string ?? "/Content/Images/default-avatar.png";
    var currentUserGroup = isLoggedIn ? Model?.FirstOrDefault(g => g.IsCurrentUser) : null;
    var otherGroups = Model?.Where(g => !g.IsCurrentUser).ToList() ?? new List<UserStoryGroupViewModel>();
}"""

content = content.replace(old_top.replace('\r\n', '\n'), new_top.replace('\r\n', '\n'))

# 2. Update header button
old_hdr = """        @if (currentUserId > 0)
        {
            <button type="button" class="btn-create-story-header" onclick="openCreateStoryModal()" title="Tạo tin 24h mới">
                <i class="fa-solid fa-plus"></i> <span>Tạo tin</span>
            </button>
        }"""

new_hdr = """        @if (isLoggedIn)
        {
            <button type="button" class="btn-create-story-header" onclick="openCreateStoryModal()" title="Tạo tin 24h mới">
                <i class="fa-solid fa-plus"></i> <span>Tạo tin</span>
            </button>
        }"""

content = content.replace(old_hdr.replace('\r\n', '\n'), new_hdr.replace('\r\n', '\n'))

# 3. Update section 1 card: only if isLoggedIn, remove else block
old_card_section = """        <!-- 1. CARD TẠO TIN / TIN CỦA BẠN -->
        @if (currentUserId > 0)
        {
            var myStoryCount = currentUserGroup?.Stories?.Count ?? 0;
            var myLatestStory = currentUserGroup?.LatestStory;

            if (myStoryCount > 0 && myLatestStory != null)
            {
                string myStoryExtension = System.IO.Path.GetExtension(myLatestStory.MediaUrl ?? "").ToLowerInvariant();
                bool myLatestStoryIsVideo = myStoryExtension == ".mp4" || myStoryExtension == ".webm" || myStoryExtension == ".mov";
                <!-- Đã có tin: Hiển thị thumbnail tin của mình + nút xem & nút tạo thêm -->
                <div class="story-card" data-user-id="@currentUserId" data-username="Tin của bạn" onclick="openStoryViewer(@currentUserId, 0)">
                    @if (myLatestStoryIsVideo)
                    {
                        <video src="@myLatestStory.MediaUrl" class="story-card-bg story-card-video-preview" muted playsinline preload="metadata" aria-label="Xem trước video tin của bạn"></video>
                    }
                    else
                    {
                        <img src="@myLatestStory.MediaUrl" class="story-card-bg" alt="Tin của bạn" onerror="this.src='/Content/Images/default-cover.jpg';" />
                    }
                    <div class="story-card-scrim"></div>
                    <div class="story-card-avatar-wrap">
                        <div class="story-avatar-ring viewed">
                            <img src="@currentUserGroup.Avatar" class="story-avatar-img" alt="Bạn" onerror="this.src='/Content/Images/default-avatar.png';" />
                        </div>
                    </div>
                    <span class="story-badge-count">@myStoryCount tin</span>

                    <!-- Nút + thêm tin nhanh ngay trên thẻ Tin của bạn -->
                    <div class="story-card-add-btn" onclick="event.stopPropagation(); openCreateStoryModal();" title="Thêm tin mới">
                        <i class="fa-solid fa-plus"></i>
                    </div>

                    <div class="story-card-info" style="right: 42px;">
                        <span class="story-card-name">Tin của bạn</span>
                        <span class="story-card-time">@myLatestStory.TimeAgo</span>
                    </div>
                </div>
            }
            else
            {
                <!-- Chưa có tin: Card "Tạo tin" chuẩn Facebook -->
                <div class="story-card create-card" onclick="openCreateStoryModal()">
                    <div class="create-card-preview">
                        <img src="@currentAvatar" alt="Tạo tin" onerror="this.src='/Content/Images/default-avatar.png';" />
                    </div>
                    <div class="create-card-btn-wrap">
                        <div class="create-card-icon-btn">
                            <i class="fa-solid fa-plus"></i>
                        </div>
                    </div>
                    <div class="create-card-footer">
                        <span class="create-card-label">Tạo tin mới</span>
                    </div>
                </div>
            }
        }
        else
        {
            <!-- Chưa đăng nhập: Mời đăng nhập tạo tin -->
            <div class="story-card create-card" onclick="location.href='@Url.Action("Login", "User")'">
                <div class="create-card-preview">
                    <img src="/Content/Images/default-avatar.png" alt="Đăng nhập tạo tin" />
                </div>
                <div class="create-card-btn-wrap">
                    <div class="create-card-icon-btn">
                        <i class="fa-solid fa-arrow-right-to-bracket"></i>
                    </div>
                </div>
                <div class="create-card-footer">
                    <span class="create-card-label">Đăng nhập</span>
                </div>
            </div>
        }"""

new_card_section = """        <!-- 1. CARD TẠO TIN / TIN CỦA BẠN (CHỈ HIỂN THỊ KHI ĐÃ ĐĂNG NHẬP) -->
        @if (isLoggedIn)
        {
            var myStoryCount = currentUserGroup?.Stories?.Count ?? 0;
            var myLatestStory = currentUserGroup?.LatestStory;

            if (myStoryCount > 0 && myLatestStory != null)
            {
                string myStoryExtension = System.IO.Path.GetExtension(myLatestStory.MediaUrl ?? "").ToLowerInvariant();
                bool myLatestStoryIsVideo = myStoryExtension == ".mp4" || myStoryExtension == ".webm" || myStoryExtension == ".mov";
                <!-- Đã có tin: Hiển thị thumbnail tin của mình + nút xem & nút tạo thêm -->
                <div class="story-card" data-user-id="@currentUserId" data-username="Tin của bạn" onclick="openStoryViewer(@currentUserId, 0)">
                    @if (myLatestStoryIsVideo)
                    {
                        <video src="@myLatestStory.MediaUrl" class="story-card-bg story-card-video-preview" muted playsinline preload="metadata" aria-label="Xem trước video tin của bạn"></video>
                    }
                    else
                    {
                        <img src="@myLatestStory.MediaUrl" class="story-card-bg" alt="Tin của bạn" onerror="this.src='/Content/Images/default-cover.jpg';" />
                    }
                    <div class="story-card-scrim"></div>
                    <div class="story-card-avatar-wrap">
                        <div class="story-avatar-ring viewed">
                            <img src="@currentUserGroup.Avatar" class="story-avatar-img" alt="Bạn" onerror="this.src='/Content/Images/default-avatar.png';" />
                        </div>
                    </div>
                    <span class="story-badge-count">@myStoryCount tin</span>

                    <!-- Nút + thêm tin nhanh ngay trên thẻ Tin của bạn -->
                    <div class="story-card-add-btn" onclick="event.stopPropagation(); openCreateStoryModal();" title="Thêm tin mới">
                        <i class="fa-solid fa-plus"></i>
                    </div>

                    <div class="story-card-info" style="right: 42px;">
                        <span class="story-card-name">Tin của bạn</span>
                        <span class="story-card-time">@myLatestStory.TimeAgo</span>
                    </div>
                </div>
            }
            else
            {
                <!-- Chưa có tin: Card "Tạo tin" chuẩn Facebook -->
                <div class="story-card create-card" onclick="openCreateStoryModal()">
                    <div class="create-card-preview">
                        <img src="@currentAvatar" alt="Tạo tin" onerror="this.src='/Content/Images/default-avatar.png';" />
                    </div>
                    <div class="create-card-btn-wrap">
                        <div class="create-card-icon-btn">
                            <i class="fa-solid fa-plus"></i>
                        </div>
                    </div>
                    <div class="create-card-footer">
                        <span class="create-card-label">Tạo tin mới</span>
                    </div>
                </div>
            }
        }"""

content = content.replace(old_card_section.replace('\r\n', '\n'), new_card_section.replace('\r\n', '\n'))

# 4. Update empty hints
old_empty = """        else if (currentUserId > 0 && (currentUserGroup == null || currentUserGroup.Stories.Count == 0))
        {
            <div class="story-tray-empty-hint">
                <i class="fa-regular fa-clock" style="font-size: 1.2rem; color: var(--tdmu-cyan, #00c8ff);"></i>
                <span>Chưa có tin 24h nào từ bạn bè. Hãy là người đầu tiên chia sẻ khoảnh khắc hôm nay!</span>
            </div>
        }"""

new_empty = """        else if (isLoggedIn && (currentUserGroup == null || currentUserGroup.Stories.Count == 0))
        {
            <div class="story-tray-empty-hint">
                <i class="fa-regular fa-clock" style="font-size: 1.2rem; color: var(--tdmu-cyan, #00c8ff);"></i>
                <span>Chưa có tin 24h nào từ bạn bè. Hãy là người đầu tiên chia sẻ khoảnh khắc hôm nay!</span>
            </div>
        }
        else if (!isLoggedIn && !otherGroups.Any())
        {
            <div class="story-tray-empty-hint">
                <i class="fa-regular fa-clock" style="font-size: 1.2rem; color: var(--tdmu-cyan, #00c8ff);"></i>
                <span>Hiện chưa có tin 24h nào. Hãy đăng nhập để chia sẻ khoảnh khắc cùng sinh viên TDMU!</span>
            </div>
        }"""

content = content.replace(old_empty.replace('\r\n', '\n'), new_empty.replace('\r\n', '\n'))

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print('Updated _StoryTrayPartial.cshtml successfully.')
