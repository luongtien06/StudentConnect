import urllib.request, urllib.parse, http.cookiejar, re

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

resp = opener.open('http://localhost:59174/User/Login')
html = resp.read().decode('utf-8')
token = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html).group(1)

data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'email': 'testuser01@student.tdmu.edu.vn',
    'password': '123456'
}).encode('utf-8')

req = urllib.request.Request('http://localhost:59174/User/Login', data=data)
resp = opener.open(req)
body = resp.read().decode('utf-8')

m = re.search(r'class="alert[^"]*"[^>]*>(.*?)</div>', body, re.DOTALL)
if m:
    print('Alert:', m.group(1).strip())
else:
    # search for error
    for l in body.splitlines():
        if 'Error' in l or 'error' in l or 'thất bại' in l or 'chính xác' in l:
            print('Line:', l.strip())
