# Update Controllers to use DateTimeHelper.VietnamNow for content timestamps

# 1. PostController.cs
with open('Controllers/PostController.cs', 'r', encoding='utf-8') as f:
    pc = f.read()
# Replace CreatedAt = DateTime.Now with CreatedAt = DateTimeHelper.VietnamNow
pc = pc.replace('CreatedAt = DateTime.Now', 'CreatedAt = DateTimeHelper.VietnamNow')
with open('Controllers/PostController.cs', 'w', encoding='utf-8') as f:
    f.write(pc)
print("Updated PostController.cs")

# 2. MarketController.cs
with open('Controllers/MarketController.cs', 'r', encoding='utf-8') as f:
    mc = f.read()
if 'using StudentConnect.Helpers;' not in mc:
    mc = 'using StudentConnect.Helpers;\n' + mc
mc = mc.replace('CreatedAt = DateTime.Now', 'CreatedAt = DateTimeHelper.VietnamNow')
with open('Controllers/MarketController.cs', 'w', encoding='utf-8') as f:
    f.write(mc)
print("Updated MarketController.cs")

# 3. ConnectController.cs
with open('Controllers/ConnectController.cs', 'r', encoding='utf-8') as f:
    cc = f.read()
cc = cc.replace('CreatedAt = DateTime.Now', 'CreatedAt = DateTimeHelper.VietnamNow')
with open('Controllers/ConnectController.cs', 'w', encoding='utf-8') as f:
    f.write(cc)
print("Updated ConnectController.cs")

# 4. StoryController.cs
with open('Controllers/StoryController.cs', 'r', encoding='utf-8') as f:
    sc = f.read()
sc = sc.replace('var now = DateTime.Now;', 'var now = DateTimeHelper.VietnamNow;')
sc = sc.replace('CreatedAt = DateTime.Now', 'CreatedAt = DateTimeHelper.VietnamNow')
with open('Controllers/StoryController.cs', 'w', encoding='utf-8') as f:
    f.write(sc)
print("Updated StoryController.cs")
