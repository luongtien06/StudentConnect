import xml.etree.ElementTree as ET
import urllib.request
import json

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

prompt = "Analyze this user text attachment for chat safety (hate speech, sexual harassment, violent threats, illegal). Return JSON strictly: {\"isSafe\": true/false, \"violationCategory\": \"NONE\" or category, \"reason\": \"Vietnamese explanation\"}\nText to analyze:\n\"Tao sẽ mang dao đến trường chém chết mày, cẩn thận đấy!\""

payload = {
    "contents": [
        {"parts": [{"text": prompt}]}
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
    headers={"Content-Type": "application/json", "x-goog-api-key": api_key},
    method="POST"
)

with urllib.request.urlopen(req, timeout=15) as resp:
    body = resp.read().decode('utf-8')
    parsed = json.loads(body)
    print("Violating text response:", parsed['candidates'][0]['content']['parts'][0]['text'])
