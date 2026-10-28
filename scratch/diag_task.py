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

data = urllib.parse.urlencode({'__RequestVerificationToken': token, 'username': 'luongtien2222@gmail.com', 'password': '123456'}).encode()
class NoRedir(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None
op_noredir = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj), NoRedir)
try:
    op_noredir.open(urllib.request.Request('http://localhost:59174/Admin/Auth/Login', data=data))
except:
    pass

r_idx = op.open('http://localhost:59174/Admin/AdminAiAgent')
idx_html = r_idx.read().decode('utf-8')
m2 = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', idx_html)
tok2 = m2.group(1) if m2 else None

task_data = urllib.parse.urlencode({
    '__RequestVerificationToken': tok2,
    'taskName': '[MASTER_AUDIT] Scheduled Task Test',
    'userPrompt': 'Kiểm tra hoạt động hệ thống',
    'taskType': 'Report',
    'scheduleType': 'Daily',
    'scheduleConfig': '{"hour":8,"minute":0}',
    'nextRunAt': ''
}).encode()

req_t = urllib.request.Request('http://localhost:59174/Admin/AdminAiAgent/CreateTask', data=task_data, headers={'X-Requested-With': 'XMLHttpRequest'})
try:
    resp = op.open(req_t)
    print('CreateTask Resp:', resp.code, resp.read().decode('utf-8'))
except urllib.error.HTTPError as e:
    print('CreateTask HTTPError:', e.code, e.read().decode('utf-8'))
