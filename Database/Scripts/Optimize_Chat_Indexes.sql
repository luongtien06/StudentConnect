-- =========================================================================
-- OPTIMIZATION SCRIPT: HIGH CONCURRENCY INDEXES FOR CHAT SYSTEM
-- =========================================================================

USE [TDMUEcoSystem];
GO

-- 1. Index on ChatMessages: for Room queries ordered by SentAt DESC
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_RoomID_SentAt' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatMessages_RoomID_SentAt 
    ON ChatMessages(RoomID, SentAt DESC) 
    INCLUDE (SenderID, ReplyToID, ImageUrl, IsHidden);
    PRINT 'Created index IX_ChatMessages_RoomID_SentAt';
END
GO

-- 2. Index on ChatMessages: for MessageID cursor pagination within a room
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_RoomID_MessageID' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatMessages_RoomID_MessageID 
    ON ChatMessages(RoomID, MessageID DESC) 
    INCLUDE (SenderID, SentAt, ReplyToID, ImageUrl, IsHidden);
    PRINT 'Created index IX_ChatMessages_RoomID_MessageID';
END
GO

-- 3. Index on ChatMessages: for Sender lookups & rate limiting
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_SenderID_SentAt' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatMessages_SenderID_SentAt 
    ON ChatMessages(SenderID, SentAt DESC);
    PRINT 'Created index IX_ChatMessages_SenderID_SentAt';
END
GO

-- 4. Index on ChatParticipants: for User's rooms lookup
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatParticipants_UserID_RoomID' AND object_id = OBJECT_ID('ChatParticipants'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatParticipants_UserID_RoomID 
    ON ChatParticipants(UserID, RoomID, IsLeft);
    PRINT 'Created index IX_ChatParticipants_UserID_RoomID';
END
GO

-- 5. Index on ChatParticipants: for Room membership verification
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatParticipants_RoomID_UserID' AND object_id = OBJECT_ID('ChatParticipants'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatParticipants_RoomID_UserID 
    ON ChatParticipants(RoomID, UserID, IsLeft);
    PRINT 'Created index IX_ChatParticipants_RoomID_UserID';
END
GO

-- 6. Index on ChatRooms: for RoomType + IsActive lookup
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatRooms_RoomType_IsActive' AND object_id = OBJECT_ID('ChatRooms'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatRooms_RoomType_IsActive 
    ON ChatRooms(RoomType, IsActive) 
    INCLUDE (CreatedAt);
    PRINT 'Created index IX_ChatRooms_RoomType_IsActive';
END
GO
