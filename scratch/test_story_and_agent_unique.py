import urllib.request, urllib.parse, http.cookiejar, json, time, re, subprocess

BASE_URL = "http://localhost:59174"
cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# Login
login_page = opener.open(f"{BASE_URL}/User/Login").read().decode('utf-8')
tok_match = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', login_page)
token = tok_match.group(1) if tok_match else ""

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None
opener_noredir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj), NoRedirect)

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

ts = int(time.time())

# 1. Test Story creation with multipart/form-data
print("--- 1. Testing Story Creation with Multipart File ---")
home_page = opener.open(f"{BASE_URL}/Home/Index").read().decode('utf-8')
m_tok = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', home_page)
home_token = m_tok.group(1) if m_tok else token

boundary = f"----WebKitFormBoundary{ts}"
story_caption = f"[Story24h_{ts}] Kiểm thử Story múi giờ và thời gian hết hạn 24h"

# 1x1 transparent PNG
png_bytes = b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15c4\x00\x00\x00\nIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01\r\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82'

body = bytearray()
def add_field(name, val):
    body.extend(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{val}\r\n".encode('utf-8'))

add_field('__RequestVerificationToken', home_token)
add_field('caption', story_caption)

# Add mediaFile
body.extend(f"--{boundary}\r\nContent-Disposition: form-data; name=\"mediaFile\"; filename=\"test.png\"\r\nContent-Type: image/png\r\n\r\n".encode('utf-8'))
body.extend(png_bytes)
body.extend(f"\r\n--{boundary}--\r\n".encode('utf-8'))

req = urllib.request.Request(f"{BASE_URL}/Story/Create", data=bytes(body))
req.add_header('Content-Type', f'multipart/form-data; boundary={boundary}')
req.add_header('X-Requested-With', 'XMLHttpRequest')
story_resp = opener.open(req).read().decode('utf-8')
print("Story create response:", story_resp)

# Query DB for story
ps = f'''
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 1 StoryID, CreatedAt, ExpiresAt, Caption FROM dbo.Stories WHERE Caption LIKE '%[Story24h_{ts}]%'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    $c = [DateTime]$reader["CreatedAt"]
    $e = [DateTime]$reader["ExpiresAt"]
    $diff = ($e - $c).TotalHours
    Write-Host ("StoryID: " + $reader["StoryID"] + " | CreatedAt: " + $c.ToString("yyyy-MM-dd HH:mm:ss") + " | ExpiresAt: " + $e.ToString("yyyy-MM-dd HH:mm:ss") + " | Diff: " + $diff + " hours")
}}
$reader.Close()
$conn.Close()
'''
with open('scratch/temp_story_check.ps1', 'w', encoding='utf-8') as f: f.write(ps)
res_st = subprocess.run(['powershell', '-File', 'scratch/temp_story_check.ps1'], capture_output=True, text=True)
print(res_st.stdout)

# 2. Test Agent unique post
print("\n--- 2. Testing Unique Agent Post Creation ---")
agent_msg = f"Đăng bài viết mới lên bảng tin: Hôm nay ngày 28/10 lúc {time.strftime('%H:%M:%S')}, thư viện trường TDMU mở cửa đón sinh viên đến ôn thi học kỳ nhé!"
agent_data = urllib.parse.urlencode({'Message': agent_msg}).encode('utf-8')
req = urllib.request.Request(f"{BASE_URL}/Assistant/Ask", data=agent_data)
req.add_header('Content-Type', 'application/x-www-form-urlencoded; charset=UTF-8')
agent_resp = json.loads(opener.open(req).read().decode('utf-8'))
print("Agent reply:", agent_resp.get('Reply'))

# Check newly created post
ps_post = '''
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 1 PostID, CreatedAt, Content FROM dbo.Posts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Latest PostID: " + $reader["PostID"] + " | CreatedAt: " + [DateTime]$reader["CreatedAt"] + " | Content: " + $reader["Content"])
}
$reader.Close()
$conn.Close()
'''
with open('scratch/temp_latest_post.ps1', 'w', encoding='utf-8') as f: f.write(ps_post)
res_p = subprocess.run(['powershell', '-File', 'scratch/temp_latest_post.ps1'], capture_output=True, text=True)
print(res_p.stdout)
