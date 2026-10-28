import urllib.request, urllib.parse, http.cookiejar, json, re

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# 1. Login SuperAdmin
resp = opener.open('http://localhost:59174/Admin/Auth/Login')
html = resp.read().decode('utf-8')
token_m = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html)
token = token_m.group(1) if token_m else ''

data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'username': 'luongtien2222@gmail.com',
    'password': '123456',
    'rememberMe': 'true'
}).encode('utf-8')

req = urllib.request.Request('http://localhost:59174/Admin/Auth/Login', data=data)
resp = opener.open(req)
print('Login URL after redirect:', resp.geturl())

# 2. Get AdminAiAgent/Index page to get token
resp = opener.open('http://localhost:59174/Admin/AdminAiAgent/Index')
index_html = resp.read().decode('utf-8')
token_m2 = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', index_html)
token2 = token_m2.group(1) if token_m2 else token

# 3. Call TestApiConnection with default configured key
post_data = urllib.parse.urlencode({
    '__RequestVerificationToken': token2,
    'customKey': '',
    'customModel': 'gemini-3.5-flash-lite'
}).encode('utf-8')

req2 = urllib.request.Request('http://localhost:59174/Admin/AdminAiAgent/TestApiConnection', data=post_data)
resp2 = opener.open(req2)
res = json.loads(resp2.read().decode('utf-8'))
print('TestApiConnection Result (default key & gemini-3.5-flash-lite):')
print(json.dumps(res, ensure_ascii=False, indent=2))
