import re

with open('Models/Assistant/AssistantService.cs', 'r', encoding='utf-8') as f:
    text = f.read()

# Extract declared tool names
match = re.search(r'GetGeminiTools\(\)\s*\{[^"]*return JArray\.Parse\(@"(\[.*?\])"\);', text, re.DOTALL)
if match:
    json_str = match.group(1).replace('""', '"')
    import json
    data = json.loads(json_str)
    declared_tools = [f['name'] for f in data[0]['functionDeclarations']]
    print("Declared tools count:", len(declared_tools))
    print("Declared tools:", declared_tools)
else:
    print("Could not parse GetGeminiTools()")

# Extract switch cases
cases = re.findall(r'case\s+"([^"]+)":', text)
print("Switch cases count:", len(cases))
print("Switch cases:", cases)

# Check missing
for t in declared_tools:
    if t not in cases:
        print("MISSING IN SWITCH:", t)
for c in cases:
    if c not in declared_tools:
        print("EXTRA IN SWITCH:", c)
