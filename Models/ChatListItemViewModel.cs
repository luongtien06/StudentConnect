using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StudentConnect.Models
{
    public class ChatListItemViewModel
    {
        public int RoomID { get; set; }
        public string RoomType { get; set; }
        public User OtherUser { get; set; }      // null nếu là phòng Group
        public string RoomName { get; set; }     // dùng cho phòng Group
        public DateTime? LastMsgAt { get; set; }
        public string LastMessage { get; set; }  // preview tin nhắn cuối
        public int MemberCount { get; set; }
        public bool IsCurrent { get; set; }
    }
}