import urllib.request, urllib.parse, http.cookiejar, re

class TestClient:
    def __init__(self):
        self.cj = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cj))

    def get(self, path):
        req = urllib.request.Request("http://localhost:59174" + path)
        with self.opener.open(req) as resp:
            return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers

    def post(self, path, data):
        enc = urllib.parse.urlencode(data).encode('utf-8')
        req = urllib.request.Request("http://localhost:59174" + path, data=enc)
        with self.opener.open(req) as resp:
            return resp.status, resp.read().decode('utf-8', errors='replace'), resp.headers

cli = TestClient()
st, html, _ = cli.get('/User/Login')
m = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html)
token = m.group(1)

# Login
st, resp, hd = cli.post('/User/Login', {
    '__RequestVerificationToken': token,
    'email': 'testuser01@student.tdmu.edu.vn',
    'password': 'Password123!'
})

print("Cookies after login with Password123!:")
for c in cli.cj:
    print(c.name, c.value[:10])

# Also try 123456
cli2 = TestClient()
st2, html2, _ = cli2.get('/User/Login')
m2 = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html2)
token2 = m2.group(1)

st2, resp2, hd2 = cli2.post('/User/Login', {
    '__RequestVerificationToken': token2,
    'email': 'testuser01@student.tdmu.edu.vn',
    'password': '123456'
})

print("Cookies after login with 123456:")
for c in cli2.cj:
    print(c.name, c.value[:10])
