import urllib.request
import urllib.parse
import http.cookiejar
import re
import json

cj = http.cookiejar.CookieJar()
op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# Admin login
r = op.open('http://localhost:59174/Admin/Auth/Login')
m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', r.read().decode())
token = m.group(1)

data = urllib.parse.urlencode({'__RequestVerificationToken': token, 'username': 'luongtien2222@gmail.com', 'password': '123456'}).encode()
class NoRedir(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None
op_noredir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj), NoRedir)
try:
    op_noredir.open(urllib.request.Request('http://localhost:59174/Admin/Auth/Login', data=data))
except:
    pass

# Get AdminAiAgent page token
r_agent = op.open('http://localhost:59174/Admin/AdminAiAgent')
m_agent = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', r_agent.read().decode())
tok_agent = m_agent.group(1) if m_agent else None

# TestApiConnection with default configured key
req = urllib.request.Request('http://localhost:59174/Admin/AdminAiAgent/TestApiConnection', 
                             data=urllib.parse.urlencode({'__RequestVerificationToken': tok_agent, 'customKey': '', 'customModel': ''}).encode(),
                             headers={'X-Requested-With': 'XMLHttpRequest'})
resp = op.open(req)
print('TestApiConnection response:')
print(json.dumps(json.loads(resp.read().decode()), indent=2, ensure_ascii=False))
