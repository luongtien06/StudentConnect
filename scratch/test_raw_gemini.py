import urllib.request, urllib.error, json

url = 'https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=AQ.Ab8RN6LbbmXDwGC9JBR-2THal17-T_RhrShqiPx9Q2zsoJyB5w'
req = urllib.request.Request(
    url,
    data=b'{"contents":[{"parts":[{"text":"hi"}]}]}',
    headers={'Content-Type': 'application/json', 'x-goog-api-key': 'AQ.Ab8RN6LbbmXDwGC9JBR-2THal17-T_RhrShqiPx9Q2zsoJyB5w'}
)
try:
    with urllib.request.urlopen(req) as resp:
        print(resp.read().decode('utf-8'))
except urllib.error.HTTPError as e:
    print('HTTP Code:', e.code)
    print(e.read().decode('utf-8'))
