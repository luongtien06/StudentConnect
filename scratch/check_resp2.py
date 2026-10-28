import urllib.request, urllib.parse, http.cookiejar, re

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

st, html, _ = opener.open('http://localhost:59174/User/Login').read().decode(), None, None
token = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', st).group(1)

data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'email': 'testuser01@student.tdmu.edu.vn',
    'password': '123456'
}).encode()

req = urllib.request.Request('http://localhost:59174/User/Login', data=data)
try:
    resp = opener.open(req)
    body = resp.read().decode('utf-8')
    print("Status:", resp.status)
    print("Final URL:", resp.geturl())
    # find any text in error/danger classes
    matches = re.findall(r'class="[^"]*(?:alert|danger|error)[^"]*"[^>]*>(.*?)(?:</div>|</p>|</span>)', body, re.DOTALL)
    for m in matches:
        clean = re.sub(r'<[^>]+>', '', m).strip()
        if clean:
            print("Alert text:", clean)
except urllib.error.HTTPError as e:
    print("HTTP Error:", e.code)
    print(e.read().decode('utf-8')[:300])
