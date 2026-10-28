with open('Areas/Admin/Controllers/AdminAiAgentController.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target = 'currentConfig.PrimaryModel = string.IsNullOrWhiteSpace(model.PrimaryModel) ? "gemini-3.5-flash-lite" : model.PrimaryModel.Trim();'

replacement = '''string selectedModel = model.PrimaryModel?.Trim();
            if (string.IsNullOrWhiteSpace(selectedModel) || !Array.Exists(AgentConfigModel.SupportedModels, m => string.Equals(m, selectedModel, StringComparison.OrdinalIgnoreCase)))
            {
                selectedModel = "gemini-1.5-flash";
            }
            currentConfig.PrimaryModel = selectedModel;'''

if target in text:
    text = text.replace(target, replacement)
    with open('Areas/Admin/Controllers/AdminAiAgentController.cs', 'w', encoding='utf-8') as f:
        f.write(text)
    print("Updated AdminAiAgentController.cs successfully.")
else:
    print("Target not found in AdminAiAgentController.cs")
