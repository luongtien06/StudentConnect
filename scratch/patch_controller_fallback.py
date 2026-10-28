with open('Areas/Admin/Controllers/AdminAiAgentController.cs', 'rb') as f:
    raw = f.read()

target = b'''        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteSelectedLogs(long[] logIds)
        {
            if (!CheckPermission("DeleteLogs", out string err))
                return Json(new { success = false, message = err });

            if (logIds == null || logIds.Length == 0)
                return Json(new { success = false, message = "Vui l\xc3\xb2ng ch\xe1\xbb\x8dn \xc3\xadt nh\xe1\xba\xa5t m\xe1\xbb\x99t b\xe1\xba\xa3n ghi nh\xe1\xba\xadt k\xc3\xbd \xc4\x91\xe1\xbb\x83 x\xc3\xb3a." });'''

replacement = b'''        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteSelectedLogs(long[] logIds)
        {
            if (!CheckPermission("DeleteLogs", out string err))
                return Json(new { success = false, message = err });

            if (logIds == null || logIds.Length == 0)
            {
                var rawIds = Request.Form.GetValues("logIds[]") ?? Request.Form.GetValues("logIds");
                if (rawIds != null && rawIds.Length > 0)
                {
                    logIds = rawIds.Select(s => long.TryParse(s, out var v) ? v : 0).Where(v => v > 0).ToArray();
                }
            }

            if (logIds == null || logIds.Length == 0)
                return Json(new { success = false, message = "Vui l\xc3\xb2ng ch\xe1\xbb\x8dn \xc3\xadt nh\xe1\xba\xa5t m\xe1\xbb\x99t b\xe1\xba\xa3n ghi nh\xe1\xba\xadt k\xc3\xbd \xc4\x91\xe1\xbb\x83 x\xc3\xb3a." });'''

# Normalize target to CRLF if needed
if b'\r\n' in raw:
    target_crlf = target.replace(b'\n', b'\r\n')
    replacement_crlf = replacement.replace(b'\n', b'\r\n')
    assert target_crlf in raw, "target_crlf not found in raw"
    raw = raw.replace(target_crlf, replacement_crlf, 1)
else:
    assert target in raw, "target not found in raw"
    raw = raw.replace(target, replacement, 1)

with open('Areas/Admin/Controllers/AdminAiAgentController.cs', 'wb') as f:
    f.write(raw)
print("[+] Successfully patched DeleteSelectedLogs in AdminAiAgentController.cs")
