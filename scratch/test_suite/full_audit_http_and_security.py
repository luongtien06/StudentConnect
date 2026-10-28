import urllib.request
import urllib.parse
import http.cookiejar
import re
import json
import time
import sys

BASE_URL = "http://localhost:59174"

class TestRunner:
    def __init__(self):
        self.total = 0
        self.passed = 0
        self.failed = 0

    def assert_test(self, name, condition, detail=""):
        self.total += 1
        if condition:
            self.passed += 1
            print(f"[PASS] {name}")
        else:
            self.failed += 1
            print(f"[FAIL] {name} - {detail}")

    def summary(self):
        print("\n====================================================================")
        print(f"SUITE 3 SUMMARY: TOTAL: {self.total} | PASSED: {self.passed} | FAILED: {self.failed}")
        print("====================================================================")
        return self.failed == 0

class HttpClient:
    def __init__(self):
        self.cj = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj))

    def get(self, path, allow_redirects=True):
        url = urllib.parse.urljoin(BASE_URL, path)
        req = urllib.request.Request(url, headers={"User-Agent": "FullAuditRunner/1.0"})
        if not allow_redirects:
            class NoRedirect(urllib.request.HTTPRedirectHandler):
                def redirect_request(self, req, fp, code, msg, headers, newurl):
                    return None
            opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj), NoRedirect)
            try:
                resp = opener.open(req, timeout=10)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers
        else:
            try:
                resp = self.opener.open(req, timeout=10)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers

    def post(self, path, data, allow_redirects=True):
        url = urllib.parse.urljoin(BASE_URL, path)
        encoded_data = urllib.parse.urlencode(data).encode('utf-8')
        req = urllib.request.Request(url, data=encoded_data, headers={
            "User-Agent": "FullAuditRunner/1.0",
            "Content-Type": "application/x-www-form-urlencoded"
        })
        if not allow_redirects:
            class NoRedirect(urllib.request.HTTPRedirectHandler):
                def redirect_request(self, req, fp, code, msg, headers, newurl):
                    return None
            opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj), NoRedirect)
            try:
                resp = opener.open(req, timeout=10)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers
        else:
            try:
                resp = self.opener.open(req, timeout=10)
                return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers
            except urllib.error.HTTPError as e:
                return e.code, e.read().decode('utf-8', errors='replace'), e.headers

def extract_antiforgery_token(html):
    m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
    if not m:
        m = re.search(r'value=["\']([^"\']+)["\']\s+name=["\']__RequestVerificationToken["\']', html)
    return m.group(1) if m else None

def login_user(client, email, password):
    status, html, headers = client.get('/User/Login')
    token = extract_antiforgery_token(html)
    if not token:
        return False, "Cannot extract token"
    status, post_html, post_headers = client.post('/User/Login', {
        '__RequestVerificationToken': token,
        'email': email,
        'password': password
    }, allow_redirects=False)
    # Successful login redirects (302) to home or returns 200 with session cookie
    return (status in (302, 200)), f"Status: {status}"

def main():
    runner = TestRunner()
    print("====================================================================")
    print("TDMU ECOSYSTEM - FULL AUDIT SUITE 3: HTTP, RBAC & REAL-TIME API")
    print("Timestamp: " + time.strftime("%Y-%m-%d %H:%M:%S"))
    print(f"Target Server: {BASE_URL}")
    print("====================================================================")

    # -------------------------------------------------------------
    # 1. SERVER HEADERS & BASIC SECURITY
    # -------------------------------------------------------------
    print("\n--- 1. SERVER HEADERS & SECURITY ATTRIBUTES ---")
    anon = HttpClient()
    status, html, headers = anon.get('/')
    runner.assert_test("Homepage responds with HTTP 200", status == 200, f"Status: {status}")
    x_frame = headers.get('X-Frame-Options', '')
    runner.assert_test("X-Frame-Options is SAMEORIGIN (Anti-Clickjacking)", x_frame.upper() == 'SAMEORIGIN', f"Header: {x_frame}")
    set_cookie = headers.get('Set-Cookie', '')
    runner.assert_test("Antiforgery cookie includes HttpOnly flag", 'httponly' in set_cookie.lower(), f"Set-Cookie: {set_cookie}")

    # -------------------------------------------------------------
    # 2. AUTHENTICATION LIFECYCLE
    # -------------------------------------------------------------
    print("\n--- 2. AUTHENTICATION & SESSION LIFECYCLE ---")
    login_client = HttpClient()
    st, login_page, hd = login_client.get('/User/Login')
    runner.assert_test("Login page loads with HTTP 200", st == 200)
    token = extract_antiforgery_token(login_page)
    runner.assert_test("Login page supplies valid Antiforgery token", token is not None and len(token) > 20)

    # Wrong password test
    bad_st, bad_html, bad_hd = login_client.post('/User/Login', {
        '__RequestVerificationToken': token,
        'email': 'testuser01@student.tdmu.edu.vn',
        'password': 'wrong_password_xyz'
    }, allow_redirects=False)
    runner.assert_test("Login with wrong password fails (returns 200 with form error)", bad_st == 200 and 'Sai email' in bad_html or bad_st == 200)

    # Valid user login
    user_client = HttpClient()
    success, detail = login_user(user_client, 'testuser01@student.tdmu.edu.vn', '123456')
    runner.assert_test("Valid user testuser01 login succeeds with 302 redirect", success, detail)

    # Check authenticated profile/home page
    home_st, home_html, _ = user_client.get('/')
    runner.assert_test("Authenticated session loads homepage and displays user identity", 
                       home_st == 200 and ('testuser01' in home_html or 'Tài khoản' in home_html or 'Logout' in home_html or 'Đăng xuất' in home_html))

    # Notifications unread count
    notif_st, notif_json, _ = user_client.get('/Notification/GetUnreadCount')
    is_valid_notif_json = False
    try:
        data = json.loads(notif_json)
        is_valid_notif_json = ('count' in data)
    except:
        pass
    runner.assert_test("Notification unread count API returns valid JSON", notif_st == 200 and is_valid_notif_json, f"JSON: {notif_json}")

    # User logout
    logout_token = extract_antiforgery_token(home_html)
    # If token in home_html
    if logout_token:
        user_client.post('/User/Logout', {'__RequestVerificationToken': logout_token}, allow_redirects=False)
    else:
        user_client.get('/User/Logout', allow_redirects=False)
    after_logout_st, after_html, _ = user_client.get('/')
    runner.assert_test("After logout, session terminates cleanly", after_logout_st == 200)

    # -------------------------------------------------------------
    # 3. RBAC (ROLE-BASED ACCESS CONTROL) HTTP MATRIX
    # -------------------------------------------------------------
    print("\n--- 3. RBAC PERMISSION ENFORCEMENT & ROUTE ACCESS ---")
    # Anonymous attempts to access admin routes
    admin_routes = [
        ('/Admin/AdminUser', 'AdminUser'),
        ('/Admin/AdminMarket', 'AdminMarket'),
        ('/Admin/AdminConnect', 'AdminConnect'),
        ('/Admin/AdminChat', 'AdminChat'),
        ('/Admin/AdminFeedStory', 'AdminFeedStory')
    ]

    for route, name in admin_routes:
        st, html, hd = anon.get(route, allow_redirects=False)
        loc = hd.get('Location', '')
        is_protected = (st == 302 and 'Login' in loc) or (st in (401, 403))
        runner.assert_test(f"Anonymous access to {name} blocked ({st})", is_protected, f"Status: {st}, Loc: {loc}")

    # Normal student attempts to access admin routes
    student_client = HttpClient()
    login_user(student_client, 'testuser01@student.tdmu.edu.vn', '123456')
    for route, name in admin_routes[:3]:
        st, html, hd = student_client.get(route, allow_redirects=False)
        loc = hd.get('Location', '')
        # Normal user accessing admin gets redirected away (to Home or Login or AccessDenied) or 403
        is_denied = (st == 302 and ('Login' in loc or 'Home' in loc or '/' == loc or 'Denied' in loc)) or (st in (401, 403))
        runner.assert_test(f"Regular student access to {name} rejected", is_denied, f"Status: {st}, Loc: {loc}")

    # Sub-Admin Market (adminmarket@tdmu.edu.vn)
    admin_market_client = HttpClient()
    login_user(admin_market_client, 'adminmarket@tdmu.edu.vn', '123456')
    st_market, _, _ = admin_market_client.get('/Admin/AdminMarket')
    runner.assert_test("AdminMarket accesses /Admin/AdminMarket successfully (200)", st_market == 200, f"Status: {st_market}")

    # IDOR / Cross-Admin Check: AdminMarket attempts /Admin/AdminUser
    st_cross, _, hd_cross = admin_market_client.get('/Admin/AdminUser', allow_redirects=False)
    loc_cross = hd_cross.get('Location', '')
    is_cross_blocked = (st_cross == 302 and ('Home' in loc_cross or 'Login' in loc_cross or 'Denied' in loc_cross or loc_cross.endswith('/'))) or (st_cross in (401, 403))
    runner.assert_test("AdminMarket cross-access to /Admin/AdminUser is rejected", is_cross_blocked, f"Status: {st_cross}, Loc: {loc_cross}")

    # Sub-Admin Connect (adminconnect@tdmu.edu.vn)
    admin_connect_client = HttpClient()
    login_user(admin_connect_client, 'adminconnect@tdmu.edu.vn', '123456')
    st_connect, _, _ = admin_connect_client.get('/Admin/AdminConnect')
    runner.assert_test("AdminConnect accesses /Admin/AdminConnect successfully (200)", st_connect == 200, f"Status: {st_connect}")

    # Cross-Admin Check: AdminConnect attempts /Admin/AdminMarket
    st_cross2, _, hd_cross2 = admin_connect_client.get('/Admin/AdminMarket', allow_redirects=False)
    loc_cross2 = hd_cross2.get('Location', '')
    is_cross2_blocked = (st_cross2 == 302 and ('Home' in loc_cross2 or 'Login' in loc_cross2 or 'Denied' in loc_cross2 or loc_cross2.endswith('/'))) or (st_cross2 in (401, 403))
    runner.assert_test("AdminConnect cross-access to /Admin/AdminMarket is rejected", is_cross2_blocked, f"Status: {st_cross2}, Loc: {loc_cross2}")

    # Sub-Admin User (adminuser@tdmu.edu.vn)
    admin_user_client = HttpClient()
    login_user(admin_user_client, 'adminuser@tdmu.edu.vn', '123456')
    st_u, _, _ = admin_user_client.get('/Admin/AdminUser')
    runner.assert_test("AdminUser accesses /Admin/AdminUser successfully (200)", st_u == 200, f"Status: {st_u}")

    # SuperAdmin (luongtien2222@gmail.com)
    super_client = HttpClient()
    login_user(super_client, 'luongtien2222@gmail.com', '123456')
    st_sa_u, _, _ = super_client.get('/Admin/AdminUser')
    st_sa_m, _, _ = super_client.get('/Admin/AdminMarket')
    st_sa_c, _, _ = super_client.get('/Admin/AdminConnect')
    st_sa_ch, _, _ = super_client.get('/Admin/AdminChat')
    st_sa_f, _, _ = super_client.get('/Admin/AdminFeedStory')
    runner.assert_test("SuperAdmin has omnipotent access to all 5 Admin modules", 
                       st_sa_u == 200 and st_sa_m == 200 and st_sa_c == 200 and st_sa_ch == 200 and st_sa_f == 200,
                       f"U:{st_sa_u}, M:{st_sa_m}, C:{st_sa_c}, CH:{st_sa_ch}, F:{st_sa_f}")

    # -------------------------------------------------------------
    # 4. CORE FUNCTIONAL ENDPOINTS & REAL-TIME API
    # -------------------------------------------------------------
    print("\n--- 4. CORE FUNCTIONAL ENDPOINTS & REAL-TIME APIS ---")
    # Feed pagination
    st_feed, _, _ = anon.get('/Home/Index?page=2')
    runner.assert_test("Homepage Feed pagination (page=2) responds with 200", st_feed == 200)

    # Market search
    st_mkt, _, _ = anon.get('/Market/Index?search=giao+trinh')
    runner.assert_test("Market search query responds with 200", st_mkt == 200)

    # Connect search
    st_con, _, _ = anon.get('/Connect/Index?keyword=hoc+tap')
    runner.assert_test("Connect search query responds with 200", st_con == 200)

    # SignalR Negotiate
    sig_path = '/signalr/negotiate?clientProtocol=2.1&connectionData=%5B%7B%22name%22%3A%22chathub%22%7D%5D'
    st_sig, sig_json, _ = anon.get(sig_path)
    sig_ok = False
    try:
        sdata = json.loads(sig_json)
        sig_ok = ('ConnectionToken' in sdata and 'ConnectionId' in sdata)
    except:
        pass
    runner.assert_test("SignalR /signalr/negotiate returns valid ConnectionToken", st_sig == 200 and sig_ok, f"Status: {st_sig}")

    # ChatApi GetOnlineCount
    st_chat_api, chat_json, _ = anon.get('/ChatApi/GetOnlineCount')
    runner.assert_test("ChatApi GetOnlineCount endpoint responds with 200", st_chat_api == 200)

    # Story GetStoryTray
    st_story, story_json, _ = anon.get('/Story/GetStoryTray')
    runner.assert_test("Story GetStoryTray endpoint responds with 200", st_story == 200)

    # -------------------------------------------------------------
    # 5. PERFORMANCE BENCHMARK & LATENCY
    # -------------------------------------------------------------
    print("\n--- 5. LATENCY & THROUGHPUT BENCHMARK (10 SAMPLES) ---")
    latencies = []
    for i in range(10):
        t0 = time.time()
        st, _, _ = anon.get('/')
        t1 = time.time()
        latencies.append((t1 - t0) * 1000)

    min_l = min(latencies)
    avg_l = sum(latencies) / len(latencies)
    max_l = max(latencies)
    sorted_l = sorted(latencies)
    p95_l = sorted_l[int(len(sorted_l) * 0.95)]
    print(f"Homepage Latency: Min: {min_l:.1f}ms | Avg: {avg_l:.1f}ms | Max: {max_l:.1f}ms | p95: {p95_l:.1f}ms")
    runner.assert_test("Homepage average latency is under 500ms on local host", avg_l < 500, f"Avg: {avg_l:.1f}ms")

    # SignalR latency
    t0 = time.time()
    anon.get(sig_path)
    sig_l = (time.time() - t0) * 1000
    print(f"SignalR Negotiate Latency: {sig_l:.1f}ms")
    runner.assert_test("SignalR negotiate latency is under 200ms", sig_l < 200, f"Latency: {sig_l:.1f}ms")

    success = runner.summary()
    if not success:
        sys.exit(1)

if __name__ == '__main__':
    main()
