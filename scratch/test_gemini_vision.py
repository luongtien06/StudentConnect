import urllib.request, json

api_key = 'AQ.Ab8RN6IbF_TPHimfg8gGg9f3vJqK00Ro4yeDCZ33sCKQFCk_Mw'
png_b64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='

payload = {
    'contents': [{
        'parts': [
            {'text': 'Analyze this image for content safety in an educational student chat network. Check for: adult/nsfw, violence/gore, hate/harassment, dangerous weapons, illegal drugs. Respond with JSON: {"isSafe": true or false, "flaggedCategory": "none" or category name, "reason": "brief explanation"}'},
            {'inlineData': {'mimeType': 'image/png', 'data': png_b64}}
        ]
    }],
    'generationConfig': {
        'responseMimeType': 'application/json'
    }
}

url = f'https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={api_key}'
data = json.dumps(payload).encode('utf-8')
req = urllib.request.Request(url, data=data, headers={'Content-Type': 'application/json', 'x-goog-api-key': api_key})
try:
    with urllib.request.urlopen(req, timeout=10) as resp:
        res = json.loads(resp.read().decode('utf-8'))
        print('IMAGE MODERATION RESULT:', res['candidates'][0]['content']['parts'][0]['text'])
except Exception as e:
    print('Error:', e)
