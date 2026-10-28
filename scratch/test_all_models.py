import urllib.request, urllib.error, json

key = 'AQ.Ab8RN6LbbmXDwGC9JBR-2THal17-T_RhrShqiPx9Q2zsoJyB5w'

models_to_test = [
    'gemini-3.5-flash-lite',
    'gemini-flash-lite-latest',
    'gemini-3.1-flash-lite',
    'gemini-3.5-flash',
    'gemini-3.8-flash'
]

for m in models_to_test:
    url = f'https://generativelanguage.googleapis.com/v1beta/models/{m}:generateContent?key={key}'
    req = urllib.request.Request(
        url,
        data=b'{"contents":[{"role":"user","parts":[{"text":"ping"}]}]}',
        headers={'Content-Type': 'application/json', 'x-goog-api-key': key}
    )
    try:
        with urllib.request.urlopen(req, timeout=8) as resp:
            print(f"[{m}] SUCCESS (200)")
    except urllib.error.HTTPError as e:
        print(f"[{m}] FAILED ({e.code}): {e.read().decode('utf-8')[:80]}")
    except Exception as ex:
        print(f"[{m}] ERROR: {ex}")
