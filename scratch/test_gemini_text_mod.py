import xml.etree.ElementTree as ET
import urllib.request
import json

# Read API key from AppSettings.config
config_path = r"E:\LTWEB\StudentConnect\Config\AppSettings.config"
tree = ET.parse(config_path)
root = tree.getroot()
api_key = ""
model = "gemini-3.5-flash-lite"
for add in root.findall("add"):
    if add.get("key") == "GeminiApiKey":
        api_key = add.get("value")
    if add.get("key") == "GeminiModel":
        model = add.get("value")

prompt = "Analyze this user text attachment for chat safety (hate speech, sexual harassment, violent threats, illegal). Return JSON strictly: {\"isSafe\": true/false, \"violationCategory\": \"NONE\" or category, \"reason\": \"Vietnamese explanation\"}\nText to analyze:\n\"Xin chào các bạn sinh viên TDMU, tài liệu ôn thi môn Mạng máy tính\""

payload = {
    "contents": [
        {
            "parts": [
                {"text": prompt}
            ]
        }
    ],
    "generationConfig": {
        "responseMimeType": "application/json",
        "temperature": 0.1,
        "maxOutputTokens": 200
    }
}

url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"
req = urllib.request.Request(
    url,
    data=json.dumps(payload).encode('utf-8'),
    headers={
        "Content-Type": "application/json",
        "x-goog-api-key": api_key
    },
    method="POST"
)

try:
    with urllib.request.urlopen(req, timeout=15) as resp:
        body = resp.read().decode('utf-8')
        print("Status:", resp.status)
        parsed = json.loads(body)
        print("Response:", json.dumps(parsed, indent=2, ensure_ascii=False))
except urllib.error.HTTPError as e:
    err_body = e.read().decode('utf-8')
    print("HTTPError:", e.code, err_body)
except Exception as ex:
    print("Error:", ex)
