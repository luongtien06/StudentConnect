with open('Models/Assistant/SocialTools.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Make sure StudentConnect.Helpers is included if needed
if 'using StudentConnect.Helpers;' not in content:
    content = 'using StudentConnect.Helpers;\n' + content

# Replace DateTime.Now in SocialTools.cs with DateTimeHelper.VietnamNow
old_count = content.count('DateTime.Now')
content = content.replace('DateTime.Now', 'DateTimeHelper.VietnamNow')
new_count = content.count('DateTime.Now')

with open('Models/Assistant/SocialTools.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print(f"SUCCESS: Replaced {old_count - new_count} occurrences of DateTime.Now with DateTimeHelper.VietnamNow in SocialTools.cs")
