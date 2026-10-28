with open('Models/Assistant/AssistantService.cs', 'rb') as f:
    c = f.read().decode('utf-8')

old_block = '''                // 2. Nếu người dùng đã nói rõ xác nhận:
                if (isConfirmed)
                {
                    var delRes = await _tools.DeletePostAsync(db, delPostId, delItemType, userId);
                    await _auditService.LogActionAsync(db, userId, toolName, targetType, targetId, "Succeeded", inputSummary, delRes.ToString(Formatting.None), null, runId, conversationId);
                    return delRes;
                }

                // 3. Nếu chưa xác nhận: tạo Pending Confirmation'''

new_block = '''                // 2. Bắt buộc kích hoạt Human-in-the-Loop: Mọi yêu cầu xóa bài đều tạo Thẻ xác nhận duyệt (Zero-Trust)
                // Ngăn chặn triệt để rủi ro prompt injection hoặc ngộ nhận lời nói từ chối/đồng ý'''

old_crlf = old_block.replace('\n', '\r\n')
new_crlf = new_block.replace('\n', '\r\n')

if old_crlf in c:
    c = c.replace(old_crlf, new_crlf)
elif old_block in c:
    c = c.replace(old_block, new_block)
else:
    print('Warning: old_block not found in AssistantService.cs!')

with open('Models/Assistant/AssistantService.cs', 'wb') as f:
    f.write(c.encode('utf-8'))

print('AssistantService.cs updated!')
