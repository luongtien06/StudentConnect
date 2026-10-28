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

# 2. Request delete without confirmation
post_data = urllib.parse.urlencode({
    'Message': 'Hãy xóa giúp tôi tin đăng Chợ số 10'
}).encode('utf-8')
req_ask = urllib.request.Request('http://localhost:59174/Assistant/Ask', data=post_data)
resp_ask = opener.open(req_ask)
res_ask = json.loads(resp_ask.read().decode('utf-8'))
print('Assistant/Ask delete_post response:')
print(json.dumps(res_ask, ensure_ascii=False, indent=2))

# 3. Test IDOR protection: User tries to delete another user's post (e.g. post 999 or non-existent)
post_data_idor = urllib.parse.urlencode({
    'Message': 'Hãy xóa giúp tôi bài viết số 1 của người khác'
}).encode('utf-8')
req_idor = urllib.request.Request('http://localhost:59174/Assistant/Ask', data=post_data_idor)
resp_idor = opener.open(req_idor)
res_idor = json.loads(resp_idor.read().decode('utf-8'))
print('Assistant/Ask IDOR protection response:')
print(json.dumps(res_idor, ensure_ascii=False, indent=2))
