import urllib.request
import re

req = urllib.request.Request("http://localhost:59174/Admin/AdminChat/Index")
req.add_header("Cookie", "AdminLogin=luongtien2222@gmail.com")

try:
    with urllib.request.urlopen(req, timeout=10) as resp:
        print("Success:", resp.status)
except Exception as e:
    err_body = e.read().decode("utf-8", errors="ignore")
    # find line number
    m = re.search(r"Line:\s*</b>\s*(\d+)", err_body)
    if m:
        print("Error on Line:", m.group(1))
    # print context around the error
    src = re.search(r"Source File:.*?Line:\s*\d+.*?</pre>", err_body, re.DOTALL)
    if src:
        print(src.group(0))
    else:
        # print error section
        print(err_body[err_body.find("Source Error:"):err_body.find("Source Error:") + 800])
