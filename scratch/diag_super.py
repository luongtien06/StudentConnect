import urllib.request
import urllib.parse
import http.cookiejar
import re

cj = http.cookiejar.CookieJar()
op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# Admin login
r = op.open('http://localhost:59174/Admin/Auth/Login')
html = r.read().decode('utf-8')
m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
token = m.group(1) if m else None

data = urllib.parse.urlencode({
    '__RequestVerificationToken': token, 
    'username': 'luongtien2222@gmail.com', 
    'password': '123456',
    'rememberMe': 'true'
}).encode()

class NoRedir(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

op_noredir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj), NoRedir)
try:
    resp_post = op_noredir.open(urllib.request.Request('http://localhost:59174/Admin/Auth/Login', data=data))
    print("Login result:", resp_post.code, resp_post.headers.get('Location'))
except urllib.error.HTTPError as e:
    print("Login result:", e.code, e.headers.get('Location'))

routes = [
    ('/Admin/AdminUser', 'AdminUser', 'Quản lý Người dùng'),
    ('/Admin/AdminMarket', 'AdminMarket', 'Quản lý Chợ'),
    ('/Admin/AdminConnect', 'AdminConnect', 'Quản lý Kết nối'),
    ('/Admin/AdminChat', 'AdminChat', 'Quản lý Trò chuyện'),
    ('/Admin/AdminFeedStory', 'AdminFeedStory', 'Quản lý Bảng tin'),
    ('/Admin/AdminAiAgent', 'AdminAiAgent', 'Quản lý AI Agent')
]

for route, name, kw in routes:
    resp = op.open('http://localhost:59174' + route)
    c = resp.read().decode('utf-8', errors='ignore')
    print(name, 'Code:', resp.code, 'Has KW ("' + kw + '"):', kw in c, 'Length:', len(c))
