using System;
using System.IO;

class Program {
    static void Main() {
        string path = "Views\\Chat\\Index.cshtml";
        string content = File.ReadAllText(path);
        
        // 1. Update updateOnlineCount
        string target1 = "function updateOnlineCount()";
        int idx1 = content.IndexOf(target1);
        if (idx1 >= 0) {
            int braceStart = content.IndexOf('{', idx1);
            int braceEnd = content.IndexOf('}', braceStart);
            string oldFunc = content.Substring(idx1, braceEnd - idx1 + 1);
            Console.WriteLine("Old func: " + oldFunc);
            
            string newFunc = @"function updateOnlineCount() {
            $.get('/ChatApi/GetOnlineCount', { roomId: rId }, function (data) {
                if (data && data.success && typeof data.count !== 'undefined') {
                    $('#onlineCount').text(data.count);
                }
            });
        }";
            content = content.Substring(0, idx1) + newFunc + content.Substring(braceEnd + 1);
            Console.WriteLine("Patched updateOnlineCount successfully.");
        }
        
        // 2. Add chat.client.updateRoomOnlineCount before chat.client.onUnauthorized
        string target2 = "chat.client.onUnauthorized = function(msg) {";
        int idx2 = content.IndexOf(target2);
        if (idx2 >= 0) {
            string eventHandler = @"chat.client.updateRoomOnlineCount = function(targetRoomId, count) {
            if (parseInt(targetRoomId) === parseInt(rId)) {
                if (typeof count !== 'undefined' && count !== null) {
                    $('#onlineCount').text(count);
                }
            }
        };

        ";
            content = content.Substring(0, idx2) + eventHandler + content.Substring(idx2);
            Console.WriteLine("Patched chat.client.updateRoomOnlineCount successfully.");
        }
        
        // 3. Update SignalR start transport & enableMessageSending
        string target3 = "if (window.WebSocket) {";
        int idx3 = content.IndexOf(target3);
        if (idx3 >= 0) {
            int elseIdx = content.IndexOf("else {", idx3);
            int endBlock = content.IndexOf('}', elseIdx);
            endBlock = content.IndexOf('}', endBlock + 1); // closing of else block
            
            string oldStart = content.Substring(idx3, endBlock - idx3 + 1);
            Console.WriteLine("Old SignalR start block length: " + oldStart.Length);
            
            string newStart = @"enableMessageSending();

        // Khởi động kết nối SignalR với cơ chế đa tầng (WebSockets -> SSE -> Long Polling)
        $.connection.hub.start({ transport: ['webSockets', 'serverSentEvents', 'longPolling'] }).done(function() {
            chat.server.joinRoom(rId, mId);
        }).fail(function() {
            console.warn('Không thể kết nối chat thời gian thực qua SignalR.');
        });";
            content = content.Substring(0, idx3) + newStart + content.Substring(endBlock + 1);
            Console.WriteLine("Patched SignalR hub start successfully.");
        }
        
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        Console.WriteLine("Views\\Chat\\Index.cshtml saved successfully.");
    }
}
