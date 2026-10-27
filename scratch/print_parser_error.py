import urllib.request
import urllib.parse
import urllib.error
import ssl
import re

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

req = urllib.request.Request("http://localhost:59174/Admin/AdminChat/Index")
req.add_header("Cookie", "AdminLogin=luongtien2222@gmail.com")

try:
    with urllib.request.urlopen(req, context=ctx, timeout=10) as resp:
        body = resp.read().decode("utf-8", errors="ignore")
        print("HTTP Status:", resp.status)
except urllib.error.HTTPError as e:
    err_body = e.read().decode("utf-8", errors="ignore")
    # extract source error or error line
    for match in re.findall(r"<b>(.*?)</b>", err_body):
        print("MATCH:", match)
    code_matches = re.findall(r"<code><pre>(.*?)</pre></code>", err_body, re.DOTALL)
    for c in code_matches:
        print("CODE:", c)
    if not code_matches:
        print(err_body[:2000])
