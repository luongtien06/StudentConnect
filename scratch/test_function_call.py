import urllib.request, urllib.error, json

key = 'AQ.Ab8RN6LbbmXDwGC9JBR-2THal17-T_RhrShqiPx9Q2zsoJyB5w'
model = 'gemini-3.5-flash-lite'
url = f'https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}'

payload = {
    "contents": [
        {
            "role": "user",
            "parts": [{"text": "Hãy tìm kiếm cho tôi bài viết có từ khóa 'thực tập' trên StudentConnect"}]
        }
    ],
    "tools": [
        {
            "functionDeclarations": [
                {
                    "name": "search_posts",
                    "description": "Tìm kiếm các bài viết của sinh viên",
                    "parameters": {
                        "type": "OBJECT",
                        "properties": {
                            "keyword": {"type": "STRING", "description": "Từ khóa cần tìm"}
                        },
                        "required": ["keyword"]
                    }
                }
            ]
        }
    ]
}

req = urllib.request.Request(
    url,
    data=json.dumps(payload).encode('utf-8'),
    headers={'Content-Type': 'application/json', 'x-goog-api-key': key}
)

try:
    with urllib.request.urlopen(req) as resp:
        data = json.loads(resp.read().decode('utf-8'))
        print("HTTP Status:", resp.status)
        print("Response:", json.dumps(data, ensure_ascii=False, indent=2))
except urllib.error.HTTPError as e:
    print(f"HTTP {e.code}: {e.read().decode('utf-8')}")
