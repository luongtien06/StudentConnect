with open('Models/Assistant/AiApiClient.cs', 'r', encoding='utf-8') as f:
    text = f.read()

text = text.replace('Model = "gemini-1.5-flash";', 'Model = "gemini-3.5-flash-lite";')
text = text.replace('modelToUse = !string.IsNullOrWhiteSpace(customModel) ? customModel.Trim() : (Model ?? "gemini-1.5-flash");', 'modelToUse = !string.IsNullOrWhiteSpace(customModel) ? customModel.Trim() : (Model ?? "gemini-3.5-flash-lite");')
text = text.replace('modelToUse = "gemini-1.5-flash";', 'modelToUse = "gemini-3.5-flash-lite";')

old_candidate_loop = """            // Danh sách model candidate: model hiện tại -> danh sách fallback hợp lệ
            var candidateModels = new List<string>();
            if (!string.IsNullOrWhiteSpace(Model))
            {
                candidateModels.Add(Model);
            }

            foreach (var supported in AgentConfigModel.SupportedModels)
            {
                if (!candidateModels.Contains(supported, StringComparer.OrdinalIgnoreCase))
                {
                    candidateModels.Add(supported);
                }
            }"""

new_candidate_loop = """            // Danh sách model candidate: model hiện tại -> danh sách fallback hợp lệ
            var candidateModels = new List<string>();
            if (!string.IsNullOrWhiteSpace(Model))
            {
                candidateModels.Add(Model);
            }

            try
            {
                var configuredFallbacks = AgentConfigService.GetConfig()?.FallbackModels;
                if (configuredFallbacks != null)
                {
                    foreach (var fb in configuredFallbacks)
                    {
                        if (!string.IsNullOrWhiteSpace(fb) && !candidateModels.Contains(fb.Trim(), StringComparer.OrdinalIgnoreCase))
                        {
                            candidateModels.Add(fb.Trim());
                        }
                    }
                }
            }
            catch { }

            foreach (var supported in AgentConfigModel.SupportedModels)
            {
                if (!candidateModels.Contains(supported, StringComparer.OrdinalIgnoreCase))
                {
                    candidateModels.Add(supported);
                }
            }"""

text = text.replace(old_candidate_loop, new_candidate_loop)

with open('Models/Assistant/AiApiClient.cs', 'w', encoding='utf-8') as f:
    f.write(text)

print("Updated AiApiClient.cs")
