file_path = r'Areas/Admin/Views/AdminUser/Index.cshtml'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Target inside #editModal right before `</div>\n                </div>\n                <div class="modal-footer">`
edit_modal_marker = '''                            else
                            {
                                <input type="text" class="form-control" value="Người dùng (User)" readonly disabled />
                                <input type="hidden" name="IsAdmin" id="edit-IsAdmin" value="false" />
                                <input type="hidden" name="AdminRole" id="edit-AdminRole" value="" />
                            }
                        </div>
                    </div>'''

edit_modal_replacement = '''                            else
                            {
                                <input type="text" class="form-control" value="Người dùng (User)" readonly disabled />
                                <input type="hidden" name="IsAdmin" id="edit-IsAdmin" value="false" />
                                <input type="hidden" name="AdminRole" id="edit-AdminRole" value="" />
                            }
                        </div>

                        @* ── Mục Đổi mật khẩu (Mở rộng / Thu gọn) ── *@
                        <div class="col-12 mt-2">
                            <div class="border rounded p-2" style="background:var(--dk-bg2); border-color:var(--dk-border)!important;">
                                <button type="button" class="btn btn-sm w-100 d-flex justify-content-between align-items-center text-decoration-none shadow-none p-1"
                                        data-bs-toggle="collapse" data-bs-target="#editPasswordCollapse" aria-expanded="false" aria-controls="editPasswordCollapse"
                                        id="btnToggleEditPassword" style="color:var(--dk-text); font-weight:600;">
                                    <span>
                                        <i class="bi bi-key-fill me-2" style="color:var(--dk-blue)"></i>Đổi mật khẩu tài khoản
                                        <small class="text-muted fw-normal ms-1">(Tùy chọn - Bấm để mở rộng)</small>
                                    </span>
                                    <i class="bi bi-chevron-down" id="iconEditPasswordCollapse" style="color:var(--dk-text3)"></i>
                                </button>

                                <div class="collapse mt-2" id="editPasswordCollapse">
                                    <div class="pt-2 border-top" style="border-color:var(--dk-border)!important;">
                                        <div class="alert alert-info py-2 px-3 mb-2" style="font-size:12px; background:rgba(59,130,246,0.1); border-color:rgba(59,130,246,0.2); color:var(--dk-text2);">
                                            <i class="bi bi-info-circle me-1" style="color:var(--dk-blue)"></i>
                                            Để trống cả hai trường nếu muốn <strong>giữ nguyên mật khẩu hiện tại</strong>.
                                        </div>
                                        <div class="row g-2">
                                            <div class="col-12">
                                                <label class="fw-bold mb-1" style="font-size:12px;" for="edit-NewPassword">Mật khẩu mới</label>
                                                <div class="input-group">
                                                    <input type="password" class="form-control" name="newPassword" id="edit-NewPassword"
                                                           placeholder="Nhập mật khẩu mới..." autocomplete="new-password" />
                                                    <button class="btn btn-outline-secondary btn-toggle-pwd" type="button" data-target="edit-NewPassword" title="Hiện/ẩn mật khẩu" style="border-color:var(--dk-border); color:var(--dk-text2);">
                                                        <i class="bi bi-eye"></i>
                                                    </button>
                                                </div>
                                                <div class="text-danger mt-1" id="edit-NewPassword-error" style="display:none; font-size:12px;"></div>
                                                <div class="form-text" style="font-size:11px; color:var(--dk-text3);">
                                                    Tối thiểu 8 ký tự, bao gồm cả chữ cái, số và ký tự đặc biệt (!@#$%^&*...).
                                                </div>
                                            </div>
                                            <div class="col-12">
                                                <label class="fw-bold mb-1" style="font-size:12px;" for="edit-ConfirmPassword">Xác nhận mật khẩu mới</label>
                                                <div class="input-group">
                                                    <input type="password" class="form-control" name="confirmPassword" id="edit-ConfirmPassword"
                                                           placeholder="Nhập lại mật khẩu mới..." autocomplete="new-password" />
                                                    <button class="btn btn-outline-secondary btn-toggle-pwd" type="button" data-target="edit-ConfirmPassword" title="Hiện/ẩn mật khẩu" style="border-color:var(--dk-border); color:var(--dk-text2);">
                                                        <i class="bi bi-eye"></i>
                                                    </button>
                                                </div>
                                                <div class="text-danger mt-1" id="edit-ConfirmPassword-error" style="display:none; font-size:12px;"></div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>'''

if edit_modal_marker in content:
    content = content.replace(edit_modal_marker, edit_modal_replacement, 1)
    print("SUCCESS: Edit modal HTML replaced.")
else:
    print("FAILED: Edit modal marker not found.")

# 2. Target in JavaScript: inside `.btn-edit-user` click handler
js_marker = '''                new bootstrap.Modal(document.getElementById('editModal')).show();
            });'''

js_replacement = '''                // Reset form đổi mật khẩu và thu gọn collapse
                $('#edit-NewPassword').val('').removeClass('is-invalid').attr('type', 'password');
                $('#edit-ConfirmPassword').val('').removeClass('is-invalid').attr('type', 'password');
                $('#edit-NewPassword-error').hide().text('');
                $('#edit-ConfirmPassword-error').hide().text('');
                $('.btn-toggle-pwd i').removeClass('bi-eye-slash').addClass('bi-eye');
                var collapseEl = document.getElementById('editPasswordCollapse');
                if (collapseEl && collapseEl.classList.contains('show')) {
                    var bsCol = bootstrap.Collapse.getInstance(collapseEl);
                    if (bsCol) bsCol.hide();
                    else collapseEl.classList.remove('show');
                }
                $('#iconEditPasswordCollapse').removeClass('bi-chevron-up').addClass('bi-chevron-down');

                new bootstrap.Modal(document.getElementById('editModal')).show();
            });

            // ── Bật/Tắt hiện mật khẩu ─────────────────────────────────────────
            $(document).on('click', '.btn-toggle-pwd', function () {
                var targetId = $(this).data('target');
                var input = document.getElementById(targetId);
                if (!input) return;
                var icon = $(this).find('i');
                if (input.type === 'password') {
                    input.type = 'text';
                    icon.removeClass('bi-eye').addClass('bi-eye-slash');
                } else {
                    input.type = 'password';
                    icon.removeClass('bi-eye-slash').addClass('bi-eye');
                }
            });

            // ── Đồng bộ icon xoay chevron khi mở/đóng collapse mật khẩu ───────
            var pwdCollapseEl = document.getElementById('editPasswordCollapse');
            if (pwdCollapseEl) {
                pwdCollapseEl.addEventListener('show.bs.collapse', function () {
                    $('#iconEditPasswordCollapse').removeClass('bi-chevron-down').addClass('bi-chevron-up');
                });
                pwdCollapseEl.addEventListener('hide.bs.collapse', function () {
                    $('#iconEditPasswordCollapse').removeClass('bi-chevron-up').addClass('bi-chevron-down');
                });
            }

            // ── Kiểm tra hợp lệ form Sửa người dùng (Validation tiếng Việt) ───
            function ensurePasswordCollapseOpen() {
                var collapseEl = document.getElementById('editPasswordCollapse');
                if (collapseEl && !collapseEl.classList.contains('show')) {
                    var bsCol = bootstrap.Collapse.getOrCreateInstance(collapseEl);
                    bsCol.show();
                }
            }

            $('#editModal form').on('submit', function (e) {
                var newPwd = ($('#edit-NewPassword').val() || '').trim();
                var confirmPwd = ($('#edit-ConfirmPassword').val() || '').trim();

                // Xóa trạng thái lỗi cũ
                $('#edit-NewPassword').removeClass('is-invalid');
                $('#edit-ConfirmPassword').removeClass('is-invalid');
                $('#edit-NewPassword-error').hide().text('');
                $('#edit-ConfirmPassword-error').hide().text('');

                // Cả 2 đều rỗng -> hợp lệ, giữ nguyên mật khẩu hiện tại
                if (!newPwd && !confirmPwd) {
                    return true;
                }

                var hasError = false;

                // Kiểm tra nếu chỉ nhập 1 trong 2 trường
                if (!newPwd) {
                    ensurePasswordCollapseOpen();
                    $('#edit-NewPassword').addClass('is-invalid');
                    $('#edit-NewPassword-error').text('Vui lòng nhập mật khẩu mới!').show();
                    hasError = true;
                }

                if (!confirmPwd) {
                    ensurePasswordCollapseOpen();
                    $('#edit-ConfirmPassword').addClass('is-invalid');
                    $('#edit-ConfirmPassword-error').text('Vui lòng nhập xác nhận mật khẩu mới!').show();
                    hasError = true;
                }

                if (hasError) {
                    e.preventDefault();
                    return false;
                }

                // Kiểm tra quy tắc mật khẩu mạnh
                var hasNumber = /[0-9]+/;
                var hasChar = /[a-zA-Z]+/;
                var hasSymbols = /[!@#$%^&*()_+=\[{\]};:<>|./?,-]/;

                if (newPwd.length < 8 || !hasNumber.test(newPwd) || !hasChar.test(newPwd) || !hasSymbols.test(newPwd)) {
                    ensurePasswordCollapseOpen();
                    $('#edit-NewPassword').addClass('is-invalid');
                    $('#edit-NewPassword-error').text('Mật khẩu phải có ít nhất 8 ký tự, bao gồm cả chữ cái, số và ký tự đặc biệt!').show();
                    $('#edit-NewPassword').focus();
                    e.preventDefault();
                    return false;
                }

                // Kiểm tra khớp mật khẩu
                if (newPwd !== confirmPwd) {
                    ensurePasswordCollapseOpen();
                    $('#edit-ConfirmPassword').addClass('is-invalid');
                    $('#edit-ConfirmPassword-error').text('Mật khẩu xác nhận không khớp!').show();
                    $('#edit-ConfirmPassword').focus();
                    e.preventDefault();
                    return false;
                }

                return true;
            });'''

if js_marker in content:
    content = content.replace(js_marker, js_replacement, 1)
    print("SUCCESS: JavaScript handlers added.")
else:
    print("FAILED: JavaScript marker not found.")

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)
print("Done writing Index.cshtml")
