using System;
using System.IO;

class Program {
    static void Main() {
        string path = "Views\\Chat\\Index.cshtml";
        string content = File.ReadAllText(path);
        
        string target = "function enableMessageSending() {";
        int idx = content.IndexOf(target);
        if (idx < 0) {
            Console.WriteLine("Target not found!");
            return;
        }
        
        string endTarget = "let _isSendingMessage = false;";
        int endIdx = content.IndexOf(endTarget, idx);
        if (endIdx < 0) {
            Console.WriteLine("End target not found!");
            return;
        }
        
        string replacement = @"function enableMessageSending() {
            $('#btnSend').off('click').on('click', sendMessage);
        }

        enableMessageSending();

        // Khởi động kết nối SignalR với cơ chế đa tầng (WebSockets -> SSE -> Long Polling)
        $.connection.hub.start({ transport: ['webSockets', 'serverSentEvents', 'longPolling'] }).done(function() {
            chat.server.joinRoom(rId, mId);
        }).fail(function() {
            console.warn('Không thể kết nối chat thời gian thực qua SignalR.');
        });

        @if (ViewBag.WaitingForPair == null ? false : (bool)ViewBag.WaitingForPair)
        {
            <text>
            var pairInterval = setInterval(function () {
                $.get('/Chat/CheckRoomReady/@ViewBag.RoomID', function (res) {
                    if (res.ready) {
                        clearInterval(pairInterval);
                        location.reload();
                    }
                });
            }, 3000);
            </text>
        }

        ";
        
        content = content.Substring(0, idx) + replacement + content.Substring(endIdx);
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        Console.WriteLine("Fixed syntax in Views\\Chat\\Index.cshtml successfully!");
    }
}
