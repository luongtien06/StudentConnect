import os

with open('Models/Assistant/AiApiClient.cs', 'rb') as f:
    content = f.read().decode('utf-8')

old_block = '''                            // 3. Hạn mức tạm thời hoặc Rate limit theo phút (429 RESOURCE_EXHAUSTED) -> Delay và thử fallback tiếp theo
                            if ((int)response.StatusCode == 429 ||
                                errMsg.IndexOf("Quota", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                errReason == "RESOURCE_EXHAUSTED")
                            {
                                await Task.Delay(1500);
                                continue;
                            }'''

new_block = '''                            // 3. Hạn mức tạm thời hoặc Rate limit theo phút (429 RESOURCE_EXHAUSTED) -> Delay và thử fallback tiếp theo
                            if ((int)response.StatusCode == 429 ||
                                errMsg.IndexOf("Quota", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                errReason == "RESOURCE_EXHAUSTED")
                            {
                                int delayMs = 1500;
                                if (response.Headers.RetryAfter != null && response.Headers.RetryAfter.Delta.HasValue)
                                {
                                    delayMs = (int)Math.Min(10000, Math.Max(1000, response.Headers.RetryAfter.Delta.Value.TotalMilliseconds));
                                }
                                await Task.Delay(delayMs);
                                continue;
                            }'''

old_url = 'string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelToUse}:generateContent?key={Uri.EscapeDataString(keyToUse)}";'
new_url = 'string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelToUse}:generateContent";'

assert old_url in content, 'old_url not found'
content = content.replace(old_url, new_url)

old_block_crlf = old_block.replace('\n', '\r\n')
new_block_crlf = new_block.replace('\n', '\r\n')
if old_block_crlf in content:
    content = content.replace(old_block_crlf, new_block_crlf)
elif old_block in content:
    content = content.replace(old_block, new_block)
else:
    print('Warning: old_block not found in content!')

with open('Models/Assistant/AiApiClient.cs', 'wb') as f:
    f.write(content.encode('utf-8'))

print('AiApiClient.cs successfully patched!')
