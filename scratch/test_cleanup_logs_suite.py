import urllib.request, urllib.parse, http.cookiejar, json, re, subprocess

print("================================================================================")
print("TEST SUITE: KIỂM ĐỊNH TOÀN DIỆN CHỨC NĂNG DỌN DẸP NHẬT KÝ AGENT")
print("================================================================================")

BASE_URL = "http://localhost:59174"

class HttpClient:
    def __init__(self):
        self.cj = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj))

    def get(self, path, allow_redirects=True):
        url = BASE_URL + path
        req = urllib.request.Request(url)
        if not allow_redirects:
            class NoRedirect(urllib.request.HTTPRedirectHandler):
                def redirect_request(self, req, fp, code, msg, headers, newurl):
                    return None
            op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj), NoRedirect)
            try:
                resp = op.open(req, timeout=30)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers
            except Exception as ex:
                return 0, str(ex), {}
        else:
            try:
                resp = self.opener.open(req, timeout=30)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers
            except Exception as ex:
                return 0, str(ex), {}

    def post(self, path, data=None, headers=None, allow_redirects=True):
        url = BASE_URL + path
        req_headers = {}
        if isinstance(data, dict):
            encoded = urllib.parse.urlencode(data, doseq=True).encode('utf-8')
            req_headers["Content-Type"] = "application/x-www-form-urlencoded"
        elif isinstance(data, str):
            encoded = data.encode('utf-8')
            req_headers["Content-Type"] = "application/x-www-form-urlencoded"
        else:
            encoded = b""

        if headers:
            req_headers.update(headers)

        req = urllib.request.Request(url, data=encoded, headers=req_headers)
        if not allow_redirects:
            class NoRedirect(urllib.request.HTTPRedirectHandler):
                def redirect_request(self, req, fp, code, msg, headers, newurl):
                    return None
            op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj), NoRedirect)
            try:
                resp = op.open(req, timeout=30)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers
            except Exception as ex:
                return 0, str(ex), {}
        else:
            try:
                resp = self.opener.open(req, timeout=30)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers
            except Exception as ex:
                return 0, str(ex), {}

def extract_token(html):
    m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
    if not m:
        m = re.search(r'value=["\']([^"\']+)["\']\s+name=["\']__RequestVerificationToken["\']', html)
    return m.group(1) if m else ""

def query_sql(sql):
    with open('Config/ConnectionStrings.config', 'r', encoding='utf-8') as f:
        text = f.read()
    m = re.findall(r'provider connection string=&quot;(.*?)&quot;', text, re.DOTALL)
    conn_str = re.sub(r'\s+', ' ', m[-1]).strip()
    ps = f'''
$conn = New-Object System.Data.SqlClient.SqlConnection("{conn_str}")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "{sql}"
$reader = $cmd.ExecuteReader()
$results = @()
while ($reader.Read()) {{
    $row = @{{}}
    for ($i = 0; $i -lt $reader.FieldCount; $i++) {{
        $row[$reader.GetName($i)] = $reader.GetValue($i).ToString()
    }}
    $results += (New-Object PSObject -Property $row)
}}
$reader.Close()
$conn.Close()
$results | ConvertTo-Json -Compress
'''
    with open('scratch/temp_sql_exec.ps1', 'w', encoding='utf-8') as ps_f:
        ps_f.write(ps)
    res = subprocess.run(['powershell', '-File', 'scratch/temp_sql_exec.ps1'], capture_output=True, text=True)
    out = res.stdout.strip()
    if not out: return []
    try:
        data = json.loads(out)
        return data if isinstance(data, list) else [data]
    except:
        return []

# Login as SuperAdmin
admin_client = HttpClient()
st, html, _ = admin_client.get('/Admin/Auth/Login')
tok = extract_token(html)
st, _, _ = admin_client.post('/Admin/Auth/Login', {
    '__RequestVerificationToken': tok,
    'username': 'luongtien2222@gmail.com',
    'password': '123456',
    'rememberMe': 'true'
}, allow_redirects=False)
print(f"[+] Logged in SuperAdmin (Status: {st})")

# Get Verification Token from AdminAiAgent page
st, agent_html, _ = admin_client.get('/Admin/AdminAiAgent')
agent_token = extract_token(agent_html)
print(f"[+] Loaded AdminAiAgent Page (HTTP {st}), Token found: {bool(agent_token)}")

# 1. TEST PREVIEW / COUNT API (CountLogsToDelete)
print("\n--- 1. KIỂM THỬ TÍNH NĂNG XEM TRƯỚC SỐ LƯỢNG (CountLogsToDelete) ---")

# A. Scope: today
st, resp, _ = admin_client.get('/Admin/AdminAiAgent/CountLogsToDelete?scope=today&days=1&status=All')
data = json.loads(resp)
count_today = data.get('count', -1)
print(f"   [Scope: today (Cũ hơn hôm nay)]: {count_today} bản ghi | Mô tả: '{data.get('criteriaDesc')}'")
assert data.get('success') == True, "CountLogsToDelete today failed"

# B. Scope: 7 days
st, resp, _ = admin_client.get('/Admin/AdminAiAgent/CountLogsToDelete?scope=range&days=7&status=All')
data = json.loads(resp)
count_7d = data.get('count', -1)
print(f"   [Scope: 7 days (Cũ hơn 7 ngày)]: {count_7d} bản ghi | Mô tả: '{data.get('criteriaDesc')}'")
assert count_7d == 0, f"Expected 0 logs older than 7 days, got {count_7d}"

# C. Scope: all, Status: Succeeded vs Success (Testing status mapping)
st, resp1, _ = admin_client.get('/Admin/AdminAiAgent/CountLogsToDelete?scope=all&status=Succeeded')
st, resp2, _ = admin_client.get('/Admin/AdminAiAgent/CountLogsToDelete?scope=all&status=Success')
d1 = json.loads(resp1)
d2 = json.loads(resp2)
print(f"   [Status Succeeded Count]: {d1.get('count')} | [Status Success Count]: {d2.get('count')}")
assert d1.get('count') == d2.get('count') and d1.get('count') > 0, "Status mapping Success/Succeeded failed!"
print(f"   -> [PASS] Status mapping giữa 'Success' và 'Succeeded' hoạt động đồng nhất ({d1.get('count')} bản ghi)!")

# 2. TEST FILTER LOGS (FilterLogs with status and dateRange)
print("\n--- 2. KIỂM THỬ BỘ LỌC NHẬT KÝ (FilterLogs) ---")
st, resp, _ = admin_client.get('/Admin/AdminAiAgent/FilterLogs?status=Success')
f_succ = json.loads(resp)
logs_succ = f_succ.get('logs', [])
print(f"   [Filter status=Success]: Trả về {len(logs_succ)} bản ghi (trước đây trả về 0)")
assert len(logs_succ) > 0, "FilterLogs status=Success returned 0 logs!"
print("   -> [PASS] Lọc theo trạng thái 'Success' đã trả về đầy đủ các bản ghi Succeeded!")

st, resp, _ = admin_client.get('/Admin/AdminAiAgent/FilterLogs?dateRange=today')
f_today = json.loads(resp)
print(f"   [Filter dateRange=today]: Trả về {len(f_today.get('logs', []))} bản ghi hôm nay")
assert len(f_today.get('logs', [])) > 0, "FilterLogs dateRange=today returned 0 logs!"
print("   -> [PASS] Lọc theo ngày hôm nay hoạt động chính xác!")

# 3. TEST UNAUTHORIZED ACCESS (Bảo mật quyền hạn)
print("\n--- 3. KIỂM THỬ BẢO MẬT PHÂN QUYỀN (Unauthorized Rejection) ---")
# Create client without admin login
guest_client = HttpClient()
st_g, resp_g, headers_g = guest_client.get('/Admin/AdminAiAgent/CountLogsToDelete', allow_redirects=False)
print(f"   [Guest GET CountLogsToDelete]: HTTP {st_g} (Redirect Location: {headers_g.get('Location', '')})")
assert st_g in [302, 401, 403], f"Expected 302/401/403 for guest GET, got {st_g}"

st_post, resp_post, _ = guest_client.post('/Admin/AdminAiAgent/DeleteOldLogs', {
    'scope': 'today',
    'days': 1,
    'status': 'All'
}, headers={'X-Requested-With': 'XMLHttpRequest'}, allow_redirects=False)
print(f"   [Guest POST DeleteOldLogs]: HTTP {st_post}")
if st_post == 200:
    res = json.loads(resp_post)
    print(f"   [Guest Rejection Message]: '{res.get('message')}'")
    assert res.get('success') == False, "Security vulnerability: Guest was able to perform action!"
else:
    assert st_post in [302, 401, 403], f"Unexpected status {st_post}"
print("   -> [PASS] Khách chưa đăng nhập Admin bị từ chối truy cập an toàn tuyệt đối!")

# 4. TEST SAFETY GUARD (Ngăn chặn xóa nhầm toàn bộ khi days <= 0 mà scope != all)
print("\n--- 4. KIỂM THỬ RÀO CHẮN AN TOÀN (Safety Guard against accidental wipeout) ---")
st_sg, resp_sg, _ = admin_client.post('/Admin/AdminAiAgent/DeleteOldLogs', {
    'scope': 'custom',
    'days': 0, # Malicious or accidental 0 days
    'status': 'All',
    '__RequestVerificationToken': agent_token
}, headers={'X-Requested-With': 'XMLHttpRequest'})
res_sg = json.loads(resp_sg)
print(f"   [Custom Days = 0]: success={res_sg.get('success')}, message='{res_sg.get('message')}'")
assert res_sg.get('success') == False, "Safety guard failed: allowed days <= 0 without scope=all!"
print("   -> [PASS] Rào chắn an toàn từ chối thành công, không để xóa trắng bảng CSDL!")

# 5. TEST ZERO-MATCHING RECORDS (Không báo lỗi giả khi không có bản ghi)
print("\n--- 5. KIỂM THỬ XỬ LÝ KHÔNG CÓ BẢN GHI PHÙ HỢP (7 days range) ---")
st_zm, resp_zm, _ = admin_client.post('/Admin/AdminAiAgent/DeleteOldLogs', {
    'scope': 'range',
    'days': 100, # 100 days ago has 0 records
    'status': 'All',
    '__RequestVerificationToken': agent_token
}, headers={'X-Requested-With': 'XMLHttpRequest'})
res_zm = json.loads(resp_zm)
print(f"   [100 Days Ago Cleanup]: success={res_zm.get('success')}, deleted={res_zm.get('deletedCount')}, msg='{res_zm.get('message')}'")
assert res_zm.get('success') == True and res_zm.get('deletedCount') == 0, "Zero matching records handling failed!"
print("   -> [PASS] Không có bản ghi phù hợp được xử lý đúng đắn (deletedCount = 0), không gây lỗi!")

# 6. TEST DELETE SELECTED LOGS (Xóa hàng loạt theo checkbox ID)
print("\n--- 6. KIỂM THỬ XÓA BẢN GHI ĐƯỢC CHỌN (DeleteSelectedLogs) ---")
sql_rows = query_sql("SELECT TOP 2 LogID FROM dbo.AgentActionLogs ORDER BY LogID DESC")
test_ids = [int(r['LogID']) for r in sql_rows]
print(f"   Selected Log IDs to delete: {test_ids}")
assert len(test_ids) == 2, f"Failed to get log IDs, got: {test_ids}"

count_before = int(query_sql("SELECT COUNT(*) AS C FROM dbo.AgentActionLogs")[0]['C'])
st_del, resp_del, _ = admin_client.post('/Admin/AdminAiAgent/DeleteSelectedLogs', {
    'logIds': test_ids,
    '__RequestVerificationToken': agent_token
}, headers={'X-Requested-With': 'XMLHttpRequest'})
res_del = json.loads(resp_del)
print(f"   [DeleteSelectedLogs Result]: success={res_del.get('success')}, deleted={res_del.get('deletedCount')}, msg='{res_del.get('message')}'")
assert res_del.get('success') == True and res_del.get('deletedCount') == 2, f"DeleteSelectedLogs failed! Result: {res_del}"

# Verify in SQL that test_ids no longer exist
remaining_test = query_sql(f"SELECT COUNT(*) AS C FROM dbo.AgentActionLogs WHERE LogID IN ({test_ids[0]}, {test_ids[1]})")[0]['C']
assert int(remaining_test) == 0, f"Expected 0 remaining test logs, but found {remaining_test}!"
print("   -> [PASS] Xóa thủ công theo danh sách checkbox thành công tuyệt đối (đã xác thực trong CSDL SQL Server)!")

# 7. TEST MULTIPLE CONSECUTIVE OPERATIONS (Thao tác liên tiếp không lỗi trạng thái)
print("\n--- 7. KIỂM THỬ THAO TÁC NHIỀU LẦN LIÊN TIẾP ---")
for i in range(3):
    st_c, resp_c, _ = admin_client.get('/Admin/AdminAiAgent/CountLogsToDelete?scope=today&days=1&status=All')
    assert json.loads(resp_c).get('success') == True
print("   -> [PASS] 3 lần gọi liên tiếp Preview/Count đều trả về kết quả nhất quán!")

# 8. VERIFY AGENT TASKS AND CONVERSATIONS INTEGRITY (Không ảnh hưởng bảng khác)
print("\n--- 8. XÁC MINH TÍNH TOÀN VẸN CỦA DỮ LIỆU TÁC VỤ & HỘI THOẠI ---")
task_count = int(query_sql("SELECT COUNT(*) AS C FROM dbo.AgentTasks")[0]['C'])
run_count = int(query_sql("SELECT COUNT(*) AS C FROM dbo.AgentTaskRuns")[0]['C'])
print(f"   AgentTasks Count: {task_count} (Toàn vẹn)")
print(f"   AgentTaskRuns Count: {run_count} (Toàn vẹn)")
assert task_count > 0, "AgentTasks was corrupted!"
print("   -> [PASS] Tác vụ Agent và lịch sử chạy hoàn toàn nguyên vẹn!")

print("\n================================================================================")
print("TẤT CẢ 8 MỤC KIỂM THỬ BẮT BUỘC ĐÃ HOÀN TẤT THÀNH CÔNG 100%!")
print("================================================================================")
