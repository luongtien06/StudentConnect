using System;
using System.IO;

class Program {
    static void Main() {
        string path = "Controllers\\ChatController.cs";
        string content = File.ReadAllText(path);
        
        string target = "if (Session[\"Avatar\"] == null) Session[\"Avatar\"] = currentUser.SafeAvatar;";
        int idx = content.IndexOf(target);
        if (idx < 0) {
            Console.WriteLine("Target not found in ChatController.cs!");
            return;
        }

        string addition = "\n\n            // Đảm bảo Cookie xác thực luôn đồng bộ để SignalR và ChatApi hoạt động trơn tru\n" +
                          "            if (UserAuthHelper.GetUserIdFromCookie(Request) != currentUser.UserID)\n" +
                          "            {\n" +
                          "                UserAuthHelper.SetAuthCookie(Response, currentUser.UserID);\n" +
                          "            }";

        content = content.Substring(0, idx + target.Length) + addition + content.Substring(idx + target.Length);
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        Console.WriteLine("Successfully synced auth cookie in ChatController.cs!");
    }
}
