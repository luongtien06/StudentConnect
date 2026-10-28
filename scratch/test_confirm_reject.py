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
opener.open(urllib.request.Request('http://localhost:59174/Admin/Auth/Login', data=data))

# 2. Reject Confirmation 4 safely
post_data = urllib.parse.urlencode({
    'confirmationId': 4,
    'approve': 'false'
}).encode('utf-8')
req = urllib.request.Request('http://localhost:59174/Assistant/ConfirmAction', data=post_data)
resp = opener.open(req)
res = json.loads(resp.read().decode('utf-8'))
print('ConfirmAction reject response:')
print(json.dumps(res, ensure_ascii=False, indent=2))
