import urllib.request, re, http.cookiejar, urllib.parse

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# Login
login_page = opener.open('http://localhost:59174/User/Login').read().decode('utf-8')
tok_match = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', login_page)
token = tok_match.group(1) if tok_match else ''

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None
op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj), NoRedirect)
data = urllib.parse.urlencode({'__RequestVerificationToken': token, 'email': 'luongtien2222@gmail.com', 'password': '123456', 'rememberMe': 'true'}).encode('utf-8')
try: op.open(urllib.request.Request('http://localhost:59174/User/Login', data=data))
except: pass

prof_html = opener.open('http://localhost:59174/User/Profile').read().decode('utf-8')
pos = prof_html.find('thư viện trường TDMU mở cửa')
if pos >= 0:
    snip = prof_html[max(0, pos-1200):pos+400]
    m_time = re.search(r'class="post-meta-line"[^>]*>[\s\S]*?<span>([^<]+)</span>', snip)
    print('Post #48 Profile HTML Time Ago:', m_time.group(1).strip() if m_time else 'NOT_FOUND')
else:
    print('Post #48 not found on Profile')
