path = 'Web.Release.config'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

import re
pattern = re.compile(r'<system\.web>.*?</system\.web>', re.DOTALL)
replacement = '''<system.web>
    <compilation xdt:Transform="RemoveAttributes(debug)" />
    <customErrors mode="RemoteOnly" xdt:Transform="Replace" />
  </system.web>'''

c_new = pattern.sub(replacement, c)
with open(path, 'w', encoding='utf-8', newline='\r\n') as f:
    f.write(c_new)
print('Successfully configured customErrors in Web.Release.config')
