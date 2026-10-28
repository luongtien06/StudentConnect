import urllib.request, urllib.parse, http.cookiejar, re

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# Login
req = urllib.request.Request('http://localhost:59174/User/Login')
resp = opener.open(req)
html = resp.read().decode('utf-8')
tok = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html).group(1)

data = urllib.parse.urlencode({
    '__RequestVerificationToken': tok,
    'email': 'luongtien2222@gmail.com',
    'password': '123456',
    'rememberMe': 'true'
}).encode('utf-8')

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj), NoRedirect)
req = urllib.request.Request('http://localhost:59174/User/Login', data=data)
try:
    op.open(req)
except:
    pass

# View Profile (where all posts of user 1 are displayed)
req = urllib.request.Request('http://localhost:59174/User/Profile')
resp = opener.open(req)
profile_html = resp.read().decode('utf-8')

# Search for both posts
for pid in [37, 36]:
    # Look for data-post-id or content
    pos = profile_html.find(f'data-post-id="{pid}"')
    if pos < 0:
        pos = profile_html.find(f'id="post-{pid}"')
    if pos < 0:
        pos = profile_html.find(f'post-card-{pid}')
    
    if pos >= 0:
        snippet = profile_html[pos:pos+2500]
        # Look for post time
        time_m = re.search(r'class="post-time"[^>]*>[\s\S]*?<span>([^<]+)</span>', snippet)
        if time_m:
            print(f'Post {pid} HTML rendered time: "{time_m.group(1).strip()}"')
        else:
            # try any span in post-time
            span_m = re.findall(r'<span>([^<]+)</span>', snippet)
            print(f'Post {pid} snippet:\n', snippet[1000:2000])
    else:
        print(f'Post {pid} not found by ID, searching by text...')
        if pid == 37:
            txt_pos = profile_html.find('Bài viết kiểm tra giờ đăng bình thường')
        else:
            txt_pos = profile_html.find('Xin chào ngày 28/10')
        if txt_pos >= 0:
            snippet = profile_html[max(0, txt_pos-600):txt_pos+200]
            time_m = re.search(r'class="post-time"[^>]*>[\s\S]*?<span>([^<]+)</span>', snippet)
            if time_m:
                print(f'Post {pid} (found by text) HTML rendered time: "{time_m.group(1).strip()}"')
            else:
                print(f'Post {pid} snippet around text:\n', snippet[-300:])
        else:
            print(f'Post {pid} text not found in profile HTML')
