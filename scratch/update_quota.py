import json

with open('App_Data/agent_config.json', 'r', encoding='utf-8') as f:
    cfg = json.load(f)

cfg['MaxDailyGeminiRequestsPerUser'] = 500

with open('App_Data/agent_config.json', 'w', encoding='utf-8') as f:
    json.dump(cfg, f, indent=2, ensure_ascii=False)

print('Updated MaxDailyGeminiRequestsPerUser to 500!')
