path = 'Areas/Admin/Controllers/AuthController.cs'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

target = """        // Admin/Auth/Logout
        public ActionResult Logout()
        {
            Session.Remove("AdminRole");
            Session.Remove("AdminEmail");
            Session.Remove("AdminUsername");
            Session.Remove("AdminAvatar");"""

replacement = """        // Admin/Auth/Logout
        public ActionResult Logout()
        {
            Session.Remove("AdminRole");
            Session.Remove("AdminEmail");
            Session.Remove("AdminUsername");
            Session.Remove("AdminAvatar");
            Session.Remove("AdminID");
            Session.Remove("UserID");"""

c = c.replace(target.replace('\r\n', '\n'), replacement.replace('\r\n', '\n'))
with open(path, 'w', encoding='utf-8') as f:
    f.write(c)
print('Updated AuthController.cs successfully.')
