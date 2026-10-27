using System;
using System.Collections.Generic;

namespace StudentConnect.Models
{
    public class AdminChatViewModel
    {
        public bool IsSuperAdmin { get; set; }
        public ChatOverviewStats Stats { get; set; } = new ChatOverviewStats();
        public List<ChatRoom> Rooms { get; set; } = new List<ChatRoom>();
        public List<ChatMessageItemViewModel> RecentMessages { get; set; } = new List<ChatMessageItemViewModel>();
        public List<ChatReportItemViewModel> Reports { get; set; } = new List<ChatReportItemViewModel>();
        public List<ChatMemberItemViewModel> Members { get; set; } = new List<ChatMemberItemViewModel>();
        public List<ChatRestrictionItemViewModel> ActiveRestrictions { get; set; } = new List<ChatRestrictionItemViewModel>();
    }

    public class ChatOverviewStats
    {
        public int TotalRooms { get; set; }
        public int ActiveRooms { get; set; }
        public int ClosedRooms { get; set; }
        public int Rooms1vs1 { get; set; }
        public int RoomsAnonymous { get; set; }
        public int RoomsGroup { get; set; }

        public int TotalMessages { get; set; }
        public int MessagesToday { get; set; }
        public int MessagesThisWeek { get; set; }
        public int MessagesThisMonth { get; set; }
        public int TotalActiveUsers { get; set; }

        public int TotalReports { get; set; }
        public int PendingReports { get; set; }
        public int ProcessingReports { get; set; }
        public int ResolvedReports { get; set; }
        public int RejectedReports { get; set; }

        public int ActiveBannedUsersCount { get; set; }
        public int ActiveMutedUsersCount { get; set; }

        public List<DailyTrendItem> DailyTrends { get; set; } = new List<DailyTrendItem>();
    }

    public class DailyTrendItem
    {
        public string DateLabel { get; set; }
        public int MessageCount { get; set; }
        public int ActiveUsers { get; set; }
    }

    public class ChatMessageItemViewModel
    {
        public int MessageID { get; set; }
        public int? RoomID { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public int? SenderID { get; set; }
        public string SenderUsername { get; set; }
        public string SenderDisplayName { get; set; }
        public string SenderAvatar { get; set; }
        public string RawContent { get; set; }
        public string DisplayText { get; set; }
        public bool IsAnonymousMsg { get; set; }
        public bool HasAttachments { get; set; }
        public int AttachmentsCount { get; set; }
        public List<ChatAttachmentItemInfo> AttachmentItems { get; set; } = new List<ChatAttachmentItemInfo>();
        public string ImageUrl { get; set; }
        public DateTime? SentAt { get; set; }
        public string SentAtFormatted { get; set; }
        public bool IsHidden { get; set; }
        public string HiddenReason { get; set; }
        public int? HiddenBy { get; set; }
        public string HiddenByName { get; set; }
        public DateTime? HiddenAt { get; set; }
        public int? ReplyToID { get; set; }
    }

    public class ChatAttachmentItemInfo
    {
        public string FileName { get; set; }
        public string Extension { get; set; }
        public long FileSize { get; set; }
        public string FormattedSize { get; set; }
        public bool IsImage { get; set; }
        public string Url { get; set; }
        public string FileKey { get; set; }
        public string ContentType { get; set; }
    }

    public class ChatReportItemViewModel
    {
        public int ReportID { get; set; }
        public int ReporterID { get; set; }
        public string ReporterName { get; set; }
        public string ReporterDisplayName { get; set; }
        public string ReporterAvatar { get; set; }
        public int? ReportedUserID { get; set; }
        public string ReportedUserName { get; set; }
        public string ReportedUserDisplayName { get; set; }
        public string ReportedUserAvatar { get; set; }
        public int? RoomID { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public int? MessageID { get; set; }
        public string Reason { get; set; }
        public string Details { get; set; }
        public string EvidenceSnippet { get; set; }
        public string Status { get; set; } // 'Pending', 'Processing', 'Resolved', 'Rejected'
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted { get; set; }
        public int? HandledBy { get; set; }
        public string HandledByName { get; set; }
        public DateTime? HandledAt { get; set; }
        public string HandlingNotes { get; set; }
        public string ActionTaken { get; set; }

        // ── THÔNG TIN DANH TÍNH THẬT CHO ADMIN ──
        public string ReportedUserEmail { get; set; }
        public string ReportedUserRealUsername { get; set; }
        public string ReportedUserRealDisplayName { get; set; }
        public bool IsReportedUserAnonymous { get; set; }
        public string AnonymousAlias { get; set; }
        public string IdentityStatus { get; set; } // "Đã xác định" hoặc "Chưa xác định danh tính"
        public string MessageContent { get; set; }
        public string MessageSentAtFormatted { get; set; }
    }

    public class ChatMemberItemViewModel
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string Avatar { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsTDMUStudent { get; set; }
        public int TotalRoomsJoined { get; set; }
        public int TotalMessagesSent { get; set; }
        public int TotalReportsReceived { get; set; }
        public bool IsBanned { get; set; }
        public bool IsMuted { get; set; }
        public DateTime? JoinedAt { get; set; }
        public DateTime? LastActiveAt { get; set; }
        public ChatRestrictionItemViewModel ActiveRestriction { get; set; }
        public List<ChatRoomSummaryItem> JoinedRooms { get; set; } = new List<ChatRoomSummaryItem>();
        public List<ChatRestrictionItemViewModel> RestrictionHistory { get; set; } = new List<ChatRestrictionItemViewModel>();
    }

    public class ChatRoomSummaryItem
    {
        public int RoomID { get; set; }
        public string RoomName { get; set; }
        public string RoomType { get; set; }
        public DateTime? JoinedAt { get; set; }
        public bool IsLeft { get; set; }
    }

    public class ChatRestrictionItemViewModel
    {
        public int RestrictionID { get; set; }
        public int UserID { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Avatar { get; set; }
        public string RestrictionType { get; set; } // 'Warn', 'Mute', 'Ban'
        public string Reason { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string ExpiresAtFormatted { get; set; }
        public bool IsPermanent { get; set; }
        public bool IsExpired { get; set; }
        public int CreatedByAdminID { get; set; }
        public string CreatedByAdminName { get; set; }
        public bool IsActive { get; set; }
        public DateTime? RevokedAt { get; set; }
        public int? RevokedByAdminID { get; set; }
        public string RevokedByAdminName { get; set; }
        public string RevokeReason { get; set; }
    }
}
