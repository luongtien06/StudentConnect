import urllib.request, urllib.parse, http.cookiejar, re, json, time, subprocess

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# 1. Login user 1
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
    r = op.open(req)
except urllib.error.HTTPError as e:
    pass

# 2. Get Home/Index to get Token
req = urllib.request.Request('http://localhost:59174/Home/Index')
resp = opener.open(req)
html = resp.read().decode('utf-8')
tok2 = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html).group(1)

# 3. Post to /Post/Create
test_content = 'Bài viết kiểm tra giờ đăng bình thường qua giao diện #' + str(int(time.time()))
post_data = urllib.parse.urlencode({
    '__RequestVerificationToken': tok2,
    'content': test_content,
    'privacy': 'public'
}).encode('utf-8')

req = urllib.request.Request('http://localhost:59174/Post/Create', data=post_data, headers={'X-Requested-With': 'XMLHttpRequest'})
resp = opener.open(req)
print('Response from /Post/Create:', resp.read().decode('utf-8'))

# 4. Check Home/Index HTML to see how it is displayed
req = urllib.request.Request('http://localhost:59174/Home/Index')
resp = opener.open(req)
feed_html = resp.read().decode('utf-8')

# Find the post in HTML
idx = feed_html.find(test_content)
if idx >= 0:
    chunk = feed_html[max(0, idx - 400):min(len(feed_html), idx + 200)]
    print("\n--- HTML CHUNK FOR NORMAL POST ---")
    print(chunk)
else:
    print("Post not found in Home/Index HTML")
