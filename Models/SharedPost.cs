namespace StudentConnect.Models
{
    using System;
    using System.Collections.Generic;

    public partial class SharedPost
    {
        public int ShareID { get; set; }
        public string ItemType { get; set; }
        public int PostID { get; set; }
        public int UserID { get; set; }
        public string Content { get; set; }
        public System.DateTime CreatedAt { get; set; }

        public virtual User User { get; set; }
    }
}
