file_path = r'Areas/Admin/Controllers/AdminUserController.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

start_idx = None
end_idx = None

for i, line in enumerate(lines):
    if '// Admin/AdminUser/Edit' in line:
        start_idx = i
    if '// Admin/AdminUser/Delete' in line:
        end_idx = i
        break

if start_idx is not None and end_idx is not None:
    print(f"Found Edit method from line {start_idx+1} to {end_idx+1}")
    new_method = '''        // Admin/AdminUser/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(User user, string newPassword = null, string confirmPassword = null)
        {
            bool isAjax = false;
            try { isAjax = Request != null && Request.IsAjaxRequest(); } catch { }

            if (user == null || user.UserID <= 0)
            {
                if (isAjax) return Json(new { success = false, message = "Mã người dùng không hợp lệ!" });
                TempData["Error"] = "Mã người dùng không hợp lệ!";
                return RedirectToAction("Index");
            }

            try
            {
                var existingUser = db.Users.Find(user.UserID);
                if (existingUser == null)
                {
                    if (isAjax) return Json(new { success = false, message = "Người dùng không tồn tại!" });
                    TempData["Error"] = "Người dùng không tồn tại!";
                    return RedirectToAction("Index");
                }

                string currentRole = Session["AdminRole"] as string ?? "";

                // Phân quyền bảo mật: Chỉ Quản trị viên tối cao (SuperAdmin) mới có quyền chỉnh sửa tài khoản Quản trị viên
                if (currentRole != "SuperAdmin" && existingUser.IsAdmin)
                {
                    if (isAjax) return Json(new { success = false, message = "Chỉ Quản trị viên tối cao mới có quyền chỉnh sửa tài khoản Quản trị viên!" });
                    TempData["Error"] = "Chỉ Quản trị viên tối cao mới có quyền chỉnh sửa tài khoản Quản trị viên!";
                    return RedirectToAction("Index");
                }

                string cleanUsername = (user.Username ?? "").Trim();
                string cleanEmail = (user.Email ?? "").Trim();

                if (string.IsNullOrWhiteSpace(cleanUsername))
                {
                    if (isAjax) return Json(new { success = false, message = "Tên tài khoản không được để trống!" });
                    TempData["Error"] = "Tên tài khoản không được để trống!";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(cleanEmail))
                {
                    if (isAjax) return Json(new { success = false, message = "Email không được để trống!" });
                    TempData["Error"] = "Email không được để trống!";
                    return RedirectToAction("Index");
                }

                // Kiểm tra trùng Email với tài khoản khác
                var dupEmail = db.Users.FirstOrDefault(u => u.UserID != user.UserID && u.Email.ToLower() == cleanEmail.ToLower());
                if (dupEmail != null)
                {
                    if (isAjax) return Json(new { success = false, message = "Email này đã được sử dụng bởi tài khoản khác!" });
                    TempData["Error"] = "Email này đã được sử dụng bởi tài khoản khác!";
                    return RedirectToAction("Index");
                }

                // Kiểm tra trùng Username với tài khoản khác
                var dupUsername = db.Users.FirstOrDefault(u => u.UserID != user.UserID && u.Username.ToLower() == cleanUsername.ToLower());
                if (dupUsername != null)
                {
                    if (isAjax) return Json(new { success = false, message = "Tên đăng nhập này đã được sử dụng bởi tài khoản khác!" });
                    TempData["Error"] = "Tên đăng nhập này đã được sử dụng bởi tài khoản khác!";
                    return RedirectToAction("Index");
                }

                existingUser.Username = cleanUsername;
                existingUser.Email = cleanEmail;
                if (user.PhoneNumber != null)
                {
                    existingUser.PhoneNumber = user.PhoneNumber;
                }
                existingUser.IsTDMUStudent = user.IsTDMUStudent;

                // Chỉ SuperAdmin mới được phân quyền hoặc thay đổi cờ Quản trị viên
                if (currentRole == "SuperAdmin")
                {
                    existingUser.IsAdmin = user.IsAdmin;
                    existingUser.AdminRole = user.IsAdmin ? (string.IsNullOrEmpty(user.AdminRole) ? "AdminUser" : user.AdminRole) : null;
                }

                // Xử lý đổi mật khẩu:
                // Nếu cả hai trường đều để trống -> Giữ nguyên mật khẩu hiện tại (không thay đổi PasswordHash)
                bool passwordChanged = false;
                string trimmedNewPwd = (newPassword ?? "").Trim();
                string trimmedConfirmPwd = (confirmPassword ?? "").Trim();

                if (!string.IsNullOrEmpty(trimmedNewPwd) || !string.IsNullOrEmpty(trimmedConfirmPwd))
                {
                    if (string.IsNullOrEmpty(trimmedNewPwd) || string.IsNullOrEmpty(trimmedConfirmPwd))
                    {
                        if (isAjax) return Json(new { success = false, message = "Vui lòng nhập đầy đủ cả mật khẩu mới và xác nhận mật khẩu mới!" });
                        TempData["Error"] = "Vui lòng nhập đầy đủ cả mật khẩu mới và xác nhận mật khẩu mới!";
                        return RedirectToAction("Index");
                    }

                    if (trimmedNewPwd != trimmedConfirmPwd)
                    {
                        if (isAjax) return Json(new { success = false, message = "Mật khẩu xác nhận không khớp!" });
                        TempData["Error"] = "Mật khẩu xác nhận không khớp!";
                        return RedirectToAction("Index");
                    }

                    // Quy tắc mật khẩu mạnh của hệ thống (tối thiểu 8 ký tự, có chữ cái, số và ký tự đặc biệt)
                    var hasNumber = new System.Text.RegularExpressions.Regex(@"[0-9]+");
                    var hasChar = new System.Text.RegularExpressions.Regex(@"[a-zA-Z]+");
                    var hasSymbols = new System.Text.RegularExpressions.Regex(@"[!@#$%^&*()_+=\[{\]};:<>|./?,-]");

                    if (trimmedNewPwd.Length < 8 ||
                        !hasNumber.IsMatch(trimmedNewPwd) ||
                        !hasChar.IsMatch(trimmedNewPwd) ||
                        !hasSymbols.IsMatch(trimmedNewPwd))
                    {
                        if (isAjax) return Json(new { success = false, message = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm cả chữ cái, số và ký tự đặc biệt!" });
                        TempData["Error"] = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm cả chữ cái, số và ký tự đặc biệt!";
                        return RedirectToAction("Index");
                    }

                    // Băm bằng PasswordHelper chuẩn (PBKDF2 với Salt 128-bit SHA-256)
                    existingUser.PasswordHash = PasswordHelper.HashPassword(trimmedNewPwd);
                    passwordChanged = true;
                }

                db.SaveChanges();

                string successMsg = passwordChanged
                    ? "Cập nhật thông tin và đổi mật khẩu người dùng thành công!"
                    : "Cập nhật thông tin người dùng thành công!";

                if (isAjax) return Json(new { success = true, message = successMsg, passwordChanged = passwordChanged });
                TempData["Success"] = successMsg;
            }
            catch (Exception ex)
            {
                string errMsg = "Có lỗi xảy ra khi cập nhật: " + ex.Message;
                if (isAjax) return Json(new { success = false, message = errMsg });
                TempData["Error"] = errMsg;
            }

            return RedirectToAction("Index");
        }

'''
    updated_lines = lines[:start_idx] + [new_method] + lines[end_idx:]
    with open(file_path, 'w', encoding='utf-8') as f:
        f.writelines(updated_lines)
    print("SUCCESS: Updated Edit method precisely.")
else:
    print("FAILED: Could not find start or end index.")
