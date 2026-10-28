with open('Models/Assistant/AgentConfigModel.cs', 'rb') as f:
    c = f.read().decode('utf-8')

old_part = '''        public static readonly string[] SupportedModels = new[]
        {
            "gemini-3.5-flash-lite",
            "gemini-flash-lite-latest",
            "gemini-3.5-flash",
            "gemini-3.1-flash-lite",
            "gemini-2.5-flash",
            "gemini-2.0-flash",
            "gemini-1.5-flash"
        };

        public string AgentName { get; set; } = "Trợ lý ảo Mr. T";
        public string PrimaryModel { get; set; } = "gemini-3.5-flash-lite";
        public List<string> FallbackModels { get; set; } = new List<string>
        {
            "gemini-flash-lite-latest",
            "gemini-3.5-flash",
            "gemini-3.1-flash-lite"
        };'''

new_part = '''        public static readonly string[] SupportedModels = new[]
        {
            "gemini-3.5-flash-lite",
            "gemini-flash-lite-latest",
            "gemini-3.5-flash"
        };

        public string AgentName { get; set; } = "Trợ lý ảo Mr. T";
        public string PrimaryModel { get; set; } = "gemini-3.5-flash-lite";
        public List<string> FallbackModels { get; set; } = new List<string>
        {
            "gemini-flash-lite-latest",
            "gemini-3.5-flash"
        };'''

old_crlf = old_part.replace('\n', '\r\n')
new_crlf = new_part.replace('\n', '\r\n')

if old_crlf in c:
    c = c.replace(old_crlf, new_crlf)
elif old_part in c:
    c = c.replace(old_part, new_part)
else:
    print('Failed to find old_part!')

with open('Models/Assistant/AgentConfigModel.cs', 'wb') as f:
    f.write(c.encode('utf-8'))

print('AgentConfigModel.cs updated!')
