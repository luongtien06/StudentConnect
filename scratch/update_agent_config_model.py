with open('Models/Assistant/AgentConfigModel.cs', 'r', encoding='utf-8') as f:
    text = f.read()

old_block = """        public static readonly string[] SupportedModels = new[]
        {
            "gemini-1.5-flash",
            "gemini-2.0-flash",
            "gemini-1.5-pro",
            "gemini-2.5-flash"
        };

        public string AgentName { get; set; } = "Trợ lý ảo Mr. T";
        public string PrimaryModel { get; set; } = "gemini-1.5-flash";
        public List<string> FallbackModels { get; set; } = new List<string>
        {
            "gemini-2.0-flash",
            "gemini-1.5-pro",
            "gemini-2.5-flash"
        };"""

new_block = """        public static readonly string[] SupportedModels = new[]
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
        };"""

text = text.replace(old_block, new_block)
with open('Models/Assistant/AgentConfigModel.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("Updated AgentConfigModel.cs")
