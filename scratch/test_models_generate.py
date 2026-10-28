import urllib.request, urllib.error, json

key = 'AQ.Ab8RN6LbbmXDwGC9JBR-2THal17-T_RhrShqiPx9Q2zsoJyB5w'

for model in ['gemini-2.5-flash', 'gemini-3.5-flash-lite', 'gemini-flash-lite-latest', 'gemini-1.5-flash']:
    url = f'https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}'
    body = json.dumps({
        "contents": [{"role": "user", "parts": [{"text": "Chào bạn, hãy trả lời ngắn gọn: 1+1=?"}]}]
    }).encode('utf-8')
    req = urllib.request.Request(url, data=body, headers={'Content-Type': 'application/json', 'x-goog-api-key': key})
    try:
        with urllib.request.urlopen(req) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            reply = data['candidates'][0]['content']['parts'][0]['text']
            print(f"SUCCESS with {model}: {reply.strip()}")
    except urllib.error.HTTPError as e:
        print(f"FAILED with {model}: HTTP {e.code} - {e.read().decode('utf-8')[:120]}")
