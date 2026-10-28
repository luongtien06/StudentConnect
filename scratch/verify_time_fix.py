import urllib.request, urllib.parse, http.cookiejar, re, json, time, sys, subprocess

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
    print("BẮT ĐẦU KIỂM THỬ XÁC MINH KHẮC PHỤC LỖI THỜI GIAN ĐĂNG BÀI CỦA AI AGENT")
    print("=" * 80)

    client = HttpClient()
    ok = login_user(client, 'luongtien2222@gmail.com', '123456')
    print(f"1. Đăng nhập User 1 (luongtien2222@gmail.com): {'THÀNH CÔNG' if ok else 'THẤT BÀI'}")
    if not ok:
        sys.exit(1)

    # TEST 1: Yêu cầu AI Agent Mr. T đăng bài ngay lập tức
    print("\n2. Gửi yêu cầu cho AI Agent Mr. T đăng bài mới ngay lập tức...")
    unique_tag = f"TestFixTime_{int(time.time())}"
    st, resp_ask, _ = client.post('/Assistant/Ask', {
        'Message': f'Hãy đăng bài viết lên bảng tin giúp tôi với nội dung: [{unique_tag}] Chúc mừng ngày hội kỹ thuật TDMU 2026, mọi người cùng tham gia nhé!'
    })
    
    print(f"   HTTP Status: {st}")
    try:
        ask_data = json.loads(resp_ask)
        print(f"   Agent Success: {ask_data.get('Success')}")
        print(f"   Agent Reply: {ask_data.get('Reply')[:120]}...")
    except Exception as e:
        print(f"   Raw response: {resp_ask[:200]}")
        sys.exit(1)

    # Query DB to get the new post ID and its CreatedAt
    print("\n3. Kiểm tra bản ghi trong SQL Server dbo.Posts...")
    res = subprocess.run(['python', 'scratch/query_db.py'], capture_output=True, text=True)
    print(res.stdout)

    # TEST 2: Kiểm tra hiển thị trên trang Profile và Bảng tin
    print("\n4. Kiểm tra thời gian hiển thị thực tế trên giao diện Trang cá nhân (/User/Profile)...")
    st, profile_html, _ = client.get('/User/Profile')
    
    idx = profile_html.find(unique_tag)
    if idx >= 0:
        snippet = profile_html[max(0, idx - 1200):idx + 400]
        # Tìm post-meta-line hoặc post-time
        m_time = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snippet)
        if m_time:
            display_time = m_time.group(1).strip()
            print(f"   => KẾT QUẢ HIỂN THỊ THỜI GIAN TRÊN UI: '{display_time}'")
            if "giờ trước" in display_time:
                print("   [FAIL] Vẫn còn hiển thị số giờ trong quá khứ!")
            elif "Vừa xong" in display_time or "phút trước" in display_time or "giây trước" in display_time:
                print("   [PASS] Thời gian hiển thị chính xác tương ứng thời điểm vừa đăng ('Vừa xong')!")
        else:
            print("   Không tìm thấy post-meta-line, in snippet:\n", snippet[-400:])
    else:
        print(f"   [WARN] Không tìm thấy tag {unique_tag} trong HTML Profile!")

    # TEST 3: Kiểm tra đăng bài trực tiếp bằng giao diện thông thường (/Post/Create)
    print("\n5. Kiểm tra đăng bài thông thường qua giao diện người dùng (/Post/Create)...")
    st, home_html, _ = client.get('/Home/Index')
    token_home = extract_token(home_html)
    unique_ui_tag = f"TestUIPost_{int(time.time())}"
    st_post, resp_post, _ = client.post('/Post/Create', {
        '__RequestVerificationToken': token_home,
        'content': f'[{unique_ui_tag}] Bài viết đăng trực tiếp từ giao diện để đối chiếu thời gian',
        'privacy': 'public'
    }, headers={'X-Requested-With': 'XMLHttpRequest'})
    print(f"   Response /Post/Create: {resp_post}")

    # TEST 4: Kiểm tra lại Profile cho bài viết UI
    print("\n6. Kiểm tra thời gian hiển thị bài viết UI trên giao diện...")
    st, profile_html2, _ = client.get('/User/Profile')
    idx2 = profile_html2.find(unique_ui_tag)
    if idx2 >= 0:
        snippet2 = profile_html2[max(0, idx2 - 1200):idx2 + 400]
        m_time2 = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snippet2)
        if m_time2:
            display_time2 = m_time2.group(1).strip()
            print(f"   => KẾT QUẢ BÀI ĐĂNG UI TRÊN GIAO DIỆN: '{display_time2}'")
            if "Vừa xong" in display_time2 or "phút trước" in display_time2:
                print("   [PASS] Bài đăng UI hiển thị thời gian chính xác ('Vừa xong')!")
        else:
            print("   Không tìm thấy thẻ thời gian bài UI.")

if __name__ == '__main__':
    main()
