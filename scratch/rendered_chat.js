
    const myId   = parseInt('1');
    const roomId = parseInt('0');
    const chat   = $.connection.chatHub;
    const chatBox = document.getElementById("chatBox");
    const isPrivatePage = "";

    let replyingToId = 0, replyingToContent = '';

    // ── DEBOUNCED SIDEBAR LOADER (Chống thundering herd / spam request storm) ──
    let _chatListDebounceTimer = null;
    function debouncedReloadChatList(delay = 800) {
        if (_chatListDebounceTimer) clearTimeout(_chatListDebounceTimer);
        _chatListDebounceTimer = setTimeout(function() {
            loadSidebarChatList();
        }, delay);
    }

    // ── PAGINATED MESSAGE HISTORY (Cursor-based) ──
    let oldestMessageId = 0 ? Model.Min(m => m.MessageID) : 0);
    let hasMoreHistory = 0 >= 30 ? "true" : "false");
    let isLoadingHistory = false;

    function renderHistoryMessageItem(m) {
        const isMe = (m.senderId == myId);
        const msgId = m.messageId || 0;
        const safeMsg = $('<div/>').text(m.content || "").html();
        const safeReply = $('<div/>').text(m.replyContent || "").html();
        const replyHtml = (m.replyToId && m.replyContent)
            ? `<div class="reply-preview-msg" onclick="scrollToMsg(${m.replyToId})">↩ ${safeReply.length > 40 ? safeReply.substring(0,40)+'...' : safeReply}</div>` : '';

        let contentHtml = '';
        let rawForReply = safeMsg;

        if (m.isHidden) {
            contentHtml = `<div class="msg msg-text" style="background:rgba(239,68,68,0.12); border:1px solid rgba(239,68,68,0.3); color:#fca5a5; font-style:italic; font-size:12.5px;"><i class="fa-solid fa-shield-halved me-1"></i> Tin nhắn này đã bị quản trị viên ẩn.</div>`;
        } else if (m.isRevoked) {
            contentHtml = `<div class="msg msg-text revoked-style"><i class="fa-solid fa-circle-info" style="font-size:11px;opacity:0.5;margin-right:4px;"></i>Tin nhắn đã được thu hồi</div>`;
        } else if (m.content && m.content.startsWith("[ATTACHMENTS_PAYLOAD]:")) {
            try {
                const payloadJson = m.content.substring("[ATTACHMENTS_PAYLOAD]:".length);
                const payload = JSON.parse(payloadJson);
                const payloadText = payload.Text || "";
                const items = payload.Items || [];
                rawForReply = payloadText || (items.length > 0 ? "[Tệp đính kèm]" : "");

                const imageItems = items.filter(i => i.IsImage);
                const docItems = items.filter(i => !i.IsImage);

                let imgHtml = '';
                if (imageItems.length === 1) {
                    imgHtml = `<div class="msg msg-image"><img src="${imageItems[0].Url}" class="chat-img" onclick="viewImage('${imageItems[0].Url}')"/></div>`;
                } else if (imageItems.length > 1) {
                    let gridInner = imageItems.map(im => `<img src="${im.Url}" class="chat-grid-img" onclick="viewImage('${im.Url}')"/>`).join('');
                    imgHtml = `<div class="msg-images-grid">${gridInner}</div>`;
                }

                let textPartHtml = payloadText ? `<div class="msg msg-text">${$('<div/>').text(payloadText).html()}</div>` : '';

                let docHtml = '';
                if (docItems.length > 0) {
                    let cards = docItems.map(d => {
                        const iconInfo = (typeof window.getFileIconInfo === 'function') ? window.getFileIconInfo(d.Extension) : { icon: 'fa-file', cssClass: 'file-icon-default' };
                        const safeName = $('<div/>').text(d.FileName).html();
                        const dlUrl = `/Chat/DownloadAttachment?messageId=${msgId}&fileKey=${encodeURIComponent(d.FileKey || '')}`;
                        const extUpper = (d.Extension || '').replace('.', '').toUpperCase();
                        return `
                            <div class="msg-file-card" onclick="window.location.href='${dlUrl}'">
                                <div class="file-icon-box ${iconInfo.cssClass}">
                                    <i class="fa-solid ${iconInfo.icon}"></i>
                                </div>
                                <div class="file-details">
                                    <div class="file-name" title="${safeName}">${safeName}</div>
                                    <div class="file-sub">
                                        <span>${d.FormattedSize || ''}</span>
                                        <span>•</span>
                                        <span class="file-ext-badge">${extUpper}</span>
                                    </div>
                                </div>
                                <a href="${dlUrl}" class="file-dl-btn" title="Tải xuống tệp" onclick="event.stopPropagation();">
                                    <i class="fa-solid fa-cloud-arrow-down"></i>
                                </a>
                            </div>
                        `;
                    }).join('');
                    docHtml = `<div class="msg-files-list">${cards}</div>`;
                }

                contentHtml = imgHtml + textPartHtml + docHtml;
            } catch(e) {
                contentHtml = `<div class="msg msg-text">${safeMsg}</div>`;
            }
        } else {
            const hasImage = m.imageUrl && m.imageUrl !== "null" && m.imageUrl !== "";
            const imageHtml = hasImage
                ? `<div class="msg msg-image"><img src="${m.imageUrl}" class="chat-img" onclick="viewImage('${m.imageUrl}')"/></div>` : '';
            const textHtml = safeMsg ? `<div class="msg msg-text">${safeMsg}</div>` : '';
            contentHtml = imageHtml + textHtml;
        }

        const isAnon = (m.isAnonymous !== false && m.isAnonymous !== "false");
        const displayName = isAnon ? "Ẩn danh" : (m.senderName || "Sinh viên");
        const safeDisplayName = $('<div/>').text(displayName).html();
        const avatarImg = (!isAnon && m.senderAvatar && m.senderAvatar !== "null" && m.senderAvatar !== "") ? m.senderAvatar : "";

        let senderNameHtml = '';
        if (!isMe) {
            if (!isAnon) {
                senderNameHtml = `
                    <div class="msg-sender-name revealed">
                        <span class="name-text">${safeDisplayName}</span>
                        <span class="badge-sv">SV TDMU</span>
                    </div>`;
            } else {
                senderNameHtml = `
                    <div class="msg-sender-name anon">
                        <span class="badge-anon"><i class="fa-solid fa-user-secret me-1"></i>Ẩn danh</span>
                    </div>`;
            }
        }

        let avatarInner = '';
        if (!isAnon && avatarImg) {
            avatarInner = `<img src="${avatarImg}" class="avatar-img" alt="${safeDisplayName}" onerror="handleAvatarError(this)" />`;
        } else if (!isAnon) {
            const initial = displayName.length > 0 ? displayName.charAt(0).toUpperCase() : 'U';
            avatarInner = `<span class="avatar-initials">${initial}</span>`;
        } else {
            avatarInner = `<i class="fa-solid fa-user-secret"></i>`;
        }

        const avatarHtml = !isMe
            ? `<div class="avatar-wrapper" onclick="showUserMenu(event, ${m.senderId}, '${safeDisplayName.replace(/'/g, "\\'")}', '${avatarImg.replace(/'/g, "\\'")}', ${isAnon})"><div class="avatar ${isAnon ? 'anon' : 'revealed'}" title="${isAnon ? 'Người lạ ẩn danh' : safeDisplayName}">${avatarInner}</div></div>` : '';

        const actionsHtml = (msgId > 0 && !m.isRevoked) ? `
            <div class="msg-actions" style="position:relative;">
                <button class="msg-option-btn" onclick="toggleMsgMenu(event, ${msgId})"><i class="fa-solid fa-ellipsis-vertical"></i></button>
                <div class="msg-dropdown" id="msgMenu-${msgId}">
                    <a href="javascript:void(0)" onclick="setReply(${msgId}, '${rawForReply.replace(/'/g,"\\'")}')"><i class="fa-solid fa-reply"></i> Trả lời</a>
                    ${isMe ? `
                    <a href="javascript:void(0)" onclick="revokeMsg(${msgId})" style="color:rgba(255,160,0,0.8);"><i class="fa-solid fa-rotate-left"></i> Thu hồi</a>
                    <a href="javascript:void(0)" onclick="deleteMsg(${msgId})" style="color:rgba(255,80,80,0.8);"><i class="fa-solid fa-trash"></i> Xóa</a>` : `
                    <a href="javascript:void(0)" onclick="showReportModal(${roomId}, ${isAnon ? 0 : m.senderId}, ${msgId})" style="color:rgba(255,100,100,0.9);"><i class="fa-solid fa-flag"></i> Tố cáo</a>`}
                </div>
            </div>` : '';

        const modeBadge = isAnon
            ? `<span class="ts-mode anon" title="Tin nhắn gửi ẩn danh"><i class="fa-solid fa-mask"></i></span>`
            : `<span class="ts-mode public" title="Tin nhắn gửi công khai"><i class="fa-solid fa-user-check"></i></span>`;

        return `
            <div class="msg-wrapper ${isMe ? 'me' : 'other'}" ${msgId > 0 ? `id="msg-${msgId}"` : ''}>
                ${senderNameHtml}
                <div class="msg-row">
                    ${avatarHtml}
                    <div class="msg-container">
                        <div class="msg-content">${replyHtml}${contentHtml}<div class="reaction-badge"></div></div>
                        ${actionsHtml}
                    </div>
                </div>
                <div class="timestamp">${m.sentAt || ''} ${modeBadge}</div>
            </div>`;
    }

    function loadEarlierMessages() {
        if (isLoadingHistory || !hasMoreHistory || oldestMessageId <= 0) return;
        isLoadingHistory = true;
        $('#chatHistoryLoader').show();
        $('#btnLoadMoreHistory').hide();

        $.get('/ChatApi/GetMessageHistory', { roomId: roomId, beforeMessageId: oldestMessageId, take: 30 }, function(res) {
            $('#chatHistoryLoader').hide();
            isLoadingHistory = false;

            if (res && res.success && res.messages && res.messages.length > 0) {
                const prevScrollHeight = chatBox.scrollHeight;
                let htmlBlock = '';
                for (let i = 0; i < res.messages.length; i++) {
                    htmlBlock += renderHistoryMessageItem(res.messages[i]);
                }

                $('#btnLoadMoreHistory').after(htmlBlock);
                chatBox.scrollTop = chatBox.scrollHeight - prevScrollHeight;

                oldestMessageId = res.messages[0].messageId;
                hasMoreHistory = res.hasMore;
                if (hasMoreHistory) {
                    $('#btnLoadMoreHistory').show();
                }
            } else {
                hasMoreHistory = false;
                $('#btnLoadMoreHistory').hide();
            }
        }).fail(function() {
            isLoadingHistory = false;
            $('#chatHistoryLoader').hide();
            $('#btnLoadMoreHistory').show();
        });
    }

    // ── SIDEBAR CHAT LIST LOADER ──
    function loadSidebarChatList() {
        $.get('/Chat/GetChatList', { currentRoomId: roomId }, function(html) {
            $('#chatListContainer').html(html);
            filterSidebarList();
        });
    }

    function openRoom(targetRoomId) {
        window.location.href = '/Chat/Private/' + targetRoomId;
    }

    function filterSidebarList() {
        var query = ($('#sidebarSearchInput').val() || '').toLowerCase().trim();
        $('.chat-list-item').each(function() {
            var name = ($(this).data('name') || '').toString();
            if (!query || name.indexOf(query) !== -1) {
                $(this).show();
            } else {
                $(this).hide();
            }
        });
    }

    // ── PROMPT RENAME ROOM ──
    function promptRenameRoom(targetRoomId, currentName) {
        Swal.fire({
            title: 'Đổi tên cuộc trò chuyện',
            input: 'text',
            inputValue: currentName || '',
            inputPlaceholder: 'Nhập tên mới cho cuộc trò chuyện...',
            showCancelButton: true,
            confirmButtonText: 'Lưu thay đổi',
            cancelButtonText: 'Hủy',
            inputValidator: (value) => {
                if (!value || !value.trim()) {
                    return 'Tên cuộc trò chuyện không được để trống!';
                }
            }
        }).then((result) => {
            if (result.isConfirmed) {
                var newName = result.value.trim();
                $.post('/Chat/RenameRoom', { roomId: targetRoomId, newName: newName }, function(res) {
                    if (res.success) {
                        Swal.fire({ icon: 'success', title: 'Thành công', text: 'Đã đổi tên cuộc trò chuyện!', toast: true, position: 'top-end', timer: 2500, showConfirmButton: false });
                        if (targetRoomId === roomId) {
                            $('#roomTitleText').text(newName);
                        }
                        $(`#sidebar-room-name-${targetRoomId}`).text(newName);
                        loadSidebarChatList();
                    } else {
                        Swal.fire('Lỗi', res.message || 'Không thể đổi tên.', 'error');
                    }
                });
            }
        });
    }

    // ── CONFIRM DELETE CONVERSATION ──
    function confirmDeleteConversation(targetRoomId, name) {
        Swal.fire({
            title: 'Xóa cuộc trò chuyện?',
            text: `Bạn có chắc chắn muốn rời và xóa "${name}" khỏi danh sách?`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#ff4d4d',
            confirmButtonText: 'Xóa ngay',
            cancelButtonText: 'Hủy'
        }).then((result) => {
            if (result.isConfirmed) {
                $.ajax({
                    url: '/Chat/DeleteConversation',
                    type: 'POST',
                    data: {
                        roomId: targetRoomId,
                        __RequestVerificationToken: getChatAntiForgeryToken()
                    },
                    success: function (res) {
                        if (res.success) {
                            Swal.fire({ icon: 'success', title: 'Đã xóa', text: 'Đã xóa cuộc trò chuyện.', toast: true, position: 'top-end', timer: 2000, showConfirmButton: false });
                            if (targetRoomId === roomId) {
                                window.location.href = '/Chat';
                            } else {
                                loadSidebarChatList();
                            }
                        } else {
                            Swal.fire('Lỗi', res.message || 'Không thể xóa.', 'error');
                        }
                    },
                    error: function (xhr) {
                        let msg = 'Không thể xóa cuộc trò chuyện.';
                        try { const j = JSON.parse(xhr.responseText); if (j && j.message) msg = j.message; } catch (e) {}
                        Swal.fire('Lỗi', msg, 'error');
                    }
                });
            }
        });
    }

    // ── MODAL THÊM THÀNH VIÊN NGAY TRONG NHÓM ──
    function showInviteMemberModal() {
        Swal.fire({
            title: 'Thêm thành viên',
            html: `
            <div style="text-align:left; font-family:'Space Grotesk',sans-serif;">
                <p style="font-size:13px; color:rgba(255,255,255,0.7); margin-bottom:12px;">
                    Nhập email sinh viên TDMU để thêm vào nhóm này:
                </p>
                <label style="font-size:11px; color:rgba(255,255,255,0.4); letter-spacing:0.08em; font-family:'JetBrains Mono',monospace;">EMAIL</label>
                <input id="swal-invite-email" class="swal2-input" placeholder="example@0.tdmu.edu.vn" style="margin:6px 0 10px;">
            </div>`,
            showCancelButton: true,
            confirmButtonText: 'Thêm vào nhóm',
            cancelButtonText: 'Hủy',
            preConfirm: () => {
                const email = document.getElementById('swal-invite-email').value.trim();
                if (!email) {
                    Swal.showValidationMessage('Vui lòng nhập email sinh viên');
                    return false;
                }
                if (!email.endsWith('1')) {
                    Swal.showValidationMessage('Chỉ chấp nhận email sinh viên TDMU (@0.tdmu.edu.vn)');
                    return false;
                }
                return email;
            }
        }).then(result => {
            if (result.isConfirmed) {
                $.post('/Chat/InviteToGroup', { roomId: roomId, email: result.value }, function(res) {
                    if (res.success) {
                        Swal.fire({ icon: 'success', title: 'Thành công', text: res.message, toast: true, position: 'top-end', timer: 3000, showConfirmButton: false });
                        loadSidebarChatList();
                    } else {
                        Swal.fire('Lỗi', res.message || 'Không thể thêm thành viên.', 'error');
                    }
                });
            }
        });
    }

    // ── MODAL TẠO NHÓM 5 ──
    function showCreateGroupModal() {
        let emailCount = 1;
        Swal.fire({
            title: 'Tạo nhóm 5',
            html: `
            <div style="text-align:left; font-family:'Space Grotesk',sans-serif;">
                <label style="font-size:11px; color:rgba(255,255,255,0.4); letter-spacing:0.08em; font-family:'JetBrains Mono',monospace;">TÊN NHÓM</label>
                <input id="swal-groupName" class="swal2-input" placeholder="Ví dụ: Nhóm học tập CNPM, Nhóm 5 bạn..." style="margin:6px 0 16px;">
                <label style="font-size:11px; color:rgba(255,255,255,0.4); letter-spacing:0.08em; font-family:'JetBrains Mono',monospace;">THÀNH VIÊN (TỐI ĐA 5)</label>
                <div id="email-inputs-container">
                    <div class="email-input-item" style="display:flex; margin-top:6px; margin-bottom:8px; gap:6px;">
                        <input class="swal2-input email-list-input" placeholder="Email thành viên 1 (@0.tdmu.edu.vn)" style="margin:0; flex:1;">
                        <button type="button" id="add-email-btn" class="swal2-confirm swal2-styled" style="min-width:40px; padding:0 12px;"><i class="fa fa-plus"></i></button>
                    </div>
                </div>
            </div>`,
            showCancelButton: true,
            confirmButtonText: 'Tạo nhóm ngay',
            cancelButtonText: 'Hủy',
            didOpen: () => {
                const container = document.getElementById('email-inputs-container');
                document.getElementById('add-email-btn').addEventListener('click', () => {
                    if (document.querySelectorAll('.email-list-input').length >= 5) {
                        Swal.showValidationMessage('Tối đa chỉ được mời 5 thành viên!'); return;
                    }
                    emailCount++;
                    const newDiv = document.createElement('div');
                    newDiv.className = 'email-input-item';
                    newDiv.style.cssText = 'display:flex; margin-bottom:8px; gap:6px;';
                    newDiv.innerHTML = `
                        <input class="swal2-input email-list-input" placeholder="Email thành viên ${emailCount} (@0.tdmu.edu.vn)" style="margin:0; flex:1;">
                        <button type="button" class="swal2-deny swal2-styled remove-email-btn" style="min-width:40px; padding:0 12px;"><i class="fa fa-minus"></i></button>`;
                    container.appendChild(newDiv);
                    newDiv.querySelector('.remove-email-btn').addEventListener('click', () => newDiv.remove());
                });
            },
            preConfirm: () => {
                const groupName = document.getElementById('swal-groupName').value.trim();
                const emails = Array.from(document.querySelectorAll('.email-list-input')).map(i => i.value.trim()).filter(e => e !== "");
                if (!groupName) { Swal.showValidationMessage('Vui lòng nhập tên nhóm'); return false; }
                if (emails.length === 0) { Swal.showValidationMessage('Vui lòng nhập ít nhất một email'); return false; }
                return { groupName, emails };
            }
        }).then(result => {
            if (result.isConfirmed) {
                $.post('/Chat/CreateGroupWithEmails', { groupName: result.value.groupName, emails: result.value.emails }, function(res) {
                    if (res.success) window.location.href = '/Chat/Private/' + res.roomId;
                    else Swal.fire('Lỗi', res.message, 'error');
                });
            }
        });
    }

    // ── RỜI PHÒNG ──
    function leaveRoom() {
        if (!isPrivatePage) return;
        Swal.fire({
            title: 'Xác nhận rời phòng?',
            text: 'Bạn sẽ không nhận tin nhắn từ cuộc trò chuyện này nữa.',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#ff4d4d',
            confirmButtonText: 'Rời đi',
            cancelButtonText: 'Hủy'
        }).then((result) => {
            if (result.isConfirmed) {
                $.post('/Chat/LeaveRoom', { roomId: roomId }, function(res) {
                    if (res.success) {
                        chat.server.leaveRoomSignalR(roomId).done(() => { window.location.href = '/Chat/Index'; });
                    } else {
                        window.location.href = '/Chat/Index';
                    }
                });
            }
        });
    }

    // ── MODAL TỐ CÁO VI PHẠM (GỬI ADMIN) ──
    function showReportModal(targetRoomId, targetUserId, messageId) {
        const rId = targetRoomId || roomId;
        Swal.fire({
            title: '🚨 Báo cáo vi phạm',
            html: `
            <div style="text-align:left; font-family:'Space Grotesk',sans-serif; font-size:13px; color:rgba(255,255,255,0.85);">
                <p style="margin-bottom:12px; color:#8b94a5;">
                    Vui lòng chọn lý do và mô tả hành vi vi phạm (ngôn từ xúc phạm, spam, quấy rối, lừa đảo...):
                </p>
                <label style="font-size:11px; color:#00c8ff; font-weight:600; letter-spacing:0.05em;">LÝ DO VI PHẠM</label>
                <select id="swal-report-reason" class="swal2-select" style="display:block; width:100%; margin:6px 0 14px; background:#0d1626; color:#fff; border:1px solid rgba(255,255,255,0.15); border-radius:8px; padding:8px;">
                    <option value="Phát ngôn thù ghét, xúc phạm, lăng mạ">Phát ngôn thù ghét, xúc phạm, lăng mạ</option>
                    <option value="Quấy rối, đe dọa người khác">Quấy rối, đe dọa người khác</option>
                    <option value="Spam, quảng cáo trái phép">Spam, quảng cáo trái phép</option>
                    <option value="Lừa đảo, gian lận">Lừa đảo, gian lận</option>
                    <option value="Nội dung phản cảm, đồi trụy">Nội dung phản cảm, đồi trụy</option>
                    <option value="Khác">Lý do khác</option>
                </select>
                <label style="font-size:11px; color:#00c8ff; font-weight:600; letter-spacing:0.05em;">MÔ TẢ CHI TIẾT (TÙY CHỌN)</label>
                <textarea id="swal-report-details" class="swal2-textarea" placeholder="Nhập thêm chi tiết nếu cần..." style="margin:6px 0 0; width:100%; height:75px; background:#0d1626; color:#fff; border:1px solid rgba(255,255,255,0.15); border-radius:8px; padding:8px;"></textarea>
            </div>`,
            showCancelButton: true,
            confirmButtonText: 'Gửi báo cáo',
            cancelButtonText: 'Hủy',
            confirmButtonColor: '#e24b4a',
            preConfirm: () => {
                const reason = document.getElementById('swal-report-reason').value;
                const details = document.getElementById('swal-report-details').value.trim();
                return { reason, details };
            }
        }).then((result) => {
            if (result.isConfirmed) {
                $.post('/Chat/ReportViolation', {
                    roomId: rId,
                    reportedUserId: targetUserId,
                    messageId: messageId,
                    reason: result.value.reason,
                    details: result.value.details
                }, function(res) {
                    if (res.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'Đã gửi báo cáo',
                            text: res.message,
                            timer: 3000,
                            showConfirmButton: false
                        });
                    } else {
                        Swal.fire('Thông báo', res.message || 'Không thể gửi báo cáo.', 'error');
                    }
                });
            }
        });
    }

    // ── DRAWER SIDEBAR RESPONSIVE ──
    function toggleSidebarDrawer() {
        $('#chatSidebar').toggleClass('open');
        $('#sidebarBackdrop').toggleClass('active');
    }

    // ── REPLY & SCROLL ──
    function setReply(msgId, content) {
        replyingToId = msgId; replyingToContent = content;
        $('#replyPreview').show();
        $('#replyText').text(content.length > 50 ? content.substring(0,50)+'...' : content);
        $('#txtMessage').focus();
    }
    function cancelReply() {
        replyingToId = 0; replyingToContent = '';
        $('#replyPreview').hide();
    }
    function scrollToMsg(msgId) {
        const el = document.getElementById('msg-' + msgId);
        if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            $(el).css('outline', '1px solid rgba(0,200,255,0.4)');
            setTimeout(() => $(el).css('outline', ''), 1500);
        }
    }
    function getCurrentTime() {
        return new Date().toLocaleTimeString([], { hour:'2-digit', minute:'2-digit', hour12:false });
    }
    function viewImage(url) {
        $('#modalImage').attr('src', url);
        $('#imageModal').css('display', 'flex');
    }

    // ── CHẾ ĐỘ ẨN DANH / HIỆN DANH TÍNH ──
    let isAnonymousMode = true; // Mặc định: Chế độ ẩn danh BẬT

    function updateAnonymousToggleUI() {
        const btn = $('#btnToggleAnon');
        const input = $('#txtMessage');
        if (isAnonymousMode) {
            btn.addClass('active').removeClass('revealed');
            btn.attr('title', 'Chế độ ẩn danh: BẬT (Bấm để hiện danh tính)');
            btn.html('<i class="fa-solid fa-user-secret"></i>');
            input.attr('placeholder', 'Nhập tin nhắn ẩn danh...');
        } else {
            btn.removeClass('active').addClass('revealed');
            btn.attr('title', 'Chế độ hiện danh tính: BẬT (Bấm để ẩn danh)');
            btn.html('<i class="fa-solid fa-user-check"></i>');
            input.attr('placeholder', 'Nhập tin nhắn công khai...');
        }
    }

    function toggleAnonymousMode() {
        isAnonymousMode = !isAnonymousMode;
        updateAnonymousToggleUI();
        const modeName = isAnonymousMode ? 'Chế độ Ẩn danh: BẬT' : 'Hiện danh tính thật: BẬT';
        const modeIcon = isAnonymousMode ? 'info' : 'success';
        Swal.fire({
            toast: true,
            position: 'top-end',
            icon: modeIcon,
            title: modeName,
            text: isAnonymousMode ? 'Tin nhắn của bạn sẽ được ẩn danh hoàn toàn.' : 'Tin nhắn của bạn sẽ hiện tên thật và avatar.',
            showConfirmButton: false,
            timer: 2500
        });
    }

    let currentTargetUserId = 0;
    let currentTargetName = "Người lạ";
    let currentTargetAvatar = "";
    let currentTargetIsAnon = true;

    function closeUserMenu() {
        $('#userContextMenu').removeClass('active').hide();
        $('#userMenuBackdrop').removeClass('active');
    }

    function showUserMenu(event, userId, displayName, avatarUrl, isAnon) {
        if (event) {
            event.preventDefault(); 
            event.stopPropagation();
        }

        const currentUserId = (typeof myId !== 'undefined' && myId) ? parseInt(myId) : 0;
        // Không mở menu cho chính mình
        if (currentUserId > 0 && parseInt(userId) === currentUserId) return;

        currentTargetUserId = parseInt(userId);
        currentTargetName = displayName || ("Người lạ #" + userId);
        currentTargetAvatar = avatarUrl || "";
        currentTargetIsAnon = (isAnon === true || isAnon === "true" || isAnon === 1 || isAnon === "1");

        // Cập nhật thông tin trên header của menu
        $('#menuTargetName').text(currentTargetName);
        if (currentTargetIsAnon) {
            $('#menuTargetAvatar').html('<i class="fa-solid fa-user-secret"></i>');
            $('#menuTargetMeta').html('<span class="badge-anon"><i class="fa-solid fa-user-secret me-1"></i>Ẩn danh</span>');
            $('#menuItemViewProfile').hide();
        } else {
            if (currentTargetAvatar) {
                $('#menuTargetAvatar').html(`<img src="${currentTargetAvatar}" alt="${currentTargetName}" onerror="handleAvatarError(this)" />`);
            } else {
                const initial = currentTargetName.length > 0 ? currentTargetName.charAt(0).toUpperCase() : 'U';
                $('#menuTargetAvatar').html(`<b>${initial}</b>`);
            }
            $('#menuTargetMeta').html('<span class="badge-sv"><i class="fa-solid fa-graduation-cap me-1"></i>SV</span>');
            $('#menuItemViewProfile').show();
        }

        // Đóng các menu 3 chấm nếu đang mở
        $('.msg-dropdown').hide();
        $('.msg-actions').removeClass('is-active');

        const menu = $('#userContextMenu');
        const backdrop = $('#userMenuBackdrop');
        const isMobile = $(window).width() <= 768;

        backdrop.addClass('active');
        menu.show().addClass('active');

        if (!isMobile) {
            const winW = $(window).width();
            const winH = $(window).height();
            const menuW = 280;
            const menuH = 260;

            let left = 20;
            let top = 80;

            if (event && typeof event.clientX === 'number' && event.clientX > 0) {
                left = event.clientX;
                top = event.clientY;
            } else if (event && event.currentTarget) {
                const rect = event.currentTarget.getBoundingClientRect();
                left = rect.right + 10;
                top = rect.top;
            }

            // Xử lý không cho menu tràn màn hình PC
            if (left + menuW > winW - 14) {
                left = Math.max(14, winW - menuW - 14);
            }
            if (top + menuH > winH - 14) {
                top = Math.max(14, winH - menuH - 14);
            }

            menu.css({ top: top + 'px', left: left + 'px', right: 'auto', bottom: 'auto' });
        } else {
            menu.css({ top: '', left: '', right: '', bottom: '' });
        }
    }

    function handleViewProfileFromMenu() {
        closeUserMenu();
        if (currentTargetUserId > 0) {
            window.location.href = '/User/Profile/' + currentTargetUserId;
        }
    }

    function handlePrivateChatFromMenu() {
        closeUserMenu();
        if (currentTargetUserId > 0) {
            startPrivateChat(currentTargetUserId, currentTargetName);
        }
    }
    function handleReportFromUserMenu() {
        closeUserMenu();
        showReportModal(roomId, currentTargetIsAnon ? 0 : currentTargetUserId, null);
    }

    function toggleMsgMenu(event, msgId) {
        if (event) {
            event.stopPropagation();
            event.preventDefault();
        }
        const menu = $(`#msgMenu-${msgId}`);
        if (menu.is(':visible')) {
            menu.hide();
            $('.msg-actions').removeClass('is-active');
            return;
        }

        // Đóng tất cả menu khác
        $('.msg-dropdown').hide();
        $('.msg-actions').removeClass('is-active');

        const btn = $(event.currentTarget);
        const actions = btn.closest('.msg-actions');
        actions.addClass('is-active');

        // Lật lên trên (dropup) nếu tin nhắn nằm gần đáy khung chat
        const btnEl = btn[0];
        if (btnEl) {
            const btnRect = btnEl.getBoundingClientRect();
            const chatBoxEl = document.getElementById('chatBox');
            const chatRect = chatBoxEl ? chatBoxEl.getBoundingClientRect() : { bottom: window.innerHeight };
            if (chatRect.bottom - btnRect.bottom < 140) {
                menu.addClass('dropup');
            } else {
                menu.removeClass('dropup');
            }
        }

        menu.show();
    }

    function handleAvatarClick(event, senderId, isAnon) {
        if (event) {
            event.stopPropagation();
            event.preventDefault();
        }
        const anon = (isAnon === true || isAnon === "true" || isAnon === 1 || isAnon === "1");
        if (anon) {
            Swal.fire({
                icon: 'info',
                title: 'Người dùng ẩn danh',
                text: 'Tin nhắn này được gửi ở chế độ ẩn danh. Danh tính người gửi được bảo mật.',
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000,
                background: '#0d1626',
                color: '#fff'
            });
            return;
        }
        if (senderId && parseInt(senderId) > 0) {
            window.location.href = '/User/Profile/' + senderId;
        }
    }

    function showAnonRoomInfo() {
        Swal.fire({
            icon: 'info',
            title: 'Phòng chat Ẩn danh',
            text: 'Cuộc trò chuyện này đang ở chế độ ẩn danh. Danh tính người tham gia được bảo vệ.',
            background: '#0d1626',
            color: '#fff',
            confirmButtonText: 'Đã hiểu'
        });
    }

    function showRoomMembersModal(roomId) {
        if (!roomId) roomId = rId;
        Swal.fire({
            title: 'Đang tải danh sách...',
            text: 'Vui lòng chờ trong giây lát',
            allowOutsideClick: false,
            background: '#0d1626',
            color: '#fff',
            didOpen: () => {
                Swal.showLoading();
                $.get('/Chat/GetRoomMembers', { roomId: roomId }, function(res) {
                    if (!res || !res.success) {
                        Swal.fire({
                            icon: 'error',
                            title: 'Lỗi',
                            text: res ? res.message : 'Không thể tải danh sách thành viên.',
                            background: '#0d1626',
                            color: '#fff'
                        });
                        return;
                    }
                    const members = res.members || [];
                    if (members.length === 0) {
                        Swal.fire({
                            icon: 'info',
                            title: 'Thành viên nhóm',
                            text: 'Chưa có thành viên nào trong nhóm.',
                            background: '#0d1626',
                            color: '#fff'
                        });
                        return;
                    }
                    let memberItems = members.map(m => {
                        const avatarUrl = m.avatar || '/Content/Images/default-avatar.png';
                        const isMeTag = m.isMe ? '<span style="font-size:10px; background:rgba(0,200,255,0.2); color:#00c8ff; padding:2px 6px; border-radius:10px; margin-left:6px;">Tôi</span>' : '';
                        const tdmuBadge = m.isTDMUStudent ? '<span style="font-size:10px; background:rgba(42,190,2,0.2); color:#2abe02; padding:2px 6px; border-radius:10px; margin-left:6px;"><i class="fa-solid fa-graduation-cap"></i> TDMU</span>' : '';
                        return `
                            <div class="member-row" onclick="window.location.href='/User/Profile/${m.userId}'" style="display:flex; align-items:center; justify-content:space-between; padding:10px 12px; margin-bottom:8px; background:rgba(255,255,255,0.03); border:1px solid rgba(255,255,255,0.08); border-radius:12px; cursor:pointer; transition:all 0.2s;" onmouseover="this.style.background='rgba(0,200,255,0.08)';this.style.borderColor='rgba(0,200,255,0.3)';" onmouseout="this.style.background='rgba(255,255,255,0.03)';this.style.borderColor='rgba(255,255,255,0.08)';">
                                <div style="display:flex; align-items:center; gap:12px; text-align:left;">
                                    <img src="${avatarUrl}" style="width:38px; height:38px; border-radius:50%; object-fit:cover; border:1px solid rgba(0,200,255,0.4);" onerror="this.src='/Content/Images/default-avatar.png';" />
                                    <div>
                                        <div style="font-weight:600; font-size:14px; color:#fff;">${m.displayName} ${isMeTag} ${tdmuBadge}</div>
                                        <div style="font-size:12px; color:rgba(255,255,255,0.5);">@@${m.username}</div>
                                    </div>
                                </div>
                                <span style="font-size:12px; color:#00c8ff; display:flex; align-items:center; gap:4px;">
                                    <i class="fa-regular fa-user"></i> Hồ sơ
                                </span>
                            </div>
                        `;
                    }).join('');

                    Swal.fire({
                        title: `Thành viên nhóm (${members.length})`,
                        html: `
                            <div style="max-height:360px; overflow-y:auto; padding-right:4px; font-family:'Space Grotesk',sans-serif;">
                                ${memberItems}
                            </div>
                        `,
                        background: '#0d1626',
                        color: '#fff',
                        showConfirmButton: false,
                        showCloseButton: true,
                        width: '450px'
                    });
                }).fail(function() {
                    Swal.fire({
                        icon: 'error',
                        title: 'Lỗi',
                        text: 'Không thể kết nối đến máy chủ.',
                        background: '#0d1626',
                        color: '#fff'
                    });
                });
            }
        });
    }
    function startPrivateChat(targetId, targetName) {
        const titleText = currentTargetIsAnon ? 'Bắt đầu chat riêng ẩn danh?' : `Bắt đầu chat riêng với ${targetName}?`;
        const bodyText = currentTargetIsAnon ? `Kết nối ẩn danh với ${targetName}?` : `Mở cuộc trò chuyện riêng với ${targetName}?`;
        Swal.fire({
            title: titleText,
            text: bodyText,
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: 'Kết nối ngay',
            cancelButtonText: 'Hủy'
        }).then((result) => {
            if (result.isConfirmed) {
                $.post('/Chat/CreatePrivateRoom', { targetId }, function(res) {
                    if (res.success) {
                        const myInviteName = isAnonymousMode ? "Người lạ" : ('1' || "Sinh viên");
                        chat.server.inviteToPrivateChat(parseInt(targetId), myId, myInviteName, res.roomId);
                        window.location.href = '/Chat/Private/' + res.roomId;
                    } else Swal.fire('Lỗi', res.message, 'error');
                });
            }
        });
    }
    function joinRandomGroup() {
        $.post('/Chat/JoinRandomGroup', function(res) {
            if (res.success) window.location.href = '/Chat/Private/' + res.roomId;
            else Swal.fire('Lỗi', res.message || 'Không thể tham gia nhóm.', 'error');
        });
    }
    function joinRandomAnonymous() {
        Swal.fire({ title: 'Đang ghép đôi...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });
        $.post('/Chat/JoinRandomAnonymous', function(res) {
            if (res.success) window.location.href = '/Chat/Private/' + res.roomId;
            else Swal.fire('Lỗi', 'Không thể kết nối.', 'error');
        }).fail(() => Swal.fire('Lỗi', 'Mất kết nối máy chủ.', 'error'));
    }
    function getChatAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function revokeMsg(id) {
        Swal.fire({
            title: 'Thu hồi tin nhắn?',
            text: 'Mọi người sẽ thấy "Tin nhắn đã được thu hồi".',
            icon: 'warning', showCancelButton: true,
            confirmButtonText: 'Thu hồi', cancelButtonText: 'Hủy'
        }).then(r => {
            if (r.isConfirmed) {
                $.ajax({
                    url: '/Chat/RevokeMessage',
                    type: 'POST',
                    data: { messageId: id, __RequestVerificationToken: getChatAntiForgeryToken() },
                    success: function (res) {
                        if (!res.success) Swal.fire('Lỗi', res.message || 'Không thể thu hồi tin nhắn.', 'error');
                    },
                    error: function (xhr) {
                        let msg = 'Không thể thu hồi tin nhắn.';
                        try { const j = JSON.parse(xhr.responseText); if (j && j.message) msg = j.message; } catch (e) {}
                        Swal.fire('Lỗi', msg, 'error');
                    }
                });
            }
        });
    }
    function deleteMsg(id) {
        Swal.fire({
            title: 'Xóa tin nhắn?', text: 'Tin nhắn sẽ bị xóa hẳn khỏi lịch sử trong vòng 5 phút từ khi gửi.',
            icon: 'warning', showCancelButton: true,
            confirmButtonText: 'Xóa', cancelButtonText: 'Hủy'
        }).then(r => {
            if (r.isConfirmed) {
                $.ajax({
                    url: '/Chat/DeleteMessage',
                    type: 'POST',
                    data: { messageId: id, __RequestVerificationToken: getChatAntiForgeryToken() },
                    success: function (res) {
                        if (res.success) $(`#msg-${id}`).fadeOut(300, function() { $(this).remove(); });
                        else Swal.fire('Lỗi', res.message || 'Không thể xóa tin nhắn.', 'error');
                    },
                    error: function (xhr) {
                        let msg = 'Không thể xóa tin nhắn.';
                        try { const j = JSON.parse(xhr.responseText); if (j && j.message) msg = j.message; } catch (e) {}
                        Swal.fire('Lỗi', msg, 'error');
                    }
                });
            }
        });
    }

    $(document).ready(function () {
        const input = $('#txtMessage');
        const rId = parseInt(roomId), mId = parseInt(myId);

        // Khởi tạo UI chế độ ẩn danh
        updateAnonymousToggleUI();

        // Nạp danh sách cuộc trò chuyện bên Sidebar
        loadSidebarChatList();

        // Toggle Sidebar trên Mobile / Tablet
        $('#btnToggleSidebar').click(function(e) {
            e.stopPropagation();
            toggleSidebarDrawer();
        });

        // Đóng sidebar khi chọn item trên màn hình nhỏ
        $(document).on('click', '.chat-list-item', function() {
            if ($(window).width() <= 900) {
                $('#chatSidebar').removeClass('open');
                $('#sidebarBackdrop').removeClass('active');
            }
        });

        function updateOnlineCount() {
            $.get('/ChatApi/GetOnlineCount', { roomId: rId }, function (data) {
                if (data && data.success && typeof data.count !== 'undefined') {
                    $('#onlineCount').text(data.count);
                }
            });
        }
        updateOnlineCount();
        setInterval(updateOnlineCount, 10000);

        $('#closeModal').click(() => $('#imageModal').hide());
        $('#imageModal').click(e => { if ($(e.target).is('#imageModal')) $('#imageModal').hide(); });

        // ── FILE ATTACHMENTS & DRAG-AND-DROP LOGIC ──
        let stagedFiles = [];

        window.formatFileSize = function(bytes) {
            if (bytes < 1024) return bytes + ' B';
            if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
            return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
        };

        window.getFileIconInfo = function(ext) {
            ext = (ext || '').toLowerCase();
            if (ext === '.pdf') return { icon: 'fa-file-pdf', cssClass: 'file-icon-pdf' };
            if (ext === '.doc' || ext === '.docx') return { icon: 'fa-file-word', cssClass: 'file-icon-word' };
            if (ext === '.xls' || ext === '.xlsx' || ext === '.csv') return { icon: 'fa-file-excel', cssClass: 'file-icon-excel' };
            if (ext === '.ppt' || ext === '.pptx') return { icon: 'fa-file-powerpoint', cssClass: 'file-icon-powerpoint' };
            if (ext === '.zip' || ext === '.rar' || ext === '.7z') return { icon: 'fa-file-zipper', cssClass: 'file-icon-zip' };
            if (ext === '.txt') return { icon: 'fa-file-lines', cssClass: 'file-icon-txt' };
            return { icon: 'fa-file', cssClass: 'file-icon-default' };
        };

        window.handleFilesSelected = function(fileList) {
            if (!fileList || fileList.length === 0) return;
            const files = Array.from(fileList);

            if (stagedFiles.length + files.length > 5) {
                Swal.fire({
                    icon: 'warning',
                    title: 'Giới hạn số lượng tệp',
                    text: `Mỗi tin nhắn chỉ được đính kèm tối đa 5 tệp (hiện bạn đã chọn ${stagedFiles.length} tệp).`,
                    toast: true,
                    position: 'top-end',
                    showConfirmButton: false,
                    timer: 4000
                });
                return;
            }

            const dangerousExts = ['.exe', '.bat', '.cmd', '.sh', '.dll', '.com', '.vbs', '.ps1', '.msi', '.php', '.asp', '.aspx', '.js', '.vbe', '.wsf', '.wsh', '.scr', '.pif', '.jar', '.bin', '.app', '.cgi'];
            const allowedImageExts = ['.jpg', '.jpeg', '.png', '.gif', '.webp'];
            const allowedDocExts = ['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx', '.txt', '.csv', '.zip', '.rar', '.7z'];

            let currentTotalSize = stagedFiles.reduce((acc, f) => acc + f.size, 0);

            for (let i = 0; i < files.length; i++) {
                const file = files[i];
                const ext = '.' + file.name.split('.').pop().toLowerCase();

                if (dangerousExts.includes(ext)) {
                    Swal.fire({
                        icon: 'error',
                        title: 'Tệp không an toàn',
                        text: `Tệp "${file.name}" bị chặn vì lý do an toàn bảo mật hệ thống.`,
                        background: '#0d1626',
                        color: '#fff'
                    });
                    continue;
                }

                const isImage = allowedImageExts.includes(ext);
                const isDoc = allowedDocExts.includes(ext);

                if (!isImage && !isDoc) {
                    Swal.fire({
                        icon: 'warning',
                        title: 'Định dạng không hỗ trợ',
                        text: `Tệp "${file.name}" không thuộc danh sách ảnh hoặc tài liệu được hỗ trợ.`,
                        background: '#0d1626',
                        color: '#fff'
                    });
                    continue;
                }

                if (file.size > 10 * 1024 * 1024) {
                    Swal.fire({
                        icon: 'warning',
                        title: 'Tệp quá lớn',
                        text: `Tệp "${file.name}" vượt quá dung lượng tối đa 10MB cho phép.`,
                        background: '#0d1626',
                        color: '#fff'
                    });
                    continue;
                }

                if (currentTotalSize + file.size > 25 * 1024 * 1024) {
                    Swal.fire({
                        icon: 'warning',
                        title: 'Vượt giới hạn dung lượng',
                        text: `Tổng dung lượng các tệp đính kèm không được vượt quá 25MB.`,
                        background: '#0d1626',
                        color: '#fff'
                    });
                    break;
                }

                currentTotalSize += file.size;
                const id = 'staged_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
                let previewUrl = null;
                if (isImage) {
                    previewUrl = URL.createObjectURL(file);
                }

                stagedFiles.push({
                    id: id,
                    file: file,
                    name: file.name,
                    size: file.size,
                    formattedSize: window.formatFileSize(file.size),
                    isImage: isImage,
                    ext: ext,
                    previewUrl: previewUrl
                });
            }

            renderStagedFiles();
        };

        window.removeStagedFile = function(id) {
            const idx = stagedFiles.findIndex(f => f.id === id);
            if (idx !== -1) {
                if (stagedFiles[idx].previewUrl) {
                    URL.revokeObjectURL(stagedFiles[idx].previewUrl);
                }
                stagedFiles.splice(idx, 1);
                renderStagedFiles();
            }
        };

        window.clearStagedFiles = function() {
            stagedFiles.forEach(f => {
                if (f.previewUrl) URL.revokeObjectURL(f.previewUrl);
            });
            stagedFiles = [];
            renderStagedFiles();
            $('#fileInput').val('');
            $('#docFileInput').val('');
        };

        function renderStagedFiles() {
            const tray = $('#attachmentStagingArea');
            if (stagedFiles.length === 0) {
                tray.hide().empty();
                return;
            }

            let chipsHtml = '';
            stagedFiles.forEach(f => {
                let visual = '';
                if (f.isImage && f.previewUrl) {
                    visual = `<img src="${f.previewUrl}" alt="${f.name}" />`;
                } else {
                    const iconInfo = window.getFileIconInfo(f.ext);
                    visual = `<div class="staged-icon ${iconInfo.cssClass}"><i class="fa-solid ${iconInfo.icon}"></i></div>`;
                }
                chipsHtml += `
                    <div class="staged-chip" id="${f.id}">
                        ${visual}
                        <div class="staged-info">
                            <span class="staged-name" title="${f.name}">${f.name}</span>
                            <span class="staged-size">${f.formattedSize}</span>
                        </div>
                        <i class="fa-solid fa-xmark staged-remove" onclick="removeStagedFile('${f.id}')" title="Xóa tệp này"></i>
                    </div>
                `;
            });

            tray.html(chipsHtml).css('display', 'flex');
        }

        $('#fileInput').on('change', function () {
            if (this.files && this.files.length > 0) {
                handleFilesSelected(this.files);
            }
        });

        $('#docFileInput').on('change', function () {
            if (this.files && this.files.length > 0) {
                handleFilesSelected(this.files);
            }
        });

        // Ngăn chặn trình duyệt mở tệp khi kéo thả vào trang
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            window.addEventListener(eventName, function (e) {
                e.preventDefault();
                e.stopPropagation();
            }, false);
        });

        let dragCounter = 0;
        const messengerEl = document.getElementById('messengerContainer');
        if (messengerEl) {
            messengerEl.addEventListener('dragenter', function(e) {
                e.preventDefault();
                e.stopPropagation();
                dragCounter++;
                $('#chatDropZoneOverlay').css('display', 'flex');
            }, false);

            messengerEl.addEventListener('dragover', function(e) {
                e.preventDefault();
                e.stopPropagation();
                $('#chatDropZoneOverlay').css('display', 'flex');
            }, false);

            messengerEl.addEventListener('dragleave', function(e) {
                e.preventDefault();
                e.stopPropagation();
                dragCounter--;
                if (dragCounter <= 0) {
                    dragCounter = 0;
                    $('#chatDropZoneOverlay').hide();
                }
            }, false);

            messengerEl.addEventListener('drop', function(e) {
                e.preventDefault();
                e.stopPropagation();
                dragCounter = 0;
                $('#chatDropZoneOverlay').hide();
                if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                    handleFilesSelected(e.dataTransfer.files);
                }
            }, false);
        }

        $('#txtMessage').off('keydown').on('keydown', function(e) {
            if (e.which == 13 && !e.shiftKey) { e.preventDefault(); sendMessage(); }
        });

        // ── SIGNALR HANDLERS ──
        chat.client.addNewMessageToPage = function(senderId, message, imageUrl, messageId, replyToId, replyContent, isAnonymous, senderName, senderAvatar) {
            if (messageId && $(`#msg-${messageId}`).length > 0) return;
            if (senderId === 0 && !isAnonymous) {
                $('#chatBox').append(`<div class="sys-msg"><i>${message}</i></div>`);
                chatBox.scrollTop = chatBox.scrollHeight;
                updateOnlineCount();
                debouncedReloadChatList(800);
                return;
            }
            const isMe = (senderId == mId);
            const msgId = messageId || 0;
            const safeMsg = $('<div/>').text(message || "").html();
            const safeReply = $('<div/>').text(replyContent || "").html();
            const replyHtml = (replyToId && replyContent)
                ? `<div class="reply-preview-msg" onclick="scrollToMsg(${replyToId})">↩ ${safeReply.length > 40 ? safeReply.substring(0,40)+'...' : safeReply}</div>` : '';

            let contentHtml = '';
            let rawForReply = safeMsg;

            if (message && message.startsWith("[ATTACHMENTS_PAYLOAD]:")) {
                try {
                    const payloadJson = message.substring("[ATTACHMENTS_PAYLOAD]:".length);
                    const payload = JSON.parse(payloadJson);
                    const payloadText = payload.Text || "";
                    const items = payload.Items || [];
                    rawForReply = payloadText || (items.length > 0 ? "[Tệp đính kèm]" : "");

                    const imageItems = items.filter(i => i.IsImage);
                    const docItems = items.filter(i => !i.IsImage);

                    let imgHtml = '';
                    if (imageItems.length === 1) {
                        imgHtml = `<div class="msg msg-image"><img src="${imageItems[0].Url}" class="chat-img" onclick="viewImage('${imageItems[0].Url}')"/></div>`;
                    } else if (imageItems.length > 1) {
                        let gridInner = imageItems.map(im => `<img src="${im.Url}" class="chat-grid-img" onclick="viewImage('${im.Url}')"/>`).join('');
                        imgHtml = `<div class="msg-images-grid">${gridInner}</div>`;
                    }

                    let textPartHtml = payloadText ? `<div class="msg msg-text">${$('<div/>').text(payloadText).html()}</div>` : '';

                    let docHtml = '';
                    if (docItems.length > 0) {
                        let cards = docItems.map(d => {
                            const iconInfo = window.getFileIconInfo(d.Extension);
                            const safeName = $('<div/>').text(d.FileName).html();
                            const dlUrl = `/Chat/DownloadAttachment?messageId=${msgId}&fileKey=${encodeURIComponent(d.FileKey || '')}`;
                            const extUpper = (d.Extension || '').replace('.', '').toUpperCase();
                            return `
                                <div class="msg-file-card" onclick="window.location.href='${dlUrl}'">
                                    <div class="file-icon-box ${iconInfo.cssClass}">
                                        <i class="fa-solid ${iconInfo.icon}"></i>
                                    </div>
                                    <div class="file-details">
                                        <div class="file-name" title="${safeName}">${safeName}</div>
                                        <div class="file-sub">
                                            <span>${d.FormattedSize || ''}</span>
                                            <span>•</span>
                                            <span class="file-ext-badge">${extUpper}</span>
                                        </div>
                                    </div>
                                    <a href="${dlUrl}" class="file-dl-btn" title="Tải xuống tệp" onclick="event.stopPropagation();">
                                        <i class="fa-solid fa-cloud-arrow-down"></i>
                                    </a>
                                </div>
                            `;
                        }).join('');
                        docHtml = `<div class="msg-files-list">${cards}</div>`;
                    }

                    contentHtml = imgHtml + textPartHtml + docHtml;
                } catch(e) {
                    contentHtml = `<div class="msg msg-text">${safeMsg}</div>`;
                }
            } else {
                const hasImage = imageUrl && imageUrl !== "null" && imageUrl !== "";
                const imageHtml = hasImage
                    ? `<div class="msg msg-image"><img src="${imageUrl}" class="chat-img" onclick="viewImage('${imageUrl}')"/></div>` : '';
                const textHtml = safeMsg
                    ? `<div class="msg msg-text">${safeMsg}</div>` : '';
                contentHtml = imageHtml + textHtml;
            }

            const isAnon = (isAnonymous !== false && isAnonymous !== "false");
            const displayName = isAnon ? "Ẩn danh" : (senderName || "Sinh viên");
            const safeDisplayName = $('<div/>').text(displayName).html();
            const avatarImg = (!isAnon && senderAvatar && senderAvatar !== "null" && senderAvatar !== "") ? senderAvatar : "";

            let senderNameHtml = '';
            if (!isMe) {
                if (!isAnon) {
                    senderNameHtml = `
                        <div class="msg-sender-name revealed">
                            <span class="name-text">${safeDisplayName}</span>
                            <span class="badge-sv">SV TDMU</span>
                        </div>`;
                } else {
                    senderNameHtml = `
                        <div class="msg-sender-name anon">
                            <span class="badge-anon"><i class="fa-solid fa-user-secret me-1"></i>Ẩn danh</span>
                        </div>`;
                }
            }

            let avatarInner = '';
            if (!isAnon && avatarImg) {
                avatarInner = `<img src="${avatarImg}" class="avatar-img" alt="${safeDisplayName}" onerror="handleAvatarError(this)" />`;
            } else if (!isAnon) {
                const initial = displayName.length > 0 ? displayName.charAt(0).toUpperCase() : 'U';
                avatarInner = `<span class="avatar-initials">${initial}</span>`;
            } else {
                avatarInner = `<i class="fa-solid fa-user-secret"></i>`;
            }

            const avatarHtml = !isMe
                ? `<div class="avatar-wrapper" onclick="showUserMenu(event, ${senderId}, '${safeDisplayName.replace(/'/g, "\\'")}', '${avatarImg.replace(/'/g, "\\'")}', ${isAnon})"><div class="avatar ${isAnon ? 'anon' : 'revealed'}" title="${isAnon ? 'Người lạ ẩn danh' : safeDisplayName}">${avatarInner}</div></div>` : '';

            const actionsHtml = msgId > 0 ? `
                <div class="msg-actions" style="position:relative;">
                    <button class="msg-option-btn" onclick="toggleMsgMenu(event, ${msgId})"><i class="fa-solid fa-ellipsis-vertical"></i></button>
                    <div class="msg-dropdown" id="msgMenu-${msgId}">
                        <a href="javascript:void(0)" onclick="setReply(${msgId}, '${rawForReply.replace(/'/g,"\\'")}')"><i class="fa-solid fa-reply"></i> Trả lời</a>
                        ${isMe ? `
                        <a href="javascript:void(0)" onclick="revokeMsg(${msgId})" style="color:rgba(255,160,0,0.8);"><i class="fa-solid fa-rotate-left"></i> Thu hồi</a>
                        <a href="javascript:void(0)" onclick="deleteMsg(${msgId})" style="color:rgba(255,80,80,0.8);"><i class="fa-solid fa-trash"></i> Xóa</a>` : `
                        <a href="javascript:void(0)" onclick="showReportModal(${rId}, ${isAnon ? 0 : senderId}, ${msgId})" style="color:rgba(255,100,100,0.9);"><i class="fa-solid fa-flag"></i> Tố cáo</a>`}
                    </div>
                </div>` : '';

            const modeBadge = isAnon
                ? `<span class="ts-mode anon" title="Tin nhắn gửi ẩn danh"><i class="fa-solid fa-mask"></i></span>`
                : `<span class="ts-mode public" title="Tin nhắn gửi công khai"><i class="fa-solid fa-user-check"></i></span>`;

            $('#chatBox').append(`
                <div class="msg-wrapper ${isMe ? 'me' : 'other'}" ${msgId > 0 ? `id="msg-${msgId}"` : ''}>
                    ${senderNameHtml}
                    <div class="msg-row">
                        ${avatarHtml}
                        <div class="msg-container">
                            <div class="msg-content">${replyHtml}${contentHtml}<div class="reaction-badge"></div></div>
                            ${actionsHtml}
                        </div>
                    </div>
                    <div class="timestamp">${getCurrentTime()} ${modeBadge}</div>
                </div>`);
            chatBox.scrollTop = chatBox.scrollHeight;
            debouncedReloadChatList(800);
        };

        chat.client.onMessageRevoked = function(messageId) {
            $(`#msg-${messageId}`).find('.msg-text, .msg-image, .msg-images-grid, .msg-files-list').replaceWith(
                `<div class="msg msg-text revoked-style"><i class="fa-solid fa-circle-info" style="font-size:11px;opacity:0.5;margin-right:4px;"></i>Tin nhắn đã được thu hồi</div>`
            );
            $(`#msg-${messageId}`).find('.msg-actions').remove();
        };
        chat.client.onMessageDeleted = function(messageId) {
            $(`#msg-${messageId}`).fadeOut(300, function() { $(this).remove(); });
        };
        chat.client.onRoomRenamed = function(targetRoomId, newName) {
            if (targetRoomId === roomId) {
                $('#roomTitleText').text(newName);
            }
            $(`#sidebar-room-name-${targetRoomId}`).text(newName);
            loadSidebarChatList();
        };
        chat.client.triggerReloadChatList = function(targetRoomId) {
            debouncedReloadChatList(800);
        };
        chat.client.receiveChatInvitation = function(fromId, fromName, targetRoomId, forUserId) {
            if (forUserId == mId && fromId != mId) {
                Swal.fire({
                    title: 'Lời mời vào nhóm/chat riêng!',
                    text: `${fromName} đã thêm hoặc mời bạn vào cuộc trò chuyện.`,
                    icon: 'info', showCancelButton: true,
                    confirmButtonText: 'Vào ngay', cancelButtonText: 'Để sau',
                    toast: true, position: 'top-end', timer: 15000, timerProgressBar: true
                }).then(r => { 
                    loadSidebarChatList();
                    if (r.isConfirmed) window.location.href = '/Chat/Private/' + targetRoomId; 
                });
            }
        };

        chat.client.updateRoomOnlineCount = function(targetRoomId, count) {
            if (parseInt(targetRoomId) === parseInt(rId)) {
                if (typeof count !== 'undefined' && count !== null) {
                    $('#onlineCount').text(count);
                }
            }
        };

        chat.client.onUnauthorized = function(msg) {
            Swal.fire({
                title: 'Phiên làm việc hết hạn',
                text: msg || 'Vui lòng đăng nhập lại để tiếp tục trò chuyện.',
                icon: 'warning',
                confirmButtonText: 'Đăng nhập ngay',
                allowOutsideClick: false
            }).then(() => {
                window.location.href = '/User/Login?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search);
            });
        };

        chat.client.onForbidden = function(msg) {
            Swal.fire({
                title: 'Không có quyền truy cập',
                text: msg || 'Bạn không phải thành viên của cuộc trò chuyện này.',
                icon: 'error',
                confirmButtonText: 'Về sảnh chat',
                allowOutsideClick: false
            }).then(() => {
                window.location.href = '/Chat';
            });
        };

        $(document).ajaxError(function (event, jqXHR, ajaxSettings, thrownError) {
            if (jqXHR.status === 401) {
                var res = jqXHR.responseJSON;
                var redirectUrl = (res && res.redirectUrl) ? res.redirectUrl : ('/User/Login?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search));
                Swal.fire({
                    title: 'Chưa đăng nhập hoặc hết phiên',
                    text: (res && res.message) ? res.message : 'Vui lòng đăng nhập để tiếp tục thao tác.',
                    icon: 'warning',
                    confirmButtonText: 'Đăng nhập',
                    allowOutsideClick: false
                }).then(() => {
                    window.location.href = redirectUrl;
                });
            } else if (jqXHR.status === 403) {
                var res = jqXHR.responseJSON;
                Swal.fire({
                    title: 'Bị từ chối truy cập',
                    text: (res && res.message) ? res.message : 'Bạn không có quyền thực hiện thao tác này.',
                    icon: 'error',
                    confirmButtonText: 'Đồng ý'
                });
            }
        });

        function enableMessageSending() {
            $('#btnSend').off('click').on('click', sendMessage);
        }

        enableMessageSending();

        // Khởi động kết nối SignalR với cơ chế đa tầng (WebSockets -> SSE -> Long Polling)
        $.connection.hub.start({ transport: ['webSockets', 'serverSentEvents', 'longPolling'] }).done(function() {
            chat.server.joinRoom(rId, mId);
        }).fail(function() {
            console.warn('Không thể kết nối chat thời gian thực qua SignalR.');
        });

        0 (ViewBag.WaitingForPair == null ? false : (bool)ViewBag.WaitingForPair)
        {
            
            var pairInterval = setInterval(function () {
                $.get('/Chat/CheckRoomReady/1', function (res) {
                    if (res.ready) {
                        clearInterval(pairInterval);
                        location.reload();
                    }
                });
            }, 3000);
            
        }

        let _isSendingMessage = false;

        function sendMessage() {
            if (_isSendingMessage) return;
            const msg = $('#txtMessage').val().trim();
            if (!msg && stagedFiles.length === 0) return;

            if (stagedFiles.length > 0) {
                const formData = new FormData();
                formData.append("roomId", rId);
                formData.append("content", msg);
                if (replyingToId) formData.append("replyToId", replyingToId);
                formData.append("isAnonymous", isAnonymousMode);
                formData.append("__RequestVerificationToken", getChatAntiForgeryToken());

                for (let i = 0; i < stagedFiles.length; i++) {
                    formData.append("files", stagedFiles[i].file);
                }

                const originalBtnHtml = $('#btnSend').html();
                $('#btnSend').html('<i class="fa-solid fa-spinner fa-spin"></i>').css('pointer-events', 'none');
                $('#txtMessage').prop('disabled', true);

                $.ajax({
                    url: '/Chat/SendMessageWithFiles',
                    type: 'POST',
                    data: formData,
                    processData: false,
                    contentType: false,
                    success: function(res) {
                        $('#btnSend').html(originalBtnHtml).css('pointer-events', '');
                        _isSendingMessage = false;
                        $('#txtMessage').prop('disabled', false).val('').focus();
                        if (res.success) {
                            clearStagedFiles();
                            cancelReply();
                            if (res.censored) {
                                Swal.fire({
                                    icon: 'warning',
                                    title: 'Nhắc nhở ngôn từ',
                                    text: 'Tin nhắn đã được tự động kiểm duyệt các từ ngữ nhạy cảm.',
                                    toast: true,
                                    position: 'top-end',
                                    showConfirmButton: false,
                                    timer: 4000
                                });
                            }
                        } else {
                            Swal.fire({ icon: 'error', title: 'Lỗi', text: res.message || 'Không thể gửi tệp đính kèm.', background: '#0d1626', color: '#fff' });
                        }
                    },
                    error: function(xhr) {
                        $('#btnSend').html(originalBtnHtml).css('pointer-events', '');
                        _isSendingMessage = false;
                        $('#txtMessage').prop('disabled', false).focus();
                        let errMsg = 'Không thể gửi tệp đính kèm do lỗi máy chủ hoặc vượt quá kích thước cho phép.';
                        try {
                            const j = JSON.parse(xhr.responseText);
                            if (j && j.message) errMsg = j.message;
                        } catch(e) {}
                        Swal.fire({ icon: 'error', title: 'Lỗi gửi tin', text: errMsg, background: '#0d1626', color: '#fff' });
                    }
                });
                return;
            }

            // Gửi tin nhắn văn bản thông thường
            _isSendingMessage = true;
            const originalSendBtnHtml = $('#btnSend').html();
            $('#btnSend').html('<i class="fa-solid fa-spinner fa-spin"></i>').css('pointer-events', 'none');
            $('#txtMessage').val('').focus();

            $.post('/Chat/SendMessage', { 
                roomId: rId, 
                content: msg, 
                replyToId: replyingToId || null,
                isAnonymous: isAnonymousMode
            }, function(res) {
                if (res.success) {
                    cancelReply();
                    if (res.censored) {
                        Swal.fire({
                            icon: 'warning',
                            title: 'Nhắc nhở ngôn từ',
                            text: 'Tin nhắn đã được tự động kiểm duyệt các từ ngữ nhạy cảm.',
                            toast: true,
                            position: 'top-end',
                            showConfirmButton: false,
                            timer: 4000
                        });
                    }
                } else {
                    $('#txtMessage').val(msg);
                    if (res.isRateLimited) {
                        Swal.fire({ icon:'warning', title:'Gửi quá nhanh', text: res.message||'', toast:true, position:'top-end', showConfirmButton:false, timer:3500 });
                    } else {
                        Swal.fire({ icon:'error', title:'Lỗi', text: res.message||'', toast:true, position:'top-end', showConfirmButton:false, timer:3000 });
                    }
                }
            }).always(function() {
                _isSendingMessage = false;
                $('#btnSend').html(originalSendBtnHtml).css('pointer-events', '');
            });
        }

        $('#emojiBtn').click(function(e) {
            e.stopPropagation();
            const p = $('#emojiPicker');
            p.css('display') === 'grid' ? p.hide() : p.css('display','grid');
        });
        $(document).on('click', '.emoji-item', function(e) {
            e.stopPropagation();
            input.val(input.val() + $(this).text()).focus();
        });
        $('#userContextMenu').click(function(e) {
            e.stopPropagation();
        });
        $(document).click(function() {
            $('#emojiPicker, .msg-dropdown').hide();
            $('.msg-actions').removeClass('is-active');
            closeUserMenu();
        });
        $('#chatBox').on('scroll', function() {
            $('.msg-dropdown').hide();
            $('.msg-actions').removeClass('is-active');
            if (chatBox.scrollTop <= 40 && hasMoreHistory && !isLoadingHistory) {
                loadEarlierMessages();
            }
        });
        $(window).on('resize', function() {
            $('.msg-dropdown').hide();
            $('.msg-actions').removeClass('is-active');
        });
        $(document).on('keydown', function(e) {
            if (e.key === 'Escape' || e.keyCode === 27) {
                $('.msg-dropdown').hide();
                $('.msg-actions').removeClass('is-active');
                closeUserMenu();
            }
        });
        setTimeout(() => { chatBox.scrollTop = chatBox.scrollHeight; }, 100);
    });
    