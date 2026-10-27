import re

path = r"Views\User\Profile.cshtml"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update cover image and avatar to have decoding="async"
content = re.sub(
    r'(<img\s+src="@coverSrc"[^>]*)(>)',
    r'\1 decoding="async"\2',
    content
)
content = re.sub(
    r'(<img\s+src="@avatarSrc"\s+alt="@Model\.Username"\s+class="profile-avatar-img"[^>]*)(>)',
    r'\1 decoding="async"\2',
    content
)

# 2. Add loading="lazy" decoding="async" to highlight cover
content = re.sub(
    r'(<img\s+src="@hl\.CoverUrl"\s+class="highlight-cover-img"[^>]*)(>)',
    r'\1 loading="lazy" decoding="async"\2',
    content
)

# 3. Add loading="lazy" decoding="async" to friend preview avatar
content = re.sub(
    r'(<img\s+src="@friend\.Avatar"\s+class="friend-preview-avatar"[^>]*)(>)',
    r'\1 loading="lazy" decoding="async"\2',
    content
)

# 4. Add loading="lazy" decoding="async" to photo-thumb-img
content = re.sub(
    r'(<img\s+src="@photo"\s+class="photo-thumb-img"[^>]*)(>)',
    r'\1 loading="lazy" decoding="async"\2',
    content
)

# 5. Add loading="lazy" decoding="async" to post author avatar
content = re.sub(
    r'(<img\s+src="@post\.AuthorAvatar"\s+class="post-author-avatar"[^>]*)(>)',
    r'\1 loading="lazy" decoding="async"\2',
    content
)

# 6. Add loading="lazy" decoding="async" to post media img
content = re.sub(
    r'(<img\s+src="@media\.MediaUrl"\s+alt="Media"[^>]*)(>)',
    r'\1 loading="lazy" decoding="async"\2',
    content
)

# 7. Add loading="lazy" decoding="async" to comment avatar
content = re.sub(
    r'(<img\s+src="@c\.AuthorAvatar"\s+class="comment-avatar"[^>]*)(>)',
    r'\1 loading="lazy" decoding="async"\2',
    content
)

# 8. Add loading="lazy" decoding="async" to reply avatar
content = re.sub(
    r'(<img\s+src="@reply\.AuthorAvatar"\s+class="comment-avatar comment-reply-avatar"[^>]*)(>)',
    r'\1 loading="lazy" decoding="async"\2',
    content
)

# 9. Add loading="lazy" decoding="async" to connect card img
content = re.sub(
    r'(<img\s+src="@item\.MainImage"\s+class="connect-card-img"[^>]*)(>)',
    r'\1 loading="lazy" decoding="async"\2',
    content
)

# 10. Improve switchTimelineTab function for smooth transition and race condition prevention
old_tab_switch = """        function switchTimelineTab(tabName, element) {

            $('.nav-tab-item').removeClass('active');

            if (element) {

                $(element).addClass('active');

            } else {

                if (tabName === 'posts') $('.nav-tab-item:eq(0)').addClass('active');

                if (tabName === 'market') $('.nav-tab-item:eq(1)').addClass('active');

                if (tabName === 'connect') $('.nav-tab-item:eq(2)').addClass('active');

            }



            $('#tabContentPosts').hide();

            $('#tabContentMarket').hide();

            $('#tabContentConnect').hide();



            if (tabName === 'posts') $('#tabContentPosts').show();

            if (tabName === 'market') $('#tabContentMarket').show();

            if (tabName === 'connect') $('#tabContentConnect').show();



            if (history.replaceState) {

                history.replaceState(null, null, '#' + tabName);

            }

        }"""

new_tab_switch = """        var _activeTabSwitching = false;
        function switchTimelineTab(tabName, element) {
            if (!tabName) tabName = 'posts';
            $('.nav-tab-item').removeClass('active');

            if (element) {
                $(element).addClass('active');
            } else {
                if (tabName === 'posts') $('.nav-tab-item:eq(0)').addClass('active');
                if (tabName === 'market') $('.nav-tab-item:eq(1)').addClass('active');
                if (tabName === 'connect') $('.nav-tab-item:eq(2)').addClass('active');
            }

            var targetContent = null;
            if (tabName === 'posts') targetContent = $('#tabContentPosts');
            else if (tabName === 'market') targetContent = $('#tabContentMarket');
            else if (tabName === 'connect') targetContent = $('#tabContentConnect');

            $('#tabContentPosts, #tabContentMarket, #tabContentConnect').hide();
            if (targetContent) {
                targetContent.show();
            }

            if (history.replaceState) {
                history.replaceState(null, null, '#' + tabName);
            }
        }"""

if old_tab_switch in content:
    content = content.replace(old_tab_switch, new_tab_switch)
    print("Replaced switchTimelineTab successfully!")
else:
    # Try normalized newlines
    old_tab_crlf = old_tab_switch.replace("\n", "\r\n")
    new_tab_crlf = new_tab_switch.replace("\n", "\r\n")
    if old_tab_crlf in content:
        content = content.replace(old_tab_crlf, new_tab_crlf)
        print("Replaced switchTimelineTab (CRLF) successfully!")
    else:
        print("Warning: old_tab_switch not matched, looking for pattern...")
        content = re.sub(
            r'function switchTimelineTab\(tabName, element\)\s*\{[\s\S]*?if\s*\(history\.replaceState\)[\s\S]*?\}\s*\}',
            new_tab_switch.strip(),
            content
        )
        print("Regex replaced switchTimelineTab!")

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("Updated Views/User/Profile.cshtml successfully!")
