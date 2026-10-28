import urllib.request
import urllib.parse
import http.cookiejar
import re
import json
import time
import subprocess
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
        print(f"ADMIN CHAT TEST SUMMARY: TOTAL: {self.total} | PASSED: {self.passed} | FAILED: {self.failed}")
        print("====================================================================")
        return self.failed == 0

class HttpClient:
    def __init__(self):
        self.cj = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj))

    def get(self, path, allow_redirects=True, is_ajax=False):
        url = urllib.parse.urljoin(BASE_URL, path)
        headers = {"User-Agent": "ChatAudit/1.0"}
        if is_ajax:
            headers["X-Requested-With"] = "XMLHttpRequest"
        req = urllib.request.Request(url, headers=headers)
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

    def post(self, path, data, headers_extra=None, allow_redirects=True, is_ajax=True):
        url = urllib.parse.urljoin(BASE_URL, path)
        encoded_data = urllib.parse.urlencode(data, doseq=True).encode('utf-8')
        headers = {
            "User-Agent": "ChatAudit/1.0",
            "Content-Type": "application/x-www-form-urlencoded"
        }
        if is_ajax:
            headers["X-Requested-With"] = "XMLHttpRequest"
        if headers_extra:
            headers.update(headers_extra)
        req = urllib.request.Request(url, data=encoded_data, headers=headers)
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

def extract_token(html):
    m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
    if not m:
        m = re.search(r'value=["\']([^"\']+)["\']\s+name=["\']__RequestVerificationToken["\']', html)
    return m.group(1) if m else None

def admin_login(client, email, password):
    st, html, _ = client.get('/Admin/Auth/Login')
    token = extract_token(html)
    if not token:
        return False
    st, post_html, hd = client.post('/Admin/Auth/Login', {
        '__RequestVerificationToken': token,
        'username': email,
        'password': password
    }, allow_redirects=False, is_ajax=False)
    return st in (302, 200)

def user_login(client, email, password):
    st, html, _ = client.get('/User/Login')
    token = extract_token(html)
    if not token:
        return False
    st, post_html, hd = client.post('/User/Login', {
        '__RequestVerificationToken': token,
        'email': email,
        'password': password
    }, allow_redirects=False, is_ajax=False)
    return st in (302, 200)

def run_sql(query):
    cmd = [
        "sqlcmd", "-S", "localhost\\MSSQLSERVER01",
        "-d", "TDMUEcoSystem", "-U", "sa", "-P", "260126",
        "-h", "-1", "-W", "-Q", query
    ]
    res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    return res.stdout.strip()

def main():
    runner = TestRunner()
    print("====================================================================")
    print("TDMU ECOSYSTEM - ADMIN CHAT MODULE VERIFICATION SUITE")
    print("Timestamp: " + time.strftime("%Y-%m-%d %H:%M:%S"))
    print("Target: " + BASE_URL)
    print("====================================================================")

    # 1. Anonymous Access Security
    print("\n--- 1. ANONYMOUS & PERMISSION BOUNDARIES ---")
    anon = HttpClient()
    st, html, hd = anon.get('/Admin/AdminChat', allow_redirects=False)
    loc = hd.get('Location', '')
    runner.assert_test("Anonymous access to /Admin/AdminChat is blocked with 302 redirect", st == 302 and 'Login' in loc, f"Status: {st}, Loc: {loc}")

    st_post_anon, _, hd_post = anon.post('/Admin/AdminChat/AdminDeleteMultipleMessages', {'ids': [1, 2]}, allow_redirects=False, is_ajax=False)
    loc_post = hd_post.get('Location', '')
    runner.assert_test("Anonymous POST AdminDeleteMultipleMessages is blocked with 302 redirect", st_post_anon == 302 and 'Login' in loc_post, f"Status: {st_post_anon}")

    # 2. Regular Student Access
    student = HttpClient()
    user_login(student, 'testuser01@student.tdmu.edu.vn', '123456')
    st_stu, _, hd_stu = student.get('/Admin/AdminChat', allow_redirects=False)
    loc_stu = hd_stu.get('Location', '')
    runner.assert_test("Regular student access to /Admin/AdminChat is rejected", (st_stu == 302 and ('Admin' in loc_stu or 'Login' in loc_stu or loc_stu == '/')) or st_stu in (401, 403), f"Status: {st_stu}")

    # 3. Cross-SubAdmin (AdminMarket)
    market_admin = HttpClient()
    admin_login(market_admin, 'adminmarket@tdmu.edu.vn', '123456')
    st_mkt, _, hd_mkt = market_admin.get('/Admin/AdminChat', allow_redirects=False)
    loc_mkt = hd_mkt.get('Location', '')
    runner.assert_test("AdminMarket cross-access to /Admin/AdminChat is rejected", (st_mkt == 302 and ('AdminHome' in loc_mkt or loc_mkt.endswith('/Admin') or 'Login' in loc_mkt)) or st_mkt in (401, 403), f"Status: {st_mkt}, Loc: {loc_mkt}")

    # 4. AdminChat Access
    chat_admin = HttpClient()
    logged_in = admin_login(chat_admin, '2424802010453@student.tdmu.edu.vn', '123456')
    runner.assert_test("AdminChat (2424802010453) successfully logs in via /Admin/Auth/Login", logged_in)

    st_admin_chat, chat_html, _ = chat_admin.get('/Admin/AdminChat')
    runner.assert_test("AdminChat accesses /Admin/AdminChat with HTTP 200", st_admin_chat == 200, f"Status: {st_admin_chat}")
    runner.assert_test("AdminChat view contains msgSelectBar element", 'id="msgSelectBar"' in chat_html)
    runner.assert_test("AdminChat view contains msgSelectAll checkbox", 'id="msgSelectAll"' in chat_html)
    runner.assert_test("AdminChat view contains messagesPagination element", 'id="messagesPagination"' in chat_html)
    runner.assert_test("AdminChat view includes pagination CSS styles (.pagination-wrap / .page-btn)", '.pagination-wrap' in chat_html and '.page-btn' in chat_html)

    # Extract CSRF token from AdminChat page
    chat_token = extract_token(chat_html)
    runner.assert_test("AdminChat page supplies valid Antiforgery token", chat_token is not None and len(chat_token) > 20)

    # 5. CSRF Protection Check (tested with separate client so cookie is not contaminated)
    print("\n--- 2. CSRF & INPUT VALIDATION SECURITY ---")
    csrf_tester = HttpClient()
    admin_login(csrf_tester, '2424802010453@student.tdmu.edu.vn', '123456')
    st_no_csrf, html_no_csrf, _ = csrf_tester.post('/Admin/AdminChat/AdminDeleteMultipleMessages', {'ids': [1]}, is_ajax=True)
    runner.assert_test("Request without CSRF token is rejected by backend (500 or csrfExpired JSON)", 
                       st_no_csrf == 500 or 'csrfExpired' in html_no_csrf or 'anti-forgery' in html_no_csrf.lower(), 
                       f"Status: {st_no_csrf}, Body: {html_no_csrf[:80]}")

    # Empty list input validation
    st_empty, res_empty_raw, _ = chat_admin.post('/Admin/AdminChat/AdminDeleteMultipleMessages', {
        '__RequestVerificationToken': chat_token,
        'ids': []
    }, is_ajax=True)
    res_empty = {}
    try: res_empty = json.loads(res_empty_raw)
    except: pass
    runner.assert_test("Empty ID list returns success=false with informative message", 
                       st_empty == 200 and res_empty.get('success') is False and "chọn" in res_empty.get('message', ''),
                       f"Response: {res_empty_raw}")

    # Non-existent ID validation
    st_nonexist, res_nonexist_raw, _ = chat_admin.post('/Admin/AdminChat/AdminDeleteMultipleMessages', {
        '__RequestVerificationToken': chat_token,
        'ids': [99999999]
    }, is_ajax=True)
    res_nonexist = {}
    try: res_nonexist = json.loads(res_nonexist_raw)
    except: pass
    runner.assert_test("Non-existent ID returns success=false safely without crashing",
                       st_nonexist == 200 and res_nonexist.get('success') is False and "Không tìm thấy" in res_nonexist.get('message', ''),
                       f"Response: {res_nonexist_raw}")

    # 6. Pagination API Check
    print("\n--- 3. PAGINATION API & NAVIGATION DATA ---")
    st_page1, res_page1_raw, _ = chat_admin.get('/Admin/AdminChat/FilterMessages?page=1&pageSize=30', is_ajax=True)
    data_page1 = {}
    try: data_page1 = json.loads(res_page1_raw)
    except: pass
    runner.assert_test("FilterMessages returns valid JSON structure with pagination metadata",
                       st_page1 == 200 and data_page1.get('success') is True and 'totalCount' in data_page1 and 'totalPages' in data_page1 and 'page' in data_page1,
                       f"Status: {st_page1}")
    total_count = data_page1.get('totalCount', 0)
    total_pages = data_page1.get('totalPages', 1)
    runner.assert_test(f"FilterMessages returns totalCount ({total_count}) and totalPages ({total_pages})", total_count >= 0 and total_pages >= 1)

    # 7. Real Bulk Message Deletion & Database Consistency Test
    print("\n--- 4. BULK DELETE EXECUTION & DATABASE CONSISTENCY ---")
    # Clean up any leftover test messages first
    run_sql("DELETE FROM ChatMessages WHERE Content LIKE '%AUDIT_BULK_DEL_%';")

    # Insert 3 fresh test messages into room 1
    insert_sql = """
    INSERT INTO ChatMessages (RoomID, SenderID, Content, SentAt)
    VALUES 
    (1, 1022, N'AUDIT_BULK_DEL_1 Tin nhắn thử nghiệm 1', GETDATE()),
    (1, 1022, N'AUDIT_BULK_DEL_2 Tin nhắn thử nghiệm 2', GETDATE()),
    (1, 1022, N'AUDIT_BULK_DEL_3 Tin nhắn thử nghiệm 3', GETDATE());
    SELECT MessageID FROM ChatMessages WHERE Content LIKE '%AUDIT_BULK_DEL_%' ORDER BY MessageID;
    """
    sql_output = run_sql(insert_sql)
    test_ids = [int(line.strip()) for line in sql_output.splitlines() if line.strip().isdigit()]
    print(f"Created 3 test messages with IDs: {test_ids}")
    runner.assert_test("3 test messages successfully inserted into ChatMessages", len(test_ids) == 3)

    if len(test_ids) == 3:
        # Call AdminDeleteMultipleMessages with the 3 test IDs
        del_st, del_res_raw, _ = chat_admin.post('/Admin/AdminChat/AdminDeleteMultipleMessages', {
            '__RequestVerificationToken': chat_token,
            'ids': test_ids,
            'reason': 'Kiểm thử tự động xóa hàng loạt'
        }, is_ajax=True)
        del_res = {}
        try: del_res = json.loads(del_res_raw)
        except: pass

        runner.assert_test("AdminDeleteMultipleMessages returns success=true and count=3",
                           del_st == 200 and del_res.get('success') is True and del_res.get('count') == 3,
                           f"Status: {del_st}, Response: {del_res_raw}")

        # Check DB to verify messages are permanently deleted
        check_sql = f"SELECT COUNT(*) FROM ChatMessages WHERE MessageID IN ({','.join(map(str, test_ids))});"
        remaining_raw = run_sql(check_sql)
        digits = [line.strip() for line in remaining_raw.splitlines() if line.strip().isdigit()]
        remaining_count = int(digits[0]) if digits else -1
        runner.assert_test("All 3 test messages are permanently removed from database", remaining_count == 0, f"Remaining: {remaining_count}")

    # 8. SuperAdmin Omnipotence Check
    print("\n--- 5. SUPERADMIN PRIVILEGES ---")
    super_admin = HttpClient()
    admin_login(super_admin, 'luongtien2222@gmail.com', '123456')
    st_sa, sa_html, _ = super_admin.get('/Admin/AdminChat')
    runner.assert_test("SuperAdmin also has full access to /Admin/AdminChat", st_sa == 200)

    success = runner.summary()
    if not success:
        sys.exit(1)

if __name__ == '__main__':
    main()
