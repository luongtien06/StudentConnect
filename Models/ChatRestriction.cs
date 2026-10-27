namespace StudentConnect.Models
{
    using System;
    using System.Collections.Generic;

    public partial class ChatRestriction
    {
        public int RestrictionID { get; set; }
        public int UserID { get; set; }
        public string RestrictionType { get; set; }
        public string Reason { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public Nullable<System.DateTime> ExpiresAt { get; set; }
        public int CreatedByAdminID { get; set; }
        public bool IsActive { get; set; }
        public Nullable<System.DateTime> RevokedAt { get; set; }
        public Nullable<int> RevokedByAdminID { get; set; }
        public string RevokeReason { get; set; }
    }
}
