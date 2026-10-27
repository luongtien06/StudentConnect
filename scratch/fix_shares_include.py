import os

path = r"Controllers\UserController.cs"
with open(path, "r", encoding="utf-8") as f:
    content = f.read()

# Fix the include: s.PostComments -> s.Post.PostComments
content = content.replace(
    ".Include(s => s.PostComments.Select(c => c.User))",
    ".Include(s => s.Post.PostComments.Select(c => c.User))"
)

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("Fixed rawShares Include in Controllers/UserController.cs!")
