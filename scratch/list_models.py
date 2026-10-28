import urllib.request, urllib.error, json

key = 'AQ.Ab8RN6LbbmXDwGC9JBR-2THal17-T_RhrShqiPx9Q2zsoJyB5w'
url = f'https://generativelanguage.googleapis.com/v1beta/models?key={key}'

req = urllib.request.Request(url, headers={'x-goog-api-key': key})
try:
    with urllib.request.urlopen(req) as resp:
        data = json.loads(resp.read().decode('utf-8'))
        print("Total models:", len(data.get('models', [])))
        for m in data.get('models', []):
            print(m.get('name'), m.get('supportedGenerationMethods'))
except urllib.error.HTTPError as e:
    print('HTTP Code:', e.code)
    print(e.read().decode('utf-8'))
