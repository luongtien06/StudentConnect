import urllib.request, urllib.parse, http.cookiejar, json, re, time

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

# Login
resp = opener.open('http://localhost:59174/User/Login')
html = resp.read().decode('utf-8')
token = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html).group(1)

data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'email': 'testuser01@student.tdmu.edu.vn',
    'password': '123456',
    'rememberMe': 'true'
}).encode('utf-8')
opener.open(urllib.request.Request('http://localhost:59174/User/Login', data=data))

# Get token from Home/Index
resp = opener.open('http://localhost:59174/Home/Index')
html_home = resp.read().decode('utf-8')
m = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html_home)
tok_home = m.group(1) if m else token

# Call /Post/Create
c_data = urllib.parse.urlencode({
    '__RequestVerificationToken': tok_home,
    'content': 'Test direct post creation ' + str(int(time.time())),
    'privacy': 'public'
}).encode('utf-8')

req = urllib.request.Request('http://localhost:59174/Post/Create', data=c_data, headers={'X-Requested-With': 'XMLHttpRequest'})
try:
    with opener.open(req) as resp_c:
        print("Status:", resp_c.status)
        print("Body:", resp_c.read().decode('utf-8'))
except urllib.error.HTTPError as e:
    print("HTTP Error:", e.code)
    print("Body:", e.read().decode('utf-8'))
