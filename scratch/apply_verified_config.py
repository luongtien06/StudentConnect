import json

# 1. Update Config/AppSettings.config
with open('Config/AppSettings.config', 'r', encoding='utf-8') as f:
    text = f.read()

text = text.replace('<add key="GeminiApiKey" value="" />', '<add key="GeminiApiKey" value="AQ.Ab8RN6LbbmXDwGC9JBR-2THal17-T_RhrShqiPx9Q2zsoJyB5w" />')
text = text.replace('<add key="GeminiModel" value="gemini-1.5-flash" />', '<add key="GeminiModel" value="gemini-3.5-flash-lite" />')

with open('Config/AppSettings.config', 'w', encoding='utf-8') as f:
    f.write(text)

# 2. Update agent_config.json
with open('App_Data/agent_config.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

data['PrimaryModel'] = 'gemini-3.5-flash-lite'
data['FallbackModels'] = ['gemini-flash-lite-latest', 'gemini-3.5-flash', 'gemini-3.1-flash-lite']

with open('App_Data/agent_config.json', 'w', encoding='utf-8') as f:
    json.dump(data, f, ensure_ascii=False, indent=2)

# 3. Update StudentConnect.Worker/App.config
with open('StudentConnect.Worker/App.config', 'r', encoding='utf-8') as f:
    wtext = f.read()

wtext = wtext.replace('<add key="GeminiApiKey" value="" />', '<add key="GeminiApiKey" value="AQ.Ab8RN6LbbmXDwGC9JBR-2THal17-T_RhrShqiPx9Q2zsoJyB5w" />')
wtext = wtext.replace('<add key="GeminiModel" value="gemini-1.5-flash" />', '<add key="GeminiModel" value="gemini-3.5-flash-lite" />')

with open('StudentConnect.Worker/App.config', 'w', encoding='utf-8') as f:
    f.write(wtext)

print("Applied verified configuration successfully.")
