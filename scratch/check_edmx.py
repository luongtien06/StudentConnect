import re

with open('Models/TDMUEcoSystemModel.edmx', 'r', encoding='utf-8') as f:
    text = f.read()

matches = re.findall(r'<EntityType Name="ConnectPosts?".*?</EntityType>', text, re.DOTALL)
for m in matches:
    print(m)
    print('='*40)
