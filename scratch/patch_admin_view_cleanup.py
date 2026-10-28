with open('Areas/Admin/Views/AdminAiAgent/Index.cshtml', 'r', encoding='utf-8') as f:
    view = f.read()

# 1. Update toolbar: Add Delete Selected button
old_toolbar = """                <div class="d-flex align-items-center gap-2">
                    @if (canDeleteLogs)
                    {
                        <button type="button" class="btn btn-sm btn-outline-danger" style="border-radius:8px; font-weight:600;" onclick="promptDeleteOldLogs()">
                            <i class="bi bi-trash3 me-1"></i> Dọn nhật ký cũ
                        </button>
                    }
                </div>"""

new_toolbar = """                <div class="d-flex align-items-center gap-2">
                    @if (canDeleteLogs)
                    {
                        <button type="button" id="btnDeleteSelectedLogs" class="btn btn-sm btn-danger d-none" style="border-radius:8px; font-weight:600;" onclick="confirmDeleteSelectedLogs()">
                            <i class="bi bi-trash3-fill me-1"></i> Xóa đã chọn (<span id="selectedLogsCount">0</span>)
                        </button>
                        <button type="button" class="btn btn-sm btn-outline-danger" style="border-radius:8px; font-weight:600;" onclick="promptDeleteOldLogs()">
                            <i class="bi bi-trash3 me-1"></i> Dọn nhật ký cũ
                        </button>
                    }
                </div>"""

# 2. Update logs table header and body
old_table_head = """                    <thead>
                        <tr>
                            <th>Log ID</th>
                            <th>Thời gian</th>
                            <th>Người dùng</th>
                            <th>Hành động (Action)</th>
                            <th>Mục tiêu (Target)</th>
                            <th>Trạng thái</th>
                            <th>Tóm tắt kết quả</th>
                            <th style="text-align:right;">Chi tiết</th>
                        </tr>
                    </thead>
                    <tbody id="logsTableBody">
                        @if (Model.Logs.Count == 0)
                        {
                            <tr>
                                <td colspan="8" class="text-center py-4 text-white-50">
                                    Chưa có bản ghi nhật ký hoạt động nào.
                                </td>
                            </tr>
                        }
                        @foreach (var log in Model.Logs)
                        {
                            <tr>
                                <td class="font-monospace text-white-50">#@log.LogID</td>"""

new_table_head = """                    <thead>
                        <tr>
                            @if (canDeleteLogs)
                            {
                                <th style="width:36px; text-align:center;">
                                    <input type="checkbox" id="chkSelectAllLogs" class="form-check-input" title="Chọn tất cả" onchange="toggleSelectAllLogs(this)" />
                                </th>
                            }
                            <th>Log ID</th>
                            <th>Thời gian</th>
                            <th>Người dùng</th>
                            <th>Hành động (Action)</th>
                            <th>Mục tiêu (Target)</th>
                            <th>Trạng thái</th>
                            <th>Tóm tắt kết quả</th>
                            <th style="text-align:right;">Chi tiết</th>
                        </tr>
                    </thead>
                    <tbody id="logsTableBody">
                        @if (Model.Logs.Count == 0)
                        {
                            <tr>
                                <td colspan="9" class="text-center py-4 text-white-50">
                                    Chưa có bản ghi nhật ký hoạt động nào.
                                </td>
                            </tr>
                        }
                        @foreach (var log in Model.Logs)
                        {
                            <tr>
                                @if (canDeleteLogs)
                                {
                                    <td style="text-align:center;">
                                        <input type="checkbox" class="form-check-input chk-log-item" value="@log.LogID" onchange="updateSelectedLogsCount()" />
                                    </td>
                                }
                                <td class="font-monospace text-white-50">#@log.LogID</td>"""

# 3. Update executeLogFilter rendering in JS
old_exec_render = """                logs.forEach(l => {
                    const statusClass = l.Status === 'Success' || l.Status === 'Succeeded' ? 'active' : (l.Status === 'Failed' ? 'failed' : 'paused');
                    html += `<tr>
                        <td class="font-monospace text-white-50">#${l.LogID}</td>
                        <td class="font-monospace" style="font-size:11.5px;">${l.CreatedAtFormatted}</td>
                        <td class="text-white fw-bold">${l.UserName}</td>
                        <td><span class="badge-status info" style="font-size:11px;">${l.ActionName}</span></td>
                        <td style="font-size:11.5px; color:rgba(255,255,255,0.6);">${l.TargetType || '—'} ${l.TargetID ? '#' + l.TargetID : ''}</td>
                        <td><span class="badge-status ${statusClass}">${l.Status}</span></td>
                        <td><span style="display:inline-block; max-width:260px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font-size:12px; color:rgba(255,255,255,0.7);">${l.ResultSnippet || l.ErrorMessage || '—'}</span></td>
                        <td style="text-align:right;">
                            <button type="button" class="btn-action-icon" title="Xem chi tiết" onclick="viewLogDetail(${l.LogID})">
                                <i class="bi bi-eye text-info"></i>
                            </button>
                            ${canDelLogs ? `
                                <button type="button" class="btn-action-icon text-danger ms-1" title="Xóa bản ghi" onclick="confirmDeleteSingleLog(${l.LogID})">
                                    <i class="bi bi-trash3 text-danger"></i>
                                </button>
                            ` : ''}
                        </td>
                    </tr>`;
                });
                $('#logsTableBody').html(html);"""

new_exec_render = """                logs.forEach(l => {
                    const statusClass = l.Status === 'Success' || l.Status === 'Succeeded' ? 'active' : (l.Status === 'Failed' ? 'failed' : 'paused');
                    html += `<tr>
                        ${canDelLogs ? `
                            <td style="text-align:center;">
                                <input type="checkbox" class="form-check-input chk-log-item" value="${l.LogID}" onchange="updateSelectedLogsCount()" />
                            </td>
                        ` : ''}
                        <td class="font-monospace text-white-50">#${l.LogID}</td>
                        <td class="font-monospace" style="font-size:11.5px;">${l.CreatedAtFormatted}</td>
                        <td class="text-white fw-bold">${l.UserName}</td>
                        <td><span class="badge-status info" style="font-size:11px;">${l.ActionName}</span></td>
                        <td style="font-size:11.5px; color:rgba(255,255,255,0.6);">${l.TargetType || '—'} ${l.TargetID ? '#' + l.TargetID : ''}</td>
                        <td><span class="badge-status ${statusClass}">${l.Status}</span></td>
                        <td><span style="display:inline-block; max-width:260px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font-size:12px; color:rgba(255,255,255,0.7);">${l.ResultSnippet || l.ErrorMessage || '—'}</span></td>
                        <td style="text-align:right;">
                            <button type="button" class="btn-action-icon" title="Xem chi tiết" onclick="viewLogDetail(${l.LogID})">
                                <i class="bi bi-eye text-info"></i>
                            </button>
                            ${canDelLogs ? `
                                <button type="button" class="btn-action-icon text-danger ms-1" title="Xóa bản ghi" onclick="confirmDeleteSingleLog(${l.LogID})">
                                    <i class="bi bi-trash3 text-danger"></i>
                                </button>
                            ` : ''}
                        </td>
                    </tr>`;
                });
                $('#logsTableBody').html(html);
                if (typeof updateSelectedLogsCount === 'function') updateSelectedLogsCount();"""

# 4. Replace promptDeleteOldLogs & executeDeleteOldLogs with rich preview & selection functions
old_js_cleanup = """    function toggleCustomDaysInput(val) {
        const group = document.getElementById('swalCustomDaysGroup');
        if (group) {
            group.style.display = val === 'custom' ? 'block' : 'none';
        }
    }

    function promptDeleteOldLogs() {
        Swal.fire({
            title: '<div class="d-flex align-items-center justify-content-center gap-2"><i class="bi bi-trash3 text-danger"></i> Dọn dẹp Nhật ký hoạt động</div>',
            html: `
                <div class="text-start" style="font-size:13px; color:rgba(255,255,255,0.85);">
                    <p class="mb-3 text-white-50">Chọn phạm vi thời gian hoặc tiêu chí để dọn dẹp các bản ghi nhật ký kiểm toán không còn cần thiết:</p>
                    
                    <div class="mb-3">
                        <label class="form-label text-info fw-bold" style="font-size:12px;">MỐC THỜI GIAN CẦN DỌN:</label>
                        <select id="swalLogDays" class="form-select bg-dark text-white border-secondary" style="font-size:13px;" onchange="toggleCustomDaysInput(this.value)">
                            <option value="all">⚡ Xóa toàn bộ nhật ký (Xóa tất cả bản ghi)</option>
                            <option value="1">📅 Cũ hơn hôm nay (Cũ hơn 1 ngày)</option>
                            <option value="3">📅 Cũ hơn 3 ngày qua</option>
                            <option value="7" selected>📅 Cũ hơn 7 ngày qua</option>
                            <option value="14">📅 Cũ hơn 14 ngày qua</option>
                            <option value="30">📅 Cũ hơn 30 ngày qua</option>
                            <option value="custom">✏️ Tùy chỉnh số ngày...</option>
                        </select>
                    </div>

                    <div class="mb-3" id="swalCustomDaysGroup" style="display:none;">
                        <label class="form-label text-white-50" style="font-size:12px;">NHẬP SỐ NGÀY (Cũ hơn X ngày sẽ bị xóa):</label>
                        <input type="number" id="swalCustomDaysVal" min="0" max="365" value="7" class="form-control bg-dark text-white border-secondary" style="font-size:13px;" placeholder="Ví dụ: 5" />
                    </div>

                    <div class="mb-3">
                        <label class="form-label text-info fw-bold" style="font-size:12px;">LỌC THEO TRẠNG THÁI:</label>
                        <select id="swalLogStatus" class="form-select bg-dark text-white border-secondary" style="font-size:13px;">
                            <option value="All">Tất cả trạng thái (Success, Failed, Pending)</option>
                            <option value="Failed">Chỉ dọn các bản ghi Thất bại (Failed)</option>
                            <option value="Success">Chỉ dọn các bản ghi Thành công (Success / Succeeded)</option>
                        </select>
                    </div>

                    <div class="alert alert-danger d-flex align-items-center gap-2 p-2 mb-0" style="font-size:11.5px; background:rgba(255,77,77,0.1); border:1px solid rgba(255,77,77,0.3); border-radius:8px;">
                        <i class="bi bi-exclamation-triangle-fill text-danger fs-5 flex-shrink-0"></i>
                        <span>Các bản ghi thỏa mãn điều kiện sẽ bị xóa vĩnh viễn khỏi CSDL và không thể hoàn tác!</span>
                    </div>
                </div>
            `,
            showCancelButton: true,
            confirmButtonText: '<i class="bi bi-trash3 me-1"></i> Tiến hành dọn dẹp',
            cancelButtonText: 'Hủy bỏ',
            confirmButtonColor: '#ff4d4d',
            cancelButtonColor: '#495057',
            customClass: {
                popup: 'modal-dark-content'
            },
            preConfirm: () => {
                const scope = document.getElementById('swalLogDays').value;
                const status = document.getElementById('swalLogStatus').value;
                let days = 0;
                if (scope === 'all') {
                    days = 0;
                } else if (scope === 'custom') {
                    const customVal = parseInt(document.getElementById('swalCustomDaysVal').value);
                    days = isNaN(customVal) || customVal < 0 ? 0 : customVal;
                } else {
                    days = parseInt(scope) || 0;
                }
                return { days: days, status: status, scope: scope };
            }
        }).then(result => {
            if (result.isConfirmed && result.value) {
                executeDeleteOldLogs(result.value.days, result.value.status);
            }
        });
    }

    function executeDeleteOldLogs(days, status) {
        Swal.fire({
            title: 'Đang dọn dẹp nhật ký...',
            text: 'Hệ thống đang xử lý xóa bản ghi trong CSDL, vui lòng chờ trong giây lát...',
            allowOutsideClick: false,
            didOpen: () => Swal.showLoading()
        });

        $.ajax({
            url: '/Admin/AdminAiAgent/DeleteOldLogs',
            type: 'POST',
            data: {
                days: days,
                status: status,
                __RequestVerificationToken: getAntiToken()
            },
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            success: function (res) {
                if (res.success) {
                    if (res.deletedCount > 0) {
                        Swal.fire({
                            icon: 'success',
                            title: 'Dọn dẹp thành công!',
                            text: res.message,
                            confirmButtonColor: '#00c8ff'
                        }).then(() => {
                            location.reload();
                        });
                    } else {
                        Swal.fire({
                            icon: 'info',
                            title: 'Không có bản ghi phù hợp',
                            text: res.message,
                            confirmButtonColor: '#00c8ff'
                        });
                    }
                } else {
                    Swal.fire('Lỗi', res.message || 'Không thể dọn dẹp nhật ký.', 'error');
                }
            },
            error: function (xhr) {
                Swal.fire('Lỗi hệ thống', 'Không thể kết nối đến máy chủ hoặc phiên làm việc đã hết hạn. (Mã lỗi: ' + xhr.status + ')', 'error');
            }
        });
    }"""

new_js_cleanup = """    // ── CHECKBOX SELECTION CHO NHẬT KÝ ──
    function toggleSelectAllLogs(masterChk) {
        const isChecked = $(masterChk).is(':checked');
        $('.chk-log-item').prop('checked', isChecked);
        updateSelectedLogsCount();
    }

    function updateSelectedLogsCount() {
        const checkedCount = $('.chk-log-item:checked').length;
        $('#selectedLogsCount').text(checkedCount);
        if (checkedCount > 0) {
            $('#btnDeleteSelectedLogs').removeClass('d-none');
        } else {
            $('#btnDeleteSelectedLogs').addClass('d-none');
            $('#chkSelectAllLogs').prop('checked', false);
        }
    }

    function confirmDeleteSelectedLogs() {
        const selectedIds = [];
        $('.chk-log-item:checked').each(function () {
            const id = parseInt($(this).val());
            if (id > 0) selectedIds.push(id);
        });

        if (selectedIds.length === 0) {
            Swal.fire('Chưa chọn bản ghi', 'Vui lòng tích chọn ít nhất 1 bản ghi nhật ký để xóa.', 'warning');
            return;
        }

        Swal.fire({
            title: '<div class="d-flex align-items-center justify-content-center gap-2"><i class="bi bi-trash3-fill text-danger"></i> Xóa các bản ghi đã chọn</div>',
            html: `
                <div class="text-start" style="font-size:13px; color:rgba(255,255,255,0.85);">
                    <p class="mb-2">Bạn đang yêu cầu xóa vĩnh viễn <strong class="text-warning fs-6">${selectedIds.length}</strong> bản ghi nhật ký đã chọn.</p>
                    <div class="alert alert-danger p-2 mb-0" style="font-size:11.5px; border-radius:8px;">
                        <i class="bi bi-exclamation-triangle-fill text-danger me-1"></i>
                        Thao tác xóa trực tiếp CSDL và không thể hoàn tác!
                    </div>
                </div>
            `,
            showCancelButton: true,
            confirmButtonText: `<i class="bi bi-trash3-fill me-1"></i> Xóa ngay (${selectedIds.length})`,
            cancelButtonText: 'Hủy bỏ',
            confirmButtonColor: '#ff4d4d',
            cancelButtonColor: '#495057',
            customClass: { popup: 'modal-dark-content' }
        }).then(result => {
            if (result.isConfirmed) {
                Swal.fire({
                    title: 'Đang xóa các bản ghi...',
                    text: 'Vui lòng chờ trong giây lát...',
                    allowOutsideClick: false,
                    didOpen: () => Swal.showLoading()
                });

                $.ajax({
                    url: '/Admin/AdminAiAgent/DeleteSelectedLogs',
                    type: 'POST',
                    traditional: true,
                    data: {
                        logIds: selectedIds,
                        __RequestVerificationToken: getAntiToken()
                    },
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    success: function (res) {
                        if (res.success) {
                            Swal.fire({
                                icon: 'success',
                                title: 'Xóa thành công!',
                                text: res.message,
                                confirmButtonColor: '#00c8ff'
                            }).then(() => {
                                window.location.hash = 'tab-logs';
                                location.reload();
                            });
                        } else {
                            Swal.fire('Lỗi', res.message || 'Không thể xóa các bản ghi đã chọn.', 'error');
                        }
                    },
                    error: function (xhr) {
                        Swal.fire('Lỗi hệ thống', 'Không thể kết nối đến máy chủ (Mã: ' + xhr.status + ')', 'error');
                    }
                });
            }
        });
    }

    function toggleCustomDaysInput(val) {
        const group = document.getElementById('swalCustomDaysGroup');
        if (group) {
            group.style.display = val === 'custom' ? 'block' : 'none';
        }
        triggerFetchPreview();
    }

    function triggerFetchPreview() {
        const daysEl = document.getElementById('swalLogDays');
        const statusEl = document.getElementById('swalLogStatus');
        const customEl = document.getElementById('swalCustomDaysVal');
        const previewBox = document.getElementById('swalPreviewCount');
        const previewDesc = document.getElementById('swalPreviewDesc');
        const confirmBtn = Swal.getConfirmButton();

        if (!daysEl || !previewBox) return;

        const scope = daysEl.value;
        const status = statusEl ? statusEl.value : 'All';
        let days = 1;

        if (scope === 'today') {
            days = 1;
        } else if (scope === 'custom') {
            days = parseInt(customEl ? customEl.value : '7') || 1;
        } else if (scope === 'all') {
            days = 0;
        } else {
            days = parseInt(scope) || 1;
        }

        previewBox.innerHTML = '<i class="bi bi-arrow-repeat spin"></i> Đang tính...';
        if (previewDesc) previewDesc.innerHTML = '';

        $.get('/Admin/AdminAiAgent/CountLogsToDelete', { scope, days, status }, function (res) {
            if (res.success) {
                if (res.count > 0) {
                    previewBox.className = 'badge bg-warning text-dark fw-bold fs-6';
                    previewBox.innerText = res.count;
                    if (previewDesc) previewDesc.innerHTML = `<span class="text-info">${res.criteriaDesc}</span>`;
                    if (confirmBtn) {
                        confirmBtn.disabled = false;
                        confirmBtn.innerHTML = `<i class="bi bi-trash3 me-1"></i> Tiến hành dọn dẹp (${res.count} bản ghi)`;
                    }
                } else {
                    previewBox.className = 'badge bg-secondary text-white fw-bold fs-6';
                    previewBox.innerText = '0';
                    if (previewDesc) previewDesc.innerHTML = `<span class="text-warning">Không có bản ghi nào thỏa mãn điều kiện này. Hãy chọn mốc thời gian gần hơn như 'Cũ hơn hôm nay'.</span>`;
                    if (confirmBtn) {
                        confirmBtn.disabled = false;
                        confirmBtn.innerHTML = '<i class="bi bi-trash3 me-1"></i> Tiến hành dọn dẹp (0 bản ghi)';
                    }
                }
            } else {
                previewBox.innerText = 'Lỗi';
                if (previewDesc) previewDesc.innerText = res.message || 'Không thể tính toán';
            }
        }).fail(function () {
            previewBox.innerText = '—';
        });
    }

    function promptDeleteOldLogs() {
        Swal.fire({
            title: '<div class="d-flex align-items-center justify-content-center gap-2"><i class="bi bi-trash3 text-danger"></i> Dọn dẹp Nhật ký hoạt động</div>',
            html: `
                <div class="text-start" style="font-size:13px; color:rgba(255,255,255,0.85);">
                    <p class="mb-3 text-white-50">Chọn phạm vi thời gian hoặc tiêu chí để dọn dẹp các bản ghi nhật ký kiểm toán không còn cần thiết:</p>
                    
                    <div class="mb-3">
                        <label class="form-label text-info fw-bold" style="font-size:12px;">MỐC THỜI GIAN CẦN DỌN:</label>
                        <select id="swalLogDays" class="form-select bg-dark text-white border-secondary" style="font-size:13px;" onchange="toggleCustomDaysInput(this.value)">
                            <option value="today" selected>📅 Cũ hơn hôm nay (Toàn bộ các ngày trước)</option>
                            <option value="3">📅 Cũ hơn 3 ngày qua</option>
                            <option value="7">📅 Cũ hơn 7 ngày qua</option>
                            <option value="14">📅 Cũ hơn 14 ngày qua</option>
                            <option value="30">📅 Cũ hơn 30 ngày qua</option>
                            <option value="all">⚡ Xóa toàn bộ nhật ký (Tất cả bản ghi)</option>
                            <option value="custom">✏️ Tùy chỉnh số ngày...</option>
                        </select>
                    </div>

                    <div class="mb-3" id="swalCustomDaysGroup" style="display:none;">
                        <label class="form-label text-white-50" style="font-size:12px;">NHẬP SỐ NGÀY (Cũ hơn X ngày sẽ bị xóa):</label>
                        <input type="number" id="swalCustomDaysVal" min="1" max="365" value="7" class="form-control bg-dark text-white border-secondary" style="font-size:13px;" placeholder="Ví dụ: 5" oninput="triggerFetchPreview()" />
                    </div>

                    <div class="mb-3">
                        <label class="form-label text-info fw-bold" style="font-size:12px;">LỌC THEO TRẠNG THÁI:</label>
                        <select id="swalLogStatus" class="form-select bg-dark text-white border-secondary" style="font-size:13px;" onchange="triggerFetchPreview()">
                            <option value="All">Tất cả trạng thái (Thành công, Thất bại, Chờ)</option>
                            <option value="Succeeded">Chỉ dọn các bản ghi Thành công (Success / Succeeded)</option>
                            <option value="Failed">Chỉ dọn các bản ghi Thất bại (Failed)</option>
                            <option value="Pending">Chỉ dọn các bản ghi Chờ xác nhận (Pending)</option>
                        </select>
                    </div>

                    <!-- Dynamic Preview Badge -->
                    <div class="p-2 mb-3 rounded" style="background:rgba(0,200,255,0.08); border:1px solid rgba(0,200,255,0.25); font-size:12.5px;">
                        <div class="d-flex align-items-center gap-2">
                            <i class="bi bi-info-circle-fill text-info fs-5 flex-shrink-0"></i>
                            <div>
                                <div>Dự kiến sẽ dọn: <span id="swalPreviewCount" class="badge bg-warning text-dark fw-bold fs-6">Đang tính...</span> bản ghi</div>
                                <div id="swalPreviewDesc" class="text-white-50" style="font-size:11px; margin-top:2px;"></div>
                            </div>
                        </div>
                    </div>

                    <div class="alert alert-danger d-flex align-items-center gap-2 p-2 mb-0" style="font-size:11.5px; background:rgba(255,77,77,0.1); border:1px solid rgba(255,77,77,0.3); border-radius:8px;">
                        <i class="bi bi-exclamation-triangle-fill text-danger fs-5 flex-shrink-0"></i>
                        <span>Các bản ghi thỏa mãn điều kiện sẽ bị xóa vĩnh viễn khỏi CSDL và không thể hoàn tác!</span>
                    </div>
                </div>
            `,
            showCancelButton: true,
            confirmButtonText: '<i class="bi bi-trash3 me-1"></i> Tiến hành dọn dẹp',
            cancelButtonText: 'Hủy bỏ',
            confirmButtonColor: '#ff4d4d',
            cancelButtonColor: '#495057',
            customClass: {
                popup: 'modal-dark-content'
            },
            didOpen: () => {
                triggerFetchPreview();
            },
            preConfirm: () => {
                const scope = document.getElementById('swalLogDays').value;
                const status = document.getElementById('swalLogStatus').value;
                let days = 1;
                if (scope === 'today') {
                    days = 1;
                } else if (scope === 'all') {
                    days = 0;
                } else if (scope === 'custom') {
                    const customVal = parseInt(document.getElementById('swalCustomDaysVal').value);
                    days = isNaN(customVal) || customVal < 1 ? 1 : customVal;
                } else {
                    days = parseInt(scope) || 1;
                }
                return { scope: scope, days: days, status: status };
            }
        }).then(result => {
            if (result.isConfirmed && result.value) {
                executeDeleteOldLogs(result.value.scope, result.value.days, result.value.status);
            }
        });
    }

    function executeDeleteOldLogs(scope, days, status) {
        Swal.fire({
            title: 'Đang dọn dẹp nhật ký...',
            text: 'Hệ thống đang xử lý xóa bản ghi trong CSDL, vui lòng chờ trong giây lát...',
            allowOutsideClick: false,
            didOpen: () => Swal.showLoading()
        });

        $.ajax({
            url: '/Admin/AdminAiAgent/DeleteOldLogs',
            type: 'POST',
            data: {
                scope: scope,
                days: days,
                status: status,
                __RequestVerificationToken: getAntiToken()
            },
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            success: function (res) {
                if (res.success) {
                    if (res.deletedCount > 0) {
                        Swal.fire({
                            icon: 'success',
                            title: 'Dọn dẹp thành công!',
                            text: res.message,
                            confirmButtonColor: '#00c8ff'
                        }).then(() => {
                            window.location.hash = 'tab-logs';
                            location.reload();
                        });
                    } else {
                        Swal.fire({
                            icon: 'info',
                            title: 'Không có bản ghi phù hợp',
                            text: res.message,
                            confirmButtonColor: '#00c8ff'
                        });
                    }
                } else {
                    Swal.fire('Lỗi', res.message || 'Không thể dọn dẹp nhật ký.', 'error');
                }
            },
            error: function (xhr) {
                Swal.fire('Lỗi hệ thống', 'Không thể kết nối đến máy chủ hoặc phiên làm việc đã hết hạn. (Mã lỗi: ' + xhr.status + ')', 'error');
            }
        });
    }"""

# 5. Add hash restoration on document ready
old_doc_ready = """    function switchAgentTab(tabId, btn) {
        $('.agent-tab-pane').hide();
        $('#' + tabId).show();
        $('.agent-tab-btn').removeClass('active');
        $(btn).addClass('active');
    }"""

new_doc_ready = """    function switchAgentTab(tabId, btn) {
        $('.agent-tab-pane').hide();
        $('#' + tabId).show();
        $('.agent-tab-btn').removeClass('active');
        $(btn).addClass('active');
        window.location.hash = tabId;
    }

    $(function () {
        const hash = window.location.hash;
        if (hash && hash.startsWith('#tab-')) {
            const tabId = hash.substring(1);
            const btn = $(`button[onclick*="${tabId}"]`);
            if (btn.length) {
                switchAgentTab(tabId, btn[0]);
            }
        }
    });"""

view = view.replace(old_toolbar, new_toolbar)
view = view.replace(old_table_head, new_table_head)
view = view.replace(old_exec_render, new_exec_render)
view = view.replace(old_js_cleanup, new_js_cleanup)
view = view.replace(old_doc_ready, new_doc_ready)

with open('Areas/Admin/Views/AdminAiAgent/Index.cshtml', 'w', encoding='utf-8') as f:
    f.write(view)

print("SUCCESS: Updated Areas/Admin/Views/AdminAiAgent/Index.cshtml")
