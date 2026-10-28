import urllib.request
import urllib.parse
import http.cookiejar
import re
import json

BASE_URL = "http://localhost:59174"

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# 1. Login as user 1002 (luongtien2222@gmail.com)
resp = opener.open(BASE_URL + "/User/Login")
html = resp.read().decode('utf-8')
m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
token = m.group(1) if m else None

login_data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'email': 'luongtien2222@gmail.com',
    'password': '123456'
}).encode('utf-8')

req = urllib.request.Request(BASE_URL + "/User/Login", data=login_data)
resp = opener.open(req)
print("Login result URL:", resp.geturl())

# 2. Call Assistant/Ask
ask_payload = {
    "Message": "đăng bài Xin chào kiểm tra AI Agent ngay bây giờ"
}
json_data = json.dumps(ask_payload).encode('utf-8')

req2 = urllib.request.Request(
    BASE_URL + "/Assistant/Ask", 
    data=json_data, 
    headers={
        "Content-Type": "application/json",
        "X-Requested-With": "XMLHttpRequest"
    }
)

try:
    resp2 = opener.open(req2, timeout=30)
    print("Ask response status:", resp2.code)
    print("Ask response body:", resp2.read().decode('utf-8'))
except urllib.error.HTTPError as e:
    print("Ask HTTPError:", e.code)
    print("Body:", e.read().decode('utf-8'))
except Exception as e:
    print("Exception:", str(e))
