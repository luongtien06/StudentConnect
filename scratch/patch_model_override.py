with open('Models/Assistant/AiApiClient.cs', 'rb') as f:
    c = f.read().decode('utf-8')

old_check = '''            string modelToUse = !string.IsNullOrWhiteSpace(customModel) ? customModel.Trim() : (Model ?? "gemini-3.5-flash-lite");
            if (!Array.Exists(AgentConfigModel.SupportedModels, m => string.Equals(m, modelToUse, StringComparison.OrdinalIgnoreCase)))
            {
                modelToUse = "gemini-3.5-flash-lite";
            }'''

new_check = '''            // Sử dụng chính xác model được yêu cầu kiểm thử để trả về phản hồi thực tế từ Google API (không âm thầm ghi đè)
            string modelToUse = !string.IsNullOrWhiteSpace(customModel) ? customModel.Trim() : (Model ?? "gemini-3.5-flash-lite");'''

old_crlf = old_check.replace('\n', '\r\n')
new_crlf = new_check.replace('\n', '\r\n')

if old_crlf in c:
    c = c.replace(old_crlf, new_crlf)
elif old_check in c:
    c = c.replace(old_check, new_check)
else:
    print('Warning: old_check not found in AiApiClient.cs!')

with open('Models/Assistant/AiApiClient.cs', 'wb') as f:
    f.write(c.encode('utf-8'))

print('AiApiClient.cs updated: Removed silent model override in TestConnectionAsync!')
