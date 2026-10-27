import difflib

with open(r"scratch\backups_p2\UserController.cs.bak", "r", encoding="utf-8") as f:
    old_lines = f.readlines()

with open(r"Controllers\UserController.cs", "r", encoding="utf-8") as f:
    new_lines = f.readlines()

diff = difflib.unified_diff(old_lines, new_lines, fromfile="UserController.cs.bak", tofile="UserController.cs", n=3)
diff_text = "".join(list(diff))
print("Diff length:", len(diff_text))
with open(r"scratch\p2_usercontroller_diff.patch", "w", encoding="utf-8") as f:
    f.write(diff_text)
print("Patch written to scratch/p2_usercontroller_diff.patch")
