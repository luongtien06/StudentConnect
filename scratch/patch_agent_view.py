import json

# 1. Update agent_config.json
with open('App_Data/agent_config.json', 'r', encoding='utf-8') as f:
    cfg = json.load(f)

cfg['PrimaryModel'] = 'gemini-3.5-flash-lite'
cfg['FallbackModels'] = ['gemini-flash-lite-latest', 'gemini-3.5-flash']

with open('App_Data/agent_config.json', 'w', encoding='utf-8') as f:
    json.dump(cfg, f, indent=2, ensure_ascii=False)

print('App_Data/agent_config.json updated!')

# 2. Update Index.cshtml select options
with open('Areas/Admin/Views/AdminAiAgent/Index.cshtml', 'rb') as f:
    view_content = f.read().decode('utf-8')

old_select = '''                                <select name="PrimaryModel" class="form-dark-control font-monospace" @(!canManageContent ? "disabled" : "")>
                                    <option value="gemini-3.5-flash-lite" @(Model.Config.PrimaryModel == "gemini-3.5-flash-lite" ? "selected" : "")>gemini-3.5-flash-lite (Khuyên dùng - Nhanh, dồi dào Quota & Gọi Tool chuẩn xác)</option>
                                    <option value="gemini-flash-lite-latest" @(Model.Config.PrimaryModel == "gemini-flash-lite-latest" ? "selected" : "")>gemini-flash-lite-latest (Tự động cập nhật Flash Lite)</option>
                                    <option value="gemini-3.5-flash" @(Model.Config.PrimaryModel == "gemini-3.5-flash" ? "selected" : "")>gemini-3.5-flash (Thông minh, hỗ trợ đa tác vụ)</option>
                                    <option value="gemini-3.1-flash-lite" @(Model.Config.PrimaryModel == "gemini-3.1-flash-lite" ? "selected" : "")>gemini-3.1-flash-lite (Bản Flash Lite ổn định)</option>
                                    <option value="gemini-2.5-flash" @(Model.Config.PrimaryModel == "gemini-2.5-flash" ? "selected" : "")>gemini-2.5-flash (Dự phòng)</option>
                                </select>'''

new_select = '''                                <select name="PrimaryModel" class="form-dark-control font-monospace" @(!canManageContent ? "disabled" : "")>
                                    <option value="gemini-3.5-flash-lite" @(Model.Config.PrimaryModel == "gemini-3.5-flash-lite" ? "selected" : "")>gemini-3.5-flash-lite (Khuyên dùng - Nhanh, dồi dào Quota & Gọi Tool chuẩn xác)</option>
                                    <option value="gemini-flash-lite-latest" @(Model.Config.PrimaryModel == "gemini-flash-lite-latest" ? "selected" : "")>gemini-flash-lite-latest (Tự động cập nhật Flash Lite)</option>
                                    <option value="gemini-3.5-flash" @(Model.Config.PrimaryModel == "gemini-3.5-flash" ? "selected" : "")>gemini-3.5-flash (Thông minh, hỗ trợ đa tác vụ & Function Calling)</option>
                                </select>'''

old_select_crlf = old_select.replace('\n', '\r\n')
new_select_crlf = new_select.replace('\n', '\r\n')

if old_select_crlf in view_content:
    view_content = view_content.replace(old_select_crlf, new_select_crlf)
elif old_select in view_content:
    view_content = view_content.replace(old_select, new_select)
else:
    print('Warning: old_select not found!')

with open('Areas/Admin/Views/AdminAiAgent/Index.cshtml', 'wb') as f:
    f.write(view_content.encode('utf-8'))

print('Areas/Admin/Views/AdminAiAgent/Index.cshtml updated!')
