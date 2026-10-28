with open('Areas/Admin/Views/AdminAiAgent/Index.cshtml', 'r', encoding='utf-8') as f:
    text = f.read()

old_block = """                                <select name="PrimaryModel" class="form-dark-control font-monospace" @(!canManageContent ? "disabled" : "")>
                                    <option value="gemini-3.5-flash-lite" @(Model.Config.PrimaryModel == "gemini-3.5-flash-lite" ? "selected" : "")>gemini-3.5-flash-lite (Khuyên dùng - Nhanh & Ổn định)</option>
                                    <option value="gemini-flash-lite-latest" @(Model.Config.PrimaryModel == "gemini-flash-lite-latest" ? "selected" : "")>gemini-flash-lite-latest</option>
                                    <option value="gemini-3.1-flash-lite" @(Model.Config.PrimaryModel == "gemini-3.1-flash-lite" ? "selected" : "")>gemini-3.1-flash-lite</option>
                                    <option value="gemini-3.5-flash" @(Model.Config.PrimaryModel == "gemini-3.5-flash" ? "selected" : "")>gemini-3.5-flash (Thông minh, hỗ trợ đa tác vụ)</option>
                                    <option value="gemini-3.8-flash" @(Model.Config.PrimaryModel == "gemini-3.8-flash" ? "selected" : "")>gemini-3.8-flash</option>
                                </select>"""

new_block = """                                <select name="PrimaryModel" class="form-dark-control font-monospace" @(!canManageContent ? "disabled" : "")>
                                    <option value="gemini-1.5-flash" @(Model.Config.PrimaryModel == "gemini-1.5-flash" ? "selected" : "")>gemini-1.5-flash (Khuyên dùng - Nhanh, dồi dào Quota & Hỗ trợ Function Calling)</option>
                                    <option value="gemini-2.0-flash" @(Model.Config.PrimaryModel == "gemini-2.0-flash" ? "selected" : "")>gemini-2.0-flash (Next-Gen - Tốc độ cao & Xử lý công cụ tối ưu)</option>
                                    <option value="gemini-1.5-pro" @(Model.Config.PrimaryModel == "gemini-1.5-pro" ? "selected" : "")>gemini-1.5-pro (Mô hình chuyên sâu - Suy luận phức tạp)</option>
                                    <option value="gemini-2.5-flash" @(Model.Config.PrimaryModel == "gemini-2.5-flash" ? "selected" : "")>gemini-2.5-flash (Thế hệ mới nhất)</option>
                                </select>"""

if old_block.replace('\r\n', '\n') in text.replace('\r\n', '\n'):
    text = text.replace('\r\n', '\n').replace(old_block.replace('\r\n', '\n'), new_block.replace('\r\n', '\n'))
    with open('Areas/Admin/Views/AdminAiAgent/Index.cshtml', 'w', encoding='utf-8') as f:
        f.write(text)
    print("Replaced select block successfully.")
else:
    print("Could not find old_block in Index.cshtml!")
