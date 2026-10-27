using System;
using System.IO;

class Program {
    static void Main() {
        string path = "Views\\Chat\\Index.cshtml";
        string content = File.ReadAllText(path);
        
        string broken = @"        function updateOnlineCount() {
            $.get('/ChatApi/GetOnlineCount', { roomId: rId }, function (data) {
                if (data && data.success && typeof data.count !== 'undefined') {
                    $('#onlineCount').text(data.count);
                }
            });
        }, function (data) {
                $('#onlineCount').text(data.count);
            });
        }";

        string correct = @"        function updateOnlineCount() {
            $.get('/ChatApi/GetOnlineCount', { roomId: rId }, function (data) {
                if (data && data.success && typeof data.count !== 'undefined') {
                    $('#onlineCount').text(data.count);
                }
            });
        }";

        int idx = content.IndexOf("function updateOnlineCount()");
        if (idx < 0) {
            Console.WriteLine("function updateOnlineCount() not found!");
            return;
        }

        // Find from idx until updateOnlineCount();
        string nextCall = "updateOnlineCount();";
        int idxNext = content.IndexOf(nextCall, idx);
        if (idxNext < 0) {
            Console.WriteLine("nextCall not found!");
            return;
        }

        string oldBlock = content.Substring(idx, idxNext - idx);
        Console.WriteLine("Found oldBlock:\n" + oldBlock);

        content = content.Substring(0, idx) + correct.TrimStart() + "\n        " + content.Substring(idxNext);
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        Console.WriteLine("Fixed syntax error in Views\\Chat\\Index.cshtml successfully!");
    }
}
