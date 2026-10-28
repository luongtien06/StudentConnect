import os

path = r"Areas\Admin\Views\AdminChat\Index.cshtml"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Add Pagination CSS
css_target = ".select-bar.show { display: flex; }"
pagination_css = """
    .select-bar.show { display: flex; }

    /* Custom Modern Pagination */
    .pagination-wrap {
        display: inline-flex;
        flex-wrap: wrap;
        align-items: center;
        gap: 5px;
    }
    .page-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        min-width: 32px;
        height: 32px;
        padding: 0 10px;
        border-radius: 8px;
        font-size: 11.5px;
        font-weight: 500;
        font-family: 'Space Grotesk', sans-serif;
        color: rgba(255, 255, 255, 0.85);
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid rgba(255, 255, 255, 0.15);
        cursor: pointer;
        transition: all 0.15s ease;
        user-select: none;
    }
    .page-btn:hover:not(:disabled):not(.active) {
        background: rgba(0, 200, 255, 0.15);
        border-color: rgba(0, 200, 255, 0.5);
        color: #00c8ff;
        transform: translateY(-1px);
    }
    .page-btn.active {
        background: rgba(0, 200, 255, 0.25);
        border-color: #00c8ff;
        color: #fff;
        font-weight: 700;
        box-shadow: 0 0 10px rgba(0, 200, 255, 0.35);
    }
    .page-btn:disabled {
        opacity: 0.3;
        cursor: not-allowed;
        border-color: rgba(255, 255, 255, 0.07);
        background: transparent;
        color: rgba(255, 255, 255, 0.4);
    }
    .page-ellipsis {
        padding: 0 5px;
        color: rgba(255, 255, 255, 0.4);
        font-size: 13px;
        align-self: center;
    }"""

if css_target in content:
    content = content.replace(css_target, pagination_css, 1)
    print("1. Injected pagination CSS")
else:
    print("WARNING: css_target not found")

# 2. Add Message Selection Bar above table in #tab-messages
table_target = '<div class="table-responsive">\r\n            <table class="admin-table">\r\n                <thead>\r\n                    <tr>\r\n                        <th>ID</th>'
if table_target not in content:
    table_target = '<div class="table-responsive">\n            <table class="admin-table">\n                <thead>\n                    <tr>\n                        <th>ID</th>'

replacement_table = """<!-- Message Selection Bar -->
        <div class="select-bar" id="msgSelectBar">
            <i class="bi bi-check2-square text-info"></i>
            <span>Đã chọn <strong id="msgSelectCount" class="text-white">0</strong> tin nhắn trên trang hiện tại</span>
            <button type="button" class="btn-adm danger" style="padding:4px 12px; font-size:11px;" id="btnDeleteSelectedMessages" disabled>
                <i class="bi bi-trash3"></i> Xóa tin nhắn đã chọn
            </button>
            <button type="button" class="btn-adm cyan" style="padding:4px 12px; font-size:11px;" id="btnClearSelectMessages">
                <i class="bi bi-x-circle"></i> Bỏ chọn
            </button>
        </div>

        <div class="table-responsive">
            <table class="admin-table">
                <thead>
                    <tr>
                        <th style="width:38px; text-align:center;">
                            <input type="checkbox" id="msgSelectAll" title="Chọn tất cả tin nhắn trên trang hiện tại" style="cursor:pointer;" />
                        </th>
                        <th>ID</th>"""

if table_target in content:
    content = content.replace(table_target, replacement_table, 1)
    print("2. Injected msgSelectBar and checkbox header")
else:
    print("WARNING: table_target not found")

# 3. Add checkbox cell to Razor foreach in #tab-messages
row_target = '<tr id="msg-row-@m.MessageID">\r\n                            <td><span class="cell-id">#@m.MessageID</span></td>'
if row_target not in content:
    row_target = '<tr id="msg-row-@m.MessageID">\n                            <td><span class="cell-id">#@m.MessageID</span></td>'

replacement_row = """<tr id="msg-row-@m.MessageID">
                            <td style="text-align:center;">
                                <input type="checkbox" class="msg-checkbox" value="@m.MessageID" style="cursor:pointer;" />
                            </td>
                            <td><span class="cell-id">#@m.MessageID</span></td>"""

if row_target in content:
    content = content.replace(row_target, replacement_row, 1)
    print("3. Injected checkbox cell in Razor loop")
else:
    print("WARNING: row_target not found")

# 4. Enhance pagination footer container
footer_target = '<div id="messagesPagination" class="d-flex justify-content-between align-items-center p-3" style="border-top:1px solid var(--chat-card-border); font-size:12px; color:rgba(255,255,255,0.5);">'
replacement_footer = '<div id="messagesPagination" class="d-flex flex-wrap justify-content-between align-items-center p-3 gap-2" style="border-top:1px solid var(--chat-card-border); font-size:12px; color:rgba(255,255,255,0.7);">'

if footer_target in content:
    content = content.replace(footer_target, replacement_footer, 1)
    print("4. Enhanced messagesPagination container")
else:
    print("WARNING: footer_target not found")

# 5. Enhance switchMainTab to auto load tab-messages
tab_target = """    function switchMainTab(tabId, btn) {
        $('.chat-tab-pane').hide();
        $('#' + tabId).show();
        $('.chat-nav-btn').removeClass('active');
        if (btn) $(btn).addClass('active');
        else $(`.chat-nav-btn[data-tab="${tabId}"]`).addClass('active');
    }"""
if tab_target not in content:
    tab_target = tab_target.replace("\n", "\r\n")

replacement_tab = """    var currentMsgPage = 1;

    function switchMainTab(tabId, btn) {
        $('.chat-tab-pane').hide();
        $('#' + tabId).show();
        $('.chat-nav-btn').removeClass('active');
        if (btn) $(btn).addClass('active');
        else $(`.chat-nav-btn[data-tab="${tabId}"]`).addClass('active');

        // Tự động tải dữ liệu có phân trang khi người dùng chuyển sang tab Quản lý tin nhắn
        if (tabId === 'tab-messages') {
            loadMessagesPage(currentMsgPage || 1);
        }
    }"""

if tab_target in content:
    content = content.replace(tab_target, replacement_tab, 1)
    print("5. Enhanced switchMainTab")
else:
    print("WARNING: tab_target not found")

# 6. Replace loadMessagesPage and inject pagination helper + bulk delete JS
old_load_msg_start = "// 2. QUẢN LÝ TIN NHẮN (MESSAGE MANAGEMENT)"
old_load_msg_end = "    function openMessageDetailModal(id) {"

pos_start = content.find(old_load_msg_start)
pos_end = content.find(old_load_msg_end)

if pos_start != -1 and pos_end != -1:
    new_msg_js = """// 2. QUẢN LÝ TIN NHẮN (MESSAGE MANAGEMENT)
    // ════════════════════════════════════════════════════════════

    function renderPaginationComponent(containerId, infoId, page, totalPages, totalCount, pageSize, callbackFnName, itemLabel) {
        var $info = $('#' + infoId);
        var $btns = $('#' + containerId);

        if (!totalCount || totalCount === 0) {
            $info.html(`Không có ${itemLabel} nào`);
            $btns.html('');
            return;
        }

        var start = (page - 1) * pageSize + 1;
        var end = Math.min(page * pageSize, totalCount);
        $info.html(`Hiển thị <strong class="text-white">${start} - ${end}</strong> / <strong class="text-white">${totalCount}</strong> ${itemLabel} (Trang <strong>${page}</strong>/<strong>${totalPages}</strong>)`);

        var html = '<div class="pagination-wrap">';

        // Nút Trang đầu & Trang trước
        var prevDisabled = (page <= 1) ? 'disabled' : '';
        html += `<button type="button" class="page-btn" ${prevDisabled} onclick="${callbackFnName}(1)" title="Trang đầu">&laquo;&laquo;</button>`;
        html += `<button type="button" class="page-btn" ${prevDisabled} onclick="${callbackFnName}(${page - 1})" title="Trang trước">&lsaquo; Trước</button>`;

        // Cửa sổ số trang
        var startPage = Math.max(1, page - 2);
        var endPage = Math.min(totalPages, page + 2);

        if (startPage > 1) {
            html += `<button type="button" class="page-btn" onclick="${callbackFnName}(1)">1</button>`;
            if (startPage > 2) {
                html += `<span class="page-ellipsis">...</span>`;
            }
        }

        for (var p = startPage; p <= endPage; p++) {
            var activeClass = (p === page) ? 'active' : '';
            html += `<button type="button" class="page-btn ${activeClass}" onclick="${callbackFnName}(${p})">${p}</button>`;
        }

        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                html += `<span class="page-ellipsis">...</span>`;
            }
            html += `<button type="button" class="page-btn" onclick="${callbackFnName}(${totalPages})">${totalPages}</button>`;
        }

        // Nút Trang sau & Trang cuối
        var nextDisabled = (page >= totalPages) ? 'disabled' : '';
        html += `<button type="button" class="page-btn" ${nextDisabled} onclick="${callbackFnName}(${page + 1})" title="Trang sau">Sau &rsaquo;</button>`;
        html += `<button type="button" class="page-btn" ${nextDisabled} onclick="${callbackFnName}(${totalPages})" title="Trang cuối">&raquo;&raquo;</button>`;

        html += '</div>';
        $btns.html(html);
    }

    function loadMessagesPage(page) {
        currentMsgPage = page || 1;
        var kw = $('#msgFilterKeyword').val();
        var snd = $('#msgFilterSender').val();
        var rId = $('#msgFilterRoomId').val();
        var type = $('#msgFilterType').val();
        var status = $('#msgFilterStatus').val();

        $('#messagesTableBody').html('<tr><td colspan="8" class="text-center py-4 text-white-50"><i class="bi bi-arrow-repeat spin"></i> Đang tải dữ liệu...</td></tr>');

        $.get('/Admin/AdminChat/FilterMessages', {
            keyword: kw, sender: snd, roomId: rId, msgType: type, status: status, page: currentMsgPage, pageSize: 30
        }, function (res) {
            if (res.success) {
                var html = '';
                if (!res.messages || res.messages.length === 0) {
                    html = '<tr><td colspan="8" class="text-center py-4 text-white-50">Không tìm thấy tin nhắn phù hợp.</td></tr>';
                } else {
                    res.messages.forEach(function (m) {
                        html += `<tr id="msg-row-${m.MessageID}">
                            <td style="text-align:center;">
                                <input type="checkbox" class="msg-checkbox" value="${m.MessageID}" style="cursor:pointer;" />
                            </td>
                            <td><span class="cell-id">#${m.MessageID}</span></td>
                            <td><span class="cell-date">${m.SentAtFormatted}</span></td>
                            <td>
                                <div class="d-flex align-items-center gap-2">
                                    <img src="${m.SenderAvatar}" style="width:26px; height:26px; border-radius:50%; object-fit:cover;" onerror="this.onerror=null;this.src=window.DEFAULT_AVATAR_SVG||'/Content/Images/default-avatar.png';" />
                                    <div>
                                        <div class="fw-bold text-white" style="font-size:12px;">${m.SenderDisplayName}</div>
                                        <div style="font-size:10px; color:rgba(255,255,255,0.4);">${m.IsAnonymousMsg ? '[Ẩn danh]' : '@@' + m.SenderUsername}</div>
                                    </div>
                                </div>
                            </td>
                            <td><span class="adm-badge badge-dim">#${m.RoomID} - ${m.RoomName}</span></td>
                            <td>
                                <div style="max-width:280px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;">
                                    ${m.HasAttachments ? `<span class="adm-badge badge-purple me-1" style="font-size:9.5px;"><i class="bi bi-paperclip"></i> ${m.AttachmentsCount} tệp</span>` : ''}
                                    ${m.ImageUrl ? `<span class="adm-badge badge-cyan me-1" style="font-size:9.5px;"><i class="bi bi-image"></i> Ảnh</span>` : ''}
                                    <span class="text-white-50">${m.DisplayText}</span>
                                </div>
                            </td>
                            <td>
                                ${m.IsHidden ? `<span class="adm-badge badge-danger" title="Lý do: ${m.HiddenReason || ''}"><i class="bi bi-eye-slash"></i> Đã ẩn</span>` : `<span class="adm-badge badge-green"><i class="bi bi-check-circle"></i> Hiển thị</span>`}
                            </td>
                            <td style="text-align:center;">
                                <div style="display:inline-flex; gap:5px; align-items:center;">
                                    <button class="btn-adm cyan icon-only" title="Xem chi tiết" onclick="openMessageDetailModal(${m.MessageID})">
                                        <i class="bi bi-eye"></i>
                                    </button>
                                    ${m.IsHidden ? `<button class="btn-adm green icon-only" title="Bỏ ẩn" onclick="unhideMessage(${m.MessageID})"><i class="bi bi-eye-fill"></i></button>` : `<button class="btn-adm amber icon-only" title="Ẩn tin" onclick="promptHideMessage(${m.MessageID})"><i class="bi bi-eye-slash"></i></button>`}
                                    <button class="btn-adm danger icon-only" title="Xóa vĩnh viễn" onclick="confirmDeleteMessage(${m.MessageID})">
                                        <i class="bi bi-trash3"></i>
                                    </button>
                                </div>
                            </td>
                        </tr>`;
                    });
                }
                $('#messagesTableBody').html(html);

                // Render full pagination component
                renderPaginationComponent('msgPaginationBtns', 'msgPaginationInfo', res.page, res.totalPages, res.totalCount, 30, 'loadMessagesPage', 'tin nhắn');

                // Reset selection for newly rendered page
                $('#msgSelectAll').prop('checked', false);
                updateMsgSelectBar();
            } else {
                $('#messagesTableBody').html(`<tr><td colspan="8" class="text-center py-4 text-danger"><i class="bi bi-exclamation-triangle"></i> ${res.message || 'Lỗi khi tải dữ liệu'}</td></tr>`);
            }
        });
    }

    // Checkbox select all & bulk delete for Messages
    $(document).on('change', '#msgSelectAll', function () {
        $('.msg-checkbox').prop('checked', this.checked);
        updateMsgSelectBar();
    });

    $(document).on('change', '.msg-checkbox', function () {
        var totalCheckboxes = $('.msg-checkbox').length;
        var checkedCount = $('.msg-checkbox:checked').length;
        $('#msgSelectAll').prop('checked', totalCheckboxes > 0 && totalCheckboxes === checkedCount);
        updateMsgSelectBar();
    });

    function updateMsgSelectBar() {
        var count = $('.msg-checkbox:checked').length;
        $('#msgSelectCount').text(count);
        if (count > 0) {
            $('#msgSelectBar').addClass('show');
            $('#btnDeleteSelectedMessages').prop('disabled', false);
        } else {
            $('#msgSelectBar').removeClass('show');
            $('#btnDeleteSelectedMessages').prop('disabled', true);
        }
    }

    $(document).on('click', '#btnClearSelectMessages', function () {
        $('.msg-checkbox').prop('checked', false);
        $('#msgSelectAll').prop('checked', false);
        updateMsgSelectBar();
    });

    $(document).on('click', '#btnDeleteSelectedMessages', function () {
        var selectedIds = $('.msg-checkbox:checked').map(function () { return parseInt($(this).val()); }).get();
        if (selectedIds.length === 0) return;

        Swal.fire({
            title: `Xóa ${selectedIds.length} tin nhắn đã chọn?`,
            text: `Bạn có chắc chắn muốn xóa vĩnh viễn ${selectedIds.length} tin nhắn này? Toàn bộ nội dung và tệp đính kèm sẽ bị gỡ bỏ khỏi hệ thống.`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: '<i class="bi bi-trash3 me-1"></i> Xác nhận xóa',
            cancelButtonText: 'Hủy',
            confirmButtonColor: '#ff4d4d',
            cancelButtonColor: 'rgba(255,255,255,0.2)'
        }).then(function (result) {
            if (result.isConfirmed) {
                $.post('/Admin/AdminChat/AdminDeleteMultipleMessages', {
                    ids: selectedIds,
                    reason: 'Xóa hàng loạt bởi Quản trị viên',
                    __RequestVerificationToken: getCsrfToken()
                }, function (response) {
                    if (response.success) {
                        Swal.fire({
                            icon: 'success',
                            title: 'Thành công',
                            text: response.message,
                            timer: 2000,
                            showConfirmButton: false
                        });

                        // Clear selection
                        $('#btnClearSelectMessages').trigger('click');

                        // Check if deleted all items on current page
                        var remainingOnPage = $('.msg-checkbox').length - selectedIds.length;
                        var targetPage = (remainingOnPage <= 0 && currentMsgPage > 1) ? currentMsgPage - 1 : currentMsgPage;
                        loadMessagesPage(targetPage);
                    } else {
                        Swal.fire('Lỗi', response.message, 'error');
                    }
                }).fail(function () {
                    Swal.fire('Lỗi', 'Không thể kết nối đến máy chủ hoặc phiên làm việc đã hết hạn.', 'error');
                });
            }
        });
    });

"""
    content = content[:pos_start] + new_msg_js + content[pos_end:]
    print("6. Successfully replaced loadMessagesPage and injected pagination + bulk delete JS!")
else:
    print("WARNING: pos_start or pos_end not found for loadMessagesPage replacement")

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("Saved updated Index.cshtml successfully!")
