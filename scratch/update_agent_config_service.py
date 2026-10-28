with open('Models/Assistant/AgentConfigService.cs', 'r', encoding='utf-8') as f:
    text = f.read()

text = text.replace('c.PrimaryModel = "gemini-1.5-flash";', 'c.PrimaryModel = "gemini-3.5-flash-lite";')
text = text.replace(
    'c.FallbackModels = new List<string> { "gemini-2.0-flash", "gemini-1.5-pro", "gemini-2.5-flash" };',
    'c.FallbackModels = new List<string> { "gemini-flash-lite-latest", "gemini-3.5-flash", "gemini-3.1-flash-lite" };'
)
text = text.replace(
    'cleanFallbacks : new List<string> { "gemini-2.0-flash", "gemini-1.5-pro", "gemini-2.5-flash" };',
    'cleanFallbacks : new List<string> { "gemini-flash-lite-latest", "gemini-3.5-flash", "gemini-3.1-flash-lite" };'
)
text = text.replace(
    'ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-1.5-flash"',
    'ConfigurationManager.AppSettings["GeminiModel"] ?? "gemini-3.5-flash-lite"'
)

with open('Models/Assistant/AgentConfigService.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("Updated AgentConfigService.cs")
