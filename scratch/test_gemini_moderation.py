import urllib.request, json, base64

api_key = 'AQ.Ab8RN6IbF_TPHimfg8gGg9f3vJqK00Ro4yeDCZ33sCKQFCk_Mw'
png_b64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII='

payload = {
    'contents': [{
        'parts': [
            {'text': 'Analyze this image for content safety in an educational student chat network. Check for: adult/nsfw, violence/gore, hate/harassment, dangerous weapons, illegal drugs. Respond with JSON: {"isSafe": true or false, "flaggedCategory": "none" or category name, "reason": "brief explanation in Vietnamese"}'},
            {'inlineData': {'mimeType': 'image/png', 'data': png_b64}}
        ]
    }],
    'generationConfig': {
        'responseMimeType': 'application/json'
    }
}

url = f'https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={api_key}'
req = urllib.request.Request(url, data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json', 'x-goog-api-key': api_key})
try:
    with urllib.request.urlopen(req, timeout=15) as resp:
        res = json.loads(resp.read().decode('utf-8'))
        text = res['candidates'][0]['content']['parts'][0]['text']
        print('Gemini Moderation Result:', text)
except Exception as e:
    print('Error:', e)
