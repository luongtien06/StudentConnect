import urllib.request, urllib.parse, http.cookiejar, re

cj = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

resp = opener.open('http://localhost:59174/User/Login')
html = resp.read().decode('utf-8')
token = re.search(r'name=["\']__RequestVerificationToken["\']\s+type=["\']hidden["\']\s+value=["\']([^"\']+)["\']', html).group(1)

data = urllib.parse.urlencode({
    '__RequestVerificationToken': token,
    'email': '2424802010453@student.tdmu.edu.vn',
    'password': '123456'
}).encode('utf-8')

req = urllib.request.Request('http://localhost:59174/User/Login', data=data)
resp = opener.open(req)
print("Final URL:", resp.geturl())
for c in cj:
    print("Cookie:", c.name, c.value[:10])
