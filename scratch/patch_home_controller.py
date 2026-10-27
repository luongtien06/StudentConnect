path = 'Controllers/HomeController.cs'
with open(path, 'r', encoding='utf-8-sig') as f:
    text = f.read()

target = 'int currentUserId = Session["UserID"] != null ? (int)Session["UserID"] : 0;'
replacement = 'int currentUserId = (Session["UserID"] != null && Session["Username"] != null) ? (int)Session["UserID"] : 0;'

new_text = text.replace(target, replacement, 1)

with open(path, 'w', encoding='utf-8-sig', newline='') as f:
    f.write(new_text)

print('Updated HomeController.cs line 15')
