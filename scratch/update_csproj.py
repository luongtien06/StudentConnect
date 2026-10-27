with open(r'e:\LTWEB\StudentConnect\StudentConnect.csproj', 'r', encoding='utf-8') as f:
    text = f.read()

target = '    <Compile Include="Helpers\\StoryHelper.cs" />'
if target in text:
    text = text.replace(target, target + '\r\n    <Compile Include="Helpers\\ChatFileModerationHelper.cs" />', 1)
    with open(r'e:\LTWEB\StudentConnect\StudentConnect.csproj', 'w', encoding='utf-8') as f:
        f.write(text)
    print("Added ChatFileModerationHelper to csproj.")
else:
    print("Target not found.")
