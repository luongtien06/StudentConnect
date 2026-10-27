with open(r'e:\LTWEB\StudentConnect\StudentConnect.csproj', 'r', encoding='utf-8') as f:
    text = f.read()

target = '<Reference Include="System.Drawing" />'
replacement = '<Reference Include="System.Drawing" />\r\n    <Reference Include="System.IO.Compression" />\r\n    <Reference Include="System.IO.Compression.FileSystem" />'
if 'System.IO.Compression' not in text:
    text = text.replace(target, replacement, 1)
    with open(r'e:\LTWEB\StudentConnect\StudentConnect.csproj', 'w', encoding='utf-8') as f:
        f.write(text)
    print("Added System.IO.Compression references to csproj")
else:
    print("Already present")
