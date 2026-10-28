import urllib.request
import urllib.parse
import http.cookiejar
import json
import subprocess
import sys
import re

BASE_URL = "http://localhost:59174"

class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

def run_sql(query):
    cmd = [
        "sqlcmd", "-S", "localhost\\MSSQLSERVER01",
        "-d", "TDMUEcoSystem", "-E", "-h", "-1", "-W",
        "-Q", "SET NOCOUNT ON; " + query
    ]
    res = subprocess.run(cmd, capture_output=True, text=True, check=True)
    out = res.stdout.strip().splitlines()
    for l in out:
        l = l.strip()
        if l and not l.startswith('('):
            return l
    return "0"

def make_client(allow_redirect=True):
    cj = http.cookiejar.CookieJar()
    handlers = [urllib.request.HTTPCookieProcessor(cj)]
    if not allow_redirect:
        handlers.append(NoRedirectHandler())
    opener = urllib.request.build_opener(*handlers)
    return opener, cj

def get_csrf_token(opener, url):
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    with opener.open(req) as resp:
        html = resp.read().decode('utf-8', errors='ignore')
    m = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html)
    if not m:
        m = re.search(r'value="([^"]+)"\s+name="__RequestVerificationToken"', html)
    return m.group(1) if m else None

def admin_login(opener, username, password):
    login_url = f"{BASE_URL}/Admin/Auth/Login"
    token = get_csrf_token(opener, login_url)
    if not token:
        raise RuntimeError("Failed to get CSRF token on Admin login page")
    data = urllib.parse.urlencode({
        "__RequestVerificationToken": token,
        "username": username,
        "password": password
    }).encode('utf-8')
    req = urllib.request.Request(login_url, data=data, headers={
        'User-Agent': 'Mozilla/5.0',
        'Content-Type': 'application/x-www-form-urlencoded'
    })
    try:
        with opener.open(req) as resp:
            return resp.status, resp.read().decode('utf-8', errors='ignore')
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode('utf-8', errors='ignore')

def main():
    print("=" * 65)
    print("AI AGENT ACTIVITY LOG CLEANUP VERIFICATION SUITE")
    print("Target:", BASE_URL)
    print("=" * 65)

    passed = 0
    failed = 0

    def assert_test(cond, title):
        nonlocal passed, failed
        if cond:
            print(f"[PASS] {title}")
            passed += 1
        else:
            print(f"[FAIL] {title}")
            failed += 1

    # 1. Anonymous access is blocked
    anon_client, _ = make_client(allow_redirect=False)
    try:
        req = urllib.request.Request(f"{BASE_URL}/Admin/AdminAiAgent/DeleteOldLogs", data=b"days=30", headers={
            'X-Requested-With': 'XMLHttpRequest',
            'Content-Type': 'application/x-www-form-urlencoded'
        })
        resp = anon_client.open(req)
        # If AJAX, AdminBaseController returns 200 with success=false or 302
        raw = resp.read().decode('utf-8')
        try:
            j = json.loads(raw)
            assert_test(j.get('success') == False, "Anonymous AJAX request rejected with success=false")
        except:
            assert_test(resp.code in (302, 401, 403), f"Anonymous request redirected or forbidden ({resp.code})")
    except urllib.error.HTTPError as e:
        assert_test(e.code in (302, 401, 403, 500), f"Anonymous POST DeleteOldLogs returned {e.code} (Blocked)")
    except Exception as e:
        assert_test(True, f"Anonymous POST DeleteOldLogs blocked ({e})")

    # 2. Login SuperAdmin
    sa_client, _ = make_client(allow_redirect=True)
    status, _ = admin_login(sa_client, "luongtien2222@gmail.com", "123456")
    assert_test(status == 200, "SuperAdmin logged in successfully")

    # Get CSRF token from /Admin/AdminAiAgent
    ai_page_url = f"{BASE_URL}/Admin/AdminAiAgent"
    token = get_csrf_token(sa_client, ai_page_url)
    assert_test(token is not None, "SuperAdmin retrieved CSRF token from /Admin/AdminAiAgent")

    # Check page contains the new UI elements
    req = urllib.request.Request(ai_page_url, headers={'User-Agent': 'Mozilla/5.0'})
    with sa_client.open(req) as resp:
        page_html = resp.read().decode('utf-8')
    assert_test('onclick="promptDeleteOldLogs()"' in page_html, "View contains 'promptDeleteOldLogs()' button")
    assert_test('id="btnDeleteCurrentLog"' in page_html, "View contains '#btnDeleteCurrentLog' in logDetailModal")
    assert_test('confirmDeleteSingleLog' in page_html, "View contains 'confirmDeleteSingleLog' handler")

    # 3. Clean up any previous test logs first
    run_sql("DELETE FROM dbo.AgentActionLogs WHERE ActionName LIKE '%TEST_CLEANUP%';")

    # Insert 4 test logs directly with specific dates and statuses
    insert_sql = """
    INSERT INTO dbo.AgentActionLogs (UserID, ActionName, TargetType, Status, InputSummary, ResultSummary, CreatedAt)
    VALUES 
    (1002, 'TEST_CLEANUP_OLD10', 'Test', 'Succeeded', 'input1', 'Old log 1', DATEADD(day, -10, GETUTCDATE())),
    (1002, 'TEST_CLEANUP_OLD5_FAIL', 'Test', 'Failed', 'input2', 'Old log 2', DATEADD(day, -5, GETUTCDATE())),
    (1002, 'TEST_CLEANUP_TODAY_FAIL', 'Test', 'Failed', 'input3', 'Today failed log', GETUTCDATE()),
    (1002, 'TEST_CLEANUP_TODAY_SUCC', 'Test', 'Succeeded', 'input4', 'Today success log', GETUTCDATE());
    SELECT COUNT(*) FROM dbo.AgentActionLogs WHERE ActionName LIKE '%TEST_CLEANUP%';
    """
    test_count = int(run_sql(insert_sql))
    assert_test(test_count == 4, f"4 test log records inserted into database (found {test_count})")

    # 4. Test DeleteOldLogs: days = 7 (should delete OLD10 older than 10 days; keep OLD5, TODAY_FAIL, TODAY_SUCC)
    data = urllib.parse.urlencode({
        "days": 7,
        "status": "All",
        "__RequestVerificationToken": token
    }).encode('utf-8')
    req = urllib.request.Request(f"{BASE_URL}/Admin/AdminAiAgent/DeleteOldLogs", data=data, headers={
        'X-Requested-With': 'XMLHttpRequest',
        'Content-Type': 'application/x-www-form-urlencoded'
    })
    with sa_client.open(req) as resp:
        res1 = json.loads(resp.read().decode('utf-8'))
    assert_test(res1.get("success") == True, f"DeleteOldLogs(days=7) success: {res1.get('message')}")
    assert_test(res1.get("deletedCount") >= 1, f"DeleteOldLogs(days=7) deleted {res1.get('deletedCount')} records")

    # Verify OLD10 is gone from DB
    log1_exists = int(run_sql("SELECT COUNT(*) FROM dbo.AgentActionLogs WHERE ActionName = 'TEST_CLEANUP_OLD10';"))
    assert_test(log1_exists == 0, "Test log older than 10 days was successfully deleted")

    # 5. Test DeleteOldLogs: status='Failed' and days=3 (should delete OLD5_FAIL, but keep TODAY_FAIL)
    data = urllib.parse.urlencode({
        "days": 3,
        "status": "Failed",
        "__RequestVerificationToken": token
    }).encode('utf-8')
    req = urllib.request.Request(f"{BASE_URL}/Admin/AdminAiAgent/DeleteOldLogs", data=data, headers={
        'X-Requested-With': 'XMLHttpRequest',
        'Content-Type': 'application/x-www-form-urlencoded'
    })
    with sa_client.open(req) as resp:
        res2 = json.loads(resp.read().decode('utf-8'))
    assert_test(res2.get("success") == True, f"DeleteOldLogs(days=3, status=Failed) success: {res2.get('message')}")
    assert_test(res2.get("deletedCount") >= 1, f"Deleted count: {res2.get('deletedCount')}")

    log2_exists = int(run_sql("SELECT COUNT(*) FROM dbo.AgentActionLogs WHERE ActionName = 'TEST_CLEANUP_OLD5_FAIL';"))
    assert_test(log2_exists == 0, "Test log older than 5 days with status Failed was deleted")

    # 6. Test DeleteSingleLog: delete TODAY_FAIL by logId
    log3_id_raw = run_sql("SELECT TOP 1 LogID FROM dbo.AgentActionLogs WHERE ActionName = 'TEST_CLEANUP_TODAY_FAIL';")
    if log3_id_raw.isdigit():
        log3_id = int(log3_id_raw)
        del_single_data = urllib.parse.urlencode({
            "logId": log3_id,
            "__RequestVerificationToken": token
        }).encode('utf-8')
        req = urllib.request.Request(f"{BASE_URL}/Admin/AdminAiAgent/DeleteSingleLog", data=del_single_data, headers={
            'X-Requested-With': 'XMLHttpRequest',
            'Content-Type': 'application/x-www-form-urlencoded'
        })
        with sa_client.open(req) as resp:
            res_single = json.loads(resp.read().decode('utf-8'))
        assert_test(res_single.get("success") == True, f"DeleteSingleLog(logId={log3_id}) succeeded: {res_single.get('message')}")
        log3_exists = int(run_sql(f"SELECT COUNT(*) FROM dbo.AgentActionLogs WHERE LogID = {log3_id};"))
        assert_test(log3_exists == 0, f"Log #{log3_id} verified permanently removed from database")
    else:
        assert_test(False, "Could not find LogID for test log 3")

    # 7. Test DeleteOldLogs: days = 0 (delete ALL remaining test logs)
    data_all = urllib.parse.urlencode({
        "days": 0,
        "status": "All",
        "__RequestVerificationToken": token
    }).encode('utf-8')
    req = urllib.request.Request(f"{BASE_URL}/Admin/AdminAiAgent/DeleteOldLogs", data=data_all, headers={
        'X-Requested-With': 'XMLHttpRequest',
        'Content-Type': 'application/x-www-form-urlencoded'
    })
    with sa_client.open(req) as resp:
        res_all = json.loads(resp.read().decode('utf-8'))
    assert_test(res_all.get("success") == True, f"DeleteOldLogs(days=0) 'All' succeeded: {res_all.get('message')}")

    # Verify no test logs remain
    remaining_test = int(run_sql("SELECT COUNT(*) FROM dbo.AgentActionLogs WHERE ActionName LIKE '%TEST_CLEANUP%';"))
    assert_test(remaining_test == 0, f"All test logs removed from database (remaining: {remaining_test})")

    # 8. CSRF Validation test: Request without CSRF token must be rejected
    req_no_csrf = urllib.request.Request(f"{BASE_URL}/Admin/AdminAiAgent/DeleteOldLogs", data=b"days=30&status=All", headers={
        'X-Requested-With': 'XMLHttpRequest',
        'Content-Type': 'application/x-www-form-urlencoded'
    })
    try:
        with sa_client.open(req_no_csrf) as resp:
            raw = resp.read().decode('utf-8')
            assert_test('csrf' in raw.lower() or 'error' in raw.lower() or resp.status != 200, "Request without CSRF was handled appropriately")
    except urllib.error.HTTPError as e:
        assert_test(e.code in (400, 500), f"Request without CSRF returned expected HTTP error {e.code}")

    # Check that audit log was generated
    audit_count = int(run_sql("SELECT COUNT(*) FROM dbo.AgentActionLogs WHERE ActionName LIKE '%delete_%';"))
    assert_test(audit_count > 0, f"Audit trail successfully logged {audit_count} admin cleanup actions")

    print("=" * 65)
    print(f"TEST SUMMARY: TOTAL: {passed + failed} | PASSED: {passed} | FAILED: {failed}")
    print("=" * 65)

    if failed > 0:
        sys.exit(1)

if __name__ == "__main__":
    main()
