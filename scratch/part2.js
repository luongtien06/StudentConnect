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

    if (false) {
        {
//             <text>
            var pairInterval = setInterval(function () {
                $.get('/Chat/CheckRoomReady/1', function (res) {
                    if (res.ready) {
                        clearInterval(pairInterval);
                        location.reload();
                    }
                });
            }, 3000);
//             </text>
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