import xml.etree.ElementTree as ET
import urllib.request
import json
import base64
import os

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

print(f"Model: {model}, Key length: {len(api_key)}")

# Create a small 1x1 png image
png_bytes = bytes.fromhex("89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000A49444154789C63000100000500010D0A2DB40000000049454E44AE426082")
b64_img = base64.b64encode(png_bytes).decode('utf-8')

prompt = "Analyze this image for student chat safety (pornography, graphic violence, hate speech). Return JSON strictly: {\"isSafe\": true/false, \"violationCategory\": \"NONE\" or category, \"reason\": \"Vietnamese explanation\"}"

payload = {
    "contents": [
        {
            "parts": [
                {"text": prompt},
                {
                    "inlineData": {
                        "mimeType": "image/png",
                        "data": b64_img
                    }
                }
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
