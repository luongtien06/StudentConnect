import urllib.request, urllib.parse, http.cookiejar, json, re

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# 1. Login student
resp = opener.open('http://localhost:59174/User/Login')
html = resp.read().decode('utf-8')
token_m = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html)
token = token_m.group(1) if token_m else ''

data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'email': 'testuser01@student.tdmu.edu.vn',
    'password': '123456',
    'rememberMe': 'true'
}).encode('utf-8')

req = urllib.request.Request('http://localhost:59174/User/Login', data=data)
resp = opener.open(req)
print('Student Login URL after redirect:', resp.geturl())

# 2. Call Assistant/Ask - Greeting
post_data1 = urllib.parse.urlencode({
    'Message': 'Chào bạn, bạn là ai?'
}).encode('utf-8')
req1 = urllib.request.Request('http://localhost:59174/Assistant/Ask', data=post_data1)
resp1 = opener.open(req1)
res1 = json.loads(resp1.read().decode('utf-8'))
print('Assistant/Ask Greeting Response:')
print(json.dumps(res1, ensure_ascii=False, indent=2))

# 3. Call Assistant/Ask - Search Tool Calling
post_data2 = urllib.parse.urlencode({
    'Message': 'Tìm giúp tôi bài viết có từ khóa thực tập'
}).encode('utf-8')
req2 = urllib.request.Request('http://localhost:59174/Assistant/Ask', data=post_data2)
resp2 = opener.open(req2)
res2 = json.loads(resp2.read().decode('utf-8'))
print('Assistant/Ask Tool Calling Response:')
print(json.dumps(res2, ensure_ascii=False, indent=2))
