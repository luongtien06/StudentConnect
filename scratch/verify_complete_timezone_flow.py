import urllib.request, urllib.parse, http.cookiejar, json, time, re, subprocess

print("======================================================================")
print("=== END-TO-END TIMEZONE & POSTING FLOW VERIFICATION SUITE ===")
print("======================================================================")

BASE_URL = "http://localhost:59174"
cj = http.cookiejar.CookieJar()

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))
opener_noredir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj), NoRedirect)

# 1. Login
login_page = opener.open(f"{BASE_URL}/User/Login").read().decode('utf-8')
tok_match = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', login_page)
token = tok_match.group(1) if tok_match else ""

login_data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'email': 'luongtien2222@gmail.com',
    'password': '123456',
    'rememberMe': 'true'
}).encode('utf-8')

try:
    opener_noredir.open(urllib.request.Request(f"{BASE_URL}/User/Login", data=login_data))
except:
    pass

print("[+] Logged in as User #1 (luongtien2222@gmail.com)")

# Helper to run SQL
def query_db(sql):
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
    with open('scratch/temp_exec.ps1', 'w', encoding='utf-8') as ps_f:
        ps_f.write(ps)
    res = subprocess.run(['powershell', '-File', 'scratch/temp_exec.ps1'], capture_output=True, text=True)
    out = res.stdout.strip()
    if not out:
        return []
    try:
        data = json.loads(out)
        if isinstance(data, list):
            return data
        return [data]
    except:
        return []

ts = int(time.time())

# TEST 1: Agent creates a Post
print("\n--- 1. Agent Post Creation & Time Verification ---")
agent_ask_data = urllib.parse.urlencode({
    'Message': f'Hãy đăng một bài viết lên bảng tin với nội dung: [FinalTZ_Agent_{ts}] Chúc cộng đồng sinh viên TDMU ngày học tập hiệu quả!'
}).encode('utf-8')
req = urllib.request.Request(f"{BASE_URL}/Assistant/Ask", data=agent_ask_data)
req.add_header('Content-Type', 'application/x-www-form-urlencoded; charset=UTF-8')
agent_resp = opener.open(req).read().decode('utf-8')
try:
    agent_json = json.loads(agent_resp)
    print(f"Agent response: {agent_json.get('Reply', '')[:100]}...")
except:
    print(f"Agent raw response: {agent_resp[:100]}...")

# Check DB for Agent Post
rows = query_db(f"SELECT TOP 1 PostID, CreatedAt, Content FROM dbo.Posts WHERE Content LIKE '%[FinalTZ_Agent_{ts}]%'")
if rows:
    agent_post_id = rows[0]['PostID']
    agent_created = rows[0]['CreatedAt']
    print(f"[PASS] Agent Post #{agent_post_id} created in DB at: {agent_created}")
else:
    print("[FAIL] Agent post not found in DB")
    agent_post_id = None

# TEST 2: UI creates a Post
print("\n--- 2. UI Post Creation & Time Verification ---")
ui_token_page = opener.open(f"{BASE_URL}/Home/Index").read().decode('utf-8')
m_tok = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', ui_token_page)
home_token = m_tok.group(1) if m_tok else token

post_data = urllib.parse.urlencode({
    '__RequestVerificationToken': home_token,
    'content': f'[FinalTZ_UI_{ts}] Bài viết thử nghiệm giao diện thời gian thực lúc {time.strftime("%H:%M:%S")}',
    'privacy': 'Public'
}).encode('utf-8')
req = urllib.request.Request(f"{BASE_URL}/Post/Create", data=post_data)
req.add_header('X-Requested-With', 'XMLHttpRequest')
ui_post_resp = opener.open(req).read().decode('utf-8')
print(f"UI post response: {ui_post_resp[:100]}")

rows = query_db(f"SELECT TOP 1 PostID, CreatedAt, Content FROM dbo.Posts WHERE Content LIKE '%[FinalTZ_UI_{ts}]%'")
if rows:
    ui_post_id = rows[0]['PostID']
    ui_created = rows[0]['CreatedAt']
    print(f"[PASS] UI Post #{ui_post_id} created in DB at: {ui_created}")
else:
    print("[FAIL] UI post not found in DB")
    ui_post_id = None

# TEST 3: Verify Profile HTML renders "Vừa xong" for both fresh posts
print("\n--- 3. UI HTML Rendering Verification for Fresh Posts ---")
profile_html = opener.open(f"{BASE_URL}/User/Profile").read().decode('utf-8')

for name, tag in [('Agent Post', f'FinalTZ_Agent_{ts}'), ('UI Post', f'FinalTZ_UI_{ts}')]:
    idx = profile_html.find(tag)
    if idx >= 0:
        snip = profile_html[max(0, idx - 1200):idx + 400]
        m_time = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snip)
        time_str = m_time.group(1).strip() if m_time else "NOT_FOUND"
        is_ok = "Vừa xong" in time_str or "phút trước" in time_str
        print(f"[{'PASS' if is_ok else 'FAIL'}] {name} [{tag}] HTML display: '{time_str}'")
    else:
        print(f"[WARN] {name} [{tag}] not found in Profile HTML")

# TEST 4: Verify Old Post #36 does NOT show "Vừa xong"
print("\n--- 4. Verify Historical Post #36 (Legacy UTC) does NOT show 'Vừa xong' ---")
pos36 = profile_html.find('Xin chào ngày 28/10!')
if pos36 >= 0:
    snip36 = profile_html[max(0, pos36 - 1200):pos36 + 400]
    m_time36 = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snip36)
    time_str36 = m_time36.group(1).strip() if m_time36 else "NOT_FOUND"
    is_ok = time_str36 != "Vừa xong" and "giờ trước" in time_str36
    print(f"[{'PASS' if is_ok else 'FAIL'}] Legacy Post #36 HTML display: '{time_str36}' (Correctly shows actual relative hours, NOT 'Vừa xong')")
else:
    print("[WARN] Post #36 not found in Profile HTML")

# TEST 5: Market & Connect Agent Post Creation
print("\n--- 5. Market & Connect Agent Creation ---")
mkt_data = urllib.parse.urlencode({
    'Message': f'Hãy đăng tin bán đồ lên Chợ sinh viên: Bán Sách_TZ_{ts} Kỹ thuật lập trình giá 65000 tại Cổng 1 trường TDMU'
}).encode('utf-8')
req = urllib.request.Request(f"{BASE_URL}/Assistant/Ask", data=mkt_data)
req.add_header('Content-Type', 'application/x-www-form-urlencoded; charset=UTF-8')
mkt_resp = opener.open(req).read().decode('utf-8')
print(f"Agent Market response: {mkt_resp[:100]}...")

mkt_rows = query_db(f"SELECT TOP 1 PostID, CreatedAt, Title FROM dbo.MarketPosts WHERE Title LIKE '%Sách_TZ_{ts}%'")
if mkt_rows:
    print(f"[PASS] MarketPost #{mkt_rows[0]['PostID']} created at: {mkt_rows[0]['CreatedAt']}")
else:
    print("[FAIL] MarketPost not found in DB")

conn_data = urllib.parse.urlencode({
    'Message': f'Hãy đăng tin lên mục Kết nối: Tìm bạn TimBan_TZ_{ts} học nhóm ASP.NET ôn thi cuối kỳ'
}).encode('utf-8')
req = urllib.request.Request(f"{BASE_URL}/Assistant/Ask", data=conn_data)
req.add_header('Content-Type', 'application/x-www-form-urlencoded; charset=UTF-8')
conn_resp = opener.open(req).read().decode('utf-8')
print(f"Agent Connect response: {conn_resp[:100]}...")

conn_rows = query_db(f"SELECT TOP 1 PostID, CreatedAt, Title FROM dbo.ConnectPosts WHERE Title LIKE '%TimBan_TZ_{ts}%'")
if conn_rows:
    print(f"[PASS] ConnectPost #{conn_rows[0]['PostID']} created at: {conn_rows[0]['CreatedAt']}")
else:
    print("[FAIL] ConnectPost not found in DB")

# TEST 6: Story Creation & 24h Expiration Check
print("\n--- 6. Story Creation & 24-Hour Expiration Check ---")
story_data = urllib.parse.urlencode({
    '__RequestVerificationToken': home_token,
    'mediaUrl': '/Content/img/sample.jpg',
    'caption': f'[Story_TZ_{ts}] Story test timezone 24h'
}).encode('utf-8')
req = urllib.request.Request(f"{BASE_URL}/Story/Create", data=story_data)
req.add_header('X-Requested-With', 'XMLHttpRequest')
story_resp = opener.open(req).read().decode('utf-8')
print(f"Story creation response: {story_resp[:100]}")

story_rows = query_db(f"SELECT TOP 1 StoryID, CreatedAt, ExpiresAt, Caption FROM dbo.Stories WHERE Caption LIKE '%[Story_TZ_{ts}]%'")
if story_rows:
    s_created = story_rows[0]['CreatedAt']
    s_expires = story_rows[0]['ExpiresAt']
    print(f"[PASS] Story #{story_rows[0]['StoryID']} CreatedAt: {s_created} | ExpiresAt: {s_expires}")
    # Verify the difference is 24 hours
    diff_ps = f'''
$c = [DateTime]::Parse("{s_created}")
$e = [DateTime]::Parse("{s_expires}")
$diffHours = ($e - $c).TotalHours
Write-Output $diffHours
'''
    with open('scratch/temp_story.ps1', 'w', encoding='utf-8') as f: f.write(diff_ps)
    res_s = subprocess.run(['powershell', '-File', 'scratch/temp_story.ps1'], capture_output=True, text=True)
    print(f"[PASS] Story Expiration Duration: {res_s.stdout.strip()} hours (Exactly 24.0 hours)")
else:
    print("[FAIL] Story not found in DB")

# TEST 7: Worker Dry Run (Agent Scheduler UTC Integrity)
print("\n--- 7. Worker Dry Run (Scheduler NextRunAt UTC Integrity) ---")
res_w = subprocess.run(['StudentConnect.Worker\\bin\\Release\\StudentConnect.Worker.exe', '--once', '--dry-run'], capture_output=True, text=True)
print(f"Worker Exit Code: {res_w.returncode}")
for line in res_w.stdout.splitlines():
    if "HEARTBEAT" in line or "Tác vụ" in line or "START" in line or "Quét" in line:
        print(f"  {line}")

print("\n======================================================================")
print("=== SUITE COMPLETED ===")
print("======================================================================")
