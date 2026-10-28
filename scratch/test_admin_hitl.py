import urllib.request, urllib.parse, http.cookiejar, json, re

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# 1. Login Admin
resp = opener.open('http://localhost:59174/Admin/Auth/Login')
html = resp.read().decode('utf-8')
token = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html).group(1)

data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'username': 'luongtien2222@gmail.com',
    'password': '123456',
    'rememberMe': 'true'
}).encode('utf-8')

req = urllib.request.Request('http://localhost:59174/Admin/Auth/Login', data=data)
resp = opener.open(req)
print('Admin Login final URL:', resp.geturl())

# 2. Check UserID in session by asking Mr. T
post_data = urllib.parse.urlencode({
    'Message': 'Xem giúp tôi các bài viết của tôi'
}).encode('utf-8')
req_ask = urllib.request.Request('http://localhost:59174/Assistant/Ask', data=post_data)
resp_ask = opener.open(req_ask)
res_ask = json.loads(resp_ask.read().decode('utf-8'))
print('Assistant/Ask get_my_posts response:')
print(json.dumps(res_ask, ensure_ascii=False, indent=2))
