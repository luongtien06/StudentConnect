import urllib.request, urllib.parse, http.cookiejar, re, json, time, subprocess, sys

class HttpClient:
    def __init__(self, base_url="http://localhost:59174"):
        self.base_url = base_url
        self.cj = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj))

    def get(self, path):
        req = urllib.request.Request(self.base_url + path)
        try:
            resp = self.opener.open(req, timeout=30)
            return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
        except urllib.error.HTTPError as e:
            return e.code, e.read().decode('utf-8', errors='replace'), e.headers
        except Exception as ex:
            return 0, str(ex), {}

    def post(self, path, data=None, headers=None, allow_redirects=True):
        url = self.base_url + path
        req_headers = {}
        if isinstance(data, dict):
            encoded_data = urllib.parse.urlencode(data).encode('utf-8')
            req_headers["Content-Type"] = "application/x-www-form-urlencoded"
        elif isinstance(data, str):
            encoded_data = data.encode('utf-8')
            req_headers["Content-Type"] = "application/x-www-form-urlencoded"
        else:
            encoded_data = b""

        if headers:
            req_headers.update(headers)

        req = urllib.request.Request(url, data=encoded_data, headers=req_headers)
        if not allow_redirects:
            class NoRedirect(urllib.request.HTTPRedirectHandler):
                def redirect_request(self, req, fp, code, msg, headers, newurl):
                    return None
            op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj), NoRedirect)
            try:
                resp = op.open(req, timeout=45)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers
            except Exception as ex:
                return 0, str(ex), {}
        else:
            try:
                resp = self.opener.open(req, timeout=45)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers
            except Exception as ex:
                return 0, str(ex), {}

def extract_token(html):
    if not html: return ""
    m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
    if not m:
        m = re.search(r'value=["\']([^"\']+)["\']\s+name=["\']__RequestVerificationToken["\']', html)
    return m.group(1) if m else ""

def login_user(client, email, password):
    st, html, _ = client.get('/User/Login')
    token = extract_token(html)
    st, resp_html, hd = client.post('/User/Login', {
        '__RequestVerificationToken': token,
        'email': email,
        'password': password,
        'rememberMe': 'true'
    }, allow_redirects=False)
    loc = hd.get('Location', '')
    return (st == 302 and ('/' in loc or 'Home' in loc))

def main():
    print("=" * 80)
    print("VÒNG AUDIT KIỂM TRA CUỐI CÙNG LỖI MÚI GIỜ AI AGENT MR. T")
    print("=" * 80)

    # 1. Server Environment Check
    print("\n--- 1. XÁC MINH MÚI GIỜ MÁY CHỦ VÀ CSDL ---")
    ps_tz = '''
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT GETDATE() AS SqlLocal, GETUTCDATE() AS SqlUtc"
$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    Write-Host ("Cloud SQL Server GETDATE(): " + $reader["SqlLocal"])
    Write-Host ("Cloud SQL Server GETUTCDATE(): " + $reader["SqlUtc"])
}
$reader.Close()
$conn.Close()

Write-Host ("Local OS TimeZone Id: " + [TimeZoneInfo]::Local.Id)
Write-Host ("Local OS TimeZone BaseUtcOffset: " + [TimeZoneInfo]::Local.BaseUtcOffset.ToString())
Write-Host ("Local Machine DateTime.Now: " + [DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss"))
Write-Host ("Local Machine DateTime.UtcNow: " + [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
'''
    with open('scratch/temp_tz.ps1', 'w', encoding='utf-8') as f:
        f.write(ps_tz)
    res_tz = subprocess.run(['powershell', '-File', 'scratch/temp_tz.ps1'], capture_output=True, text=True)
    print(res_tz.stdout)

    client = HttpClient()
    ok = login_user(client, 'luongtien2222@gmail.com', '123456')
    if not ok:
        print("[FAIL] Đăng nhập User 1 thất bại!")
        sys.exit(1)

    # 2. Tạo bài viết kiểm thử qua AI Agent
    print("\n--- 2. TẠO BÀI VIẾT QUA AI AGENT MR. T ---")
    ts = int(time.time())
    agent_tag = f"FinalAgentAudit_{ts}"
    st, resp_agent, _ = client.post('/Assistant/Ask', {
        'Message': f'Hãy đăng giúp tôi bài viết lên bảng tin: [{agent_tag}] Bài viết kiểm định múi giờ cuối cùng của Mr. T lúc {time.strftime("%H:%M:%S")}'
    })
    try:
        a_data = json.loads(resp_agent)
        print(f"   Agent Success: {a_data.get('Success')}")
        print(f"   Agent Reply: {a_data.get('Reply')[:120]}...")
    except:
        print(f"   Raw Resp: {resp_agent[:120]}")

    # 3. Tạo bài viết kiểm thử qua giao diện thông thường
    print("\n--- 3. TẠO BÀI VIẾT QUA GIAO DIỆN THƯỜNG (/Post/Create) ---")
    st, home_html, _ = client.get('/Home/Index')
    tok_home = extract_token(home_html)
    ui_tag = f"FinalUIAudit_{ts}"
    st_ui, resp_ui, _ = client.post('/Post/Create', {
        '__RequestVerificationToken': tok_home,
        'content': f'[{ui_tag}] Bài viết kiểm định giao diện người dùng lúc {time.strftime("%H:%M:%S")}',
        'privacy': 'public'
    }, headers={'X-Requested-With': 'XMLHttpRequest'})
    print(f"   UI Post Resp: {resp_ui}")

    # 4. Đối chiếu trong SQL Server
    print("\n--- 4. ĐỐI CHIẾU THỜI GIAN TRONG SQL SERVER (dbo.Posts) ---")
    ps_query = '''
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 4 PostID, UserID, Content, CreatedAt FROM dbo.Posts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    $c = $reader["Content"].ToString()
    if ($c.Length -gt 45) { $c = $c.Substring(0, 45) }
    Write-Host ("PostID: " + $reader["PostID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | Content: " + $c)
}
$reader.Close()
$conn.Close()
'''
    with open('scratch/temp_query_final.ps1', 'w', encoding='utf-8') as f:
        f.write(ps_query)
    res_q = subprocess.run(['powershell', '-File', 'scratch/temp_query_final.ps1'], capture_output=True, text=True)
    print(res_q.stdout)

    # 5. Kiểm tra hiển thị trên giao diện người dùng (/User/Profile)
    print("\n--- 5. KIỂM TRA HIỂN THỊ THỜI GIAN TRÊN GIAO DIỆN (User Profile) ---")
    st, prof_html, _ = client.get('/User/Profile')
    
    # Check Agent Post
    idx_a = prof_html.find(agent_tag)
    if idx_a >= 0:
        snip_a = prof_html[max(0, idx_a - 1200):idx_a + 400]
        m_a = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snip_a)
        time_a = m_a.group(1).strip() if m_a else "N/A"
        print(f"   [Bài đăng AI Agent - {agent_tag}]:")
        print(f"   => Hiển thị: '{time_a}' -> {'PASS (Vừa xong)' if ('Vừa xong' in time_a or 'phút' in time_a) else 'FAIL'}")
    else:
        print("   [WARN] Không tìm thấy bài Agent trên Profile")

    # Check UI Post
    idx_u = prof_html.find(ui_tag)
    if idx_u >= 0:
        snip_u = prof_html[max(0, idx_u - 1200):idx_u + 400]
        m_u = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snip_u)
        time_u = m_u.group(1).strip() if m_u else "N/A"
        print(f"   [Bài đăng Web UI - {ui_tag}]:")
        print(f"   => Hiển thị: '{time_u}' -> {'PASS (Vừa xong)' if ('Vừa xong' in time_u or 'phút' in time_u) else 'FAIL'}")
    else:
        print("   [WARN] Không tìm thấy bài UI trên Profile")

    # 6. Kiểm tra Worker ngầm
    print("\n--- 6. XÁC MINH LỊCH CHẠY AGENT VẪN SỬ DỤNG UTC CHUẨN XÁC ---")
    worker_exe = r"E:\LTWEB\StudentConnect\StudentConnect.Worker\bin\Release\StudentConnect.Worker.exe"
    p = subprocess.run([worker_exe, "--once", "--dry-run"], capture_output=True, text=True, timeout=30)
    print(f"   Standalone Worker Exit Code: {p.returncode} -> {'PASS' if p.returncode == 0 else 'FAIL'}")

    print("\n" + "=" * 80)
    print("HOÀN TẤT VÒNG AUDIT KIỂM ĐỊNH MÚI GIỜ CUỐI CÙNG")
    print("=" * 80)

if __name__ == '__main__':
    main()
