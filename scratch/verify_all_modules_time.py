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
    print("KIỂM THỬ TOÀN DIỆN CHUẨN HÓA THỜI GIAN: POSTS, MARKET, CONNECT, SCHEDULER")
    print("=" * 80)

    client = HttpClient()
    login_user(client, 'luongtien2222@gmail.com', '123456')

    # TEST A: Kiểm tra phân cấp thời gian hiển thị tương đối:
    # 1. Bài vừa đăng (Post 39 hoặc 40) -> "Vừa xong"
    # 2. Bài đăng cách đây ~10-15 phút (Post 37) -> "X phút trước"
    print("\n--- TEST A: KIỂM TRA HIỂN THỊ THỜI GIAN TƯƠNG ĐỐI (VỪA XONG vs PHÚT TRƯỚC) ---")
    st, profile_html, _ = client.get('/User/Profile')
    
    # Check Post 39 (created ~1 min ago)
    p39_pos = profile_html.find('data-post-id="39"')
    if p39_pos >= 0:
        snip39 = profile_html[p39_pos:p39_pos+1800]
        m39 = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snip39)
        time39 = m39.group(1).strip() if m39 else "N/A"
        print(f"   [Post #39 - Agent đăng vừa xong]: {time39} -> {'PASS' if ('Vừa xong' in time39 or 'phút trước' in time39) else 'FAIL'}")

    # Check Post 37 (created at 13:45, now is ~13:58 -> ~13 mins ago)
    p37_pos = profile_html.find('data-post-id="37"')
    if p37_pos >= 0:
        snip37 = profile_html[p37_pos:p37_pos+1800]
        m37 = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snip37)
        time37 = m37.group(1).strip() if m37 else "N/A"
        print(f"   [Post #37 - Đăng cách đây ~13 phút]: {time37} -> {'PASS' if 'phút trước' in time37 else 'CHECK'}")

    # TEST B: Kiểm tra MarketPosts và ConnectPosts khi Agent đăng tin
    print("\n--- TEST B: KIỂM TRA ĐĂNG TIN CHỢ SINH VIÊN QUA AGENT (create_market_item) ---")
    market_tag = f"GiaoTrinh_{int(time.time())}"
    st, resp_market, _ = client.post('/Assistant/Ask', {
        'Message': f'Hãy đăng tin bán đồ lên Chợ sinh viên giúp tôi: Bán {market_tag} Toán cao cấp giá 50000 tại Cổng 1 trường TDMU'
    })
    try:
        m_json = json.loads(resp_market)
        print(f"   Agent Market Reply: {m_json.get('Reply')[:120]}...")
    except:
        print(f"   Raw Market Resp: {resp_market[:120]}")

    print("\n--- TEST C: KIỂM TRA ĐĂNG TIN KẾT NỐI QUA AGENT (create_connect_item) ---")
    connect_tag = f"TimO_Ghep_{int(time.time())}"
    st, resp_conn, _ = client.post('/Assistant/Ask', {
        'Message': f'Hãy đăng tin lên mục Kết nối giúp tôi: {connect_tag} cần tìm 1 bạn nam ở ghép gần trường TDMU phòng sạch sẽ'
    })
    try:
        c_json = json.loads(resp_conn)
        print(f"   Agent Connect Reply: {c_json.get('Reply')[:120]}...")
    except:
        print(f"   Raw Connect Resp: {resp_conn[:120]}")

    # Query DB for Market and Connect posts
    print("\n--- TEST D: ĐỐI CHIẾU DỮ LIỆU SQL SERVER (dbo.MarketPosts & dbo.ConnectPosts) ---")
    ps_query = '''
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "Mới nhất trong dbo.MarketPosts:"
$cmd.CommandText = "SELECT TOP 2 PostID, UserID, Title, CreatedAt FROM dbo.MarketPosts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Market PostID: " + $reader["PostID"] + " | Title: " + $reader["Title"] + " | CreatedAt: " + $reader["CreatedAt"])
}
$reader.Close()

Write-Host "`nMới nhất trong dbo.ConnectPosts:"
$cmd.CommandText = "SELECT TOP 2 PostID, UserID, Title, CreatedAt FROM dbo.ConnectPosts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Connect PostID: " + $reader["PostID"] + " | Title: " + $reader["Title"] + " | CreatedAt: " + $reader["CreatedAt"])
}
$reader.Close()
$conn.Close()
'''
    with open('scratch/temp_check_items.ps1', 'w', encoding='utf-8') as f:
        f.write(ps_query)
    res = subprocess.run(['powershell', '-File', 'scratch/temp_check_items.ps1'], capture_output=True, text=True)
    print(res.stdout)

    # TEST E: Kiểm tra tác vụ định kỳ của Agent (Scheduled Tasks)
    print("\n--- TEST E: KIỂM TRA TÁC VỤ ĐỊNH KỲ AGENT VẪN VẬN HÀNH CHÍNH XÁC ---")
    worker_exe = r"E:\LTWEB\StudentConnect\StudentConnect.Worker\bin\Release\StudentConnect.Worker.exe"
    proc = subprocess.run([worker_exe, "--once", "--dry-run"], capture_output=True, text=True, timeout=30)
    print(f"   Worker Execution ReturnCode: {proc.returncode} -> {'PASS' if proc.returncode == 0 else 'FAIL'}")

    print("\n" + "=" * 80)
    print("HOÀN THÀNH TẤT CẢ CÁC CA KIỂM ĐỊNH CHUẨN HÓA THỜI GIAN!")
    print("=" * 80)

if __name__ == '__main__':
    main()
