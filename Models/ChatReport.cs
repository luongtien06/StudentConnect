namespace StudentConnect.Models
{
    using System;
    using System.Collections.Generic;

    public partial class ChatReport
    {
        public int ReportID { get; set; }
        public int ReporterID { get; set; }
        public Nullable<int> ReportedUserID { get; set; }
        public Nullable<int> RoomID { get; set; }
        public Nullable<int> MessageID { get; set; }
        public string Reason { get; set; }
        public string Details { get; set; }
        public string EvidenceSnippet { get; set; }
        public string Status { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public Nullable<int> HandledBy { get; set; }
        public Nullable<System.DateTime> HandledAt { get; set; }
        public string HandlingNotes { get; set; }
        public string ActionTaken { get; set; }
    }
}
