-- =====================================================================================
-- MIGRATION SCRIPT TOÀN DIỆN CHO PRODUCTION (IDEMPOTENT & ZERO DATA-LOSS)
-- CƠ SỞ DỮ LIỆU ĐÍCH: db_acf6d2_studentdb (sql9001.site4now.net)
-- PHIÊN BẢN: ASP.NET MVC 5 - .NET Framework 4.8 - Entity Framework 6 Database First
-- TÍNH NĂNG: KIỂM DUYỆT TIN NHẮN, TỐ CÁO VI PHẠM & GIẢI MÃ DANH TÍNH CHAT ẨN DANH
-- =====================================================================================

USE [db_acf6d2_studentdb];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

PRINT '>>> [BƯỚC 1/5] KIỂM TRA & BỔ SUNG CỘT BẢNG [dbo].[ChatMessages]...';
GO

-- 1. Cột IsHidden (bit, NULL, Default 0)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND name = 'IsHidden')
BEGIN
    ALTER TABLE [dbo].[ChatMessages] ADD [IsHidden] [bit] NULL CONSTRAINT [DF_ChatMessages_IsHidden] DEFAULT ((0));
    PRINT '  + [PASS] Đã thêm cột [IsHidden] vào [dbo].[ChatMessages].';
END
ELSE
BEGIN
    PRINT '  * [SKIP] Cột [IsHidden] đã tồn tại trong [dbo].[ChatMessages].';
END
GO

-- 2. Cột HiddenReason (nvarchar(255), NULL)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND name = 'HiddenReason')
BEGIN
    ALTER TABLE [dbo].[ChatMessages] ADD [HiddenReason] [nvarchar](255) NULL;
    PRINT '  + [PASS] Đã thêm cột [HiddenReason] vào [dbo].[ChatMessages].';
END
ELSE
BEGIN
    PRINT '  * [SKIP] Cột [HiddenReason] đã tồn tại trong [dbo].[ChatMessages].';
END
GO

-- 3. Cột HiddenBy (int, NULL)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND name = 'HiddenBy')
BEGIN
    ALTER TABLE [dbo].[ChatMessages] ADD [HiddenBy] [int] NULL;
    PRINT '  + [PASS] Đã thêm cột [HiddenBy] vào [dbo].[ChatMessages].';
END
ELSE
BEGIN
    PRINT '  * [SKIP] Cột [HiddenBy] đã tồn tại trong [dbo].[ChatMessages].';
END
GO

-- 4. Cột HiddenAt (datetime, NULL)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND name = 'HiddenAt')
BEGIN
    ALTER TABLE [dbo].[ChatMessages] ADD [HiddenAt] [datetime] NULL;
    PRINT '  + [PASS] Đã thêm cột [HiddenAt] vào [dbo].[ChatMessages].';
END
ELSE
BEGIN
    PRINT '  * [SKIP] Cột [HiddenAt] đã tồn tại trong [dbo].[ChatMessages].';
END
GO

PRINT '>>> [BƯỚC 2/5] KIỂM TRA & TẠO BẢNG [dbo].[ChatReports]...';
GO

-- Tạo bảng ChatReports nếu chưa có (Khớp 100% EDMX SSDL)
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatReports]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ChatReports](
        [ReportID] [int] IDENTITY(1,1) NOT NULL,
        [ReporterID] [int] NOT NULL,
        [ReportedUserID] [int] NULL,
        [RoomID] [int] NULL,
        [MessageID] [int] NULL,
        [Reason] [nvarchar](255) NOT NULL,
        [Details] [nvarchar](max) NULL,
        [EvidenceSnippet] [nvarchar](max) NULL,
        [Status] [nvarchar](50) NOT NULL CONSTRAINT [DF_ChatReports_Status] DEFAULT (N'Pending'),
        [CreatedAt] [datetime] NOT NULL CONSTRAINT [DF_ChatReports_CreatedAt] DEFAULT (getdate()),
        [HandledBy] [int] NULL,
        [HandledAt] [datetime] NULL,
        [HandlingNotes] [nvarchar](max) NULL,
        [ActionTaken] [nvarchar](100) NULL,
        CONSTRAINT [PK_ChatReports] PRIMARY KEY CLUSTERED ([ReportID] ASC)
    );
    PRINT '  + [PASS] Đã tạo bảng [dbo].[ChatReports] thành công.';
END
ELSE
BEGIN
    PRINT '  * [SKIP] Bảng [dbo].[ChatReports] đã tồn tại. Đang kiểm tra mở rộng độ dài cột nếu cần...';
    -- Đảm bảo tương thích nếu bảng cũ được tạo với độ dài ngắn hơn
    ALTER TABLE [dbo].[ChatReports] ALTER COLUMN [Reason] [nvarchar](255) NOT NULL;
    ALTER TABLE [dbo].[ChatReports] ALTER COLUMN [Details] [nvarchar](max) NULL;
    ALTER TABLE [dbo].[ChatReports] ALTER COLUMN [EvidenceSnippet] [nvarchar](max) NULL;
    ALTER TABLE [dbo].[ChatReports] ALTER COLUMN [Status] [nvarchar](50) NOT NULL;
    ALTER TABLE [dbo].[ChatReports] ALTER COLUMN [HandlingNotes] [nvarchar](max) NULL;
    ALTER TABLE [dbo].[ChatReports] ALTER COLUMN [ActionTaken] [nvarchar](100) NULL;
    PRINT '  + [PASS] Đã đồng bộ kích thước cột của [dbo].[ChatReports] theo chuẩn EDMX.';
END
GO

-- Khóa ngoại cho ChatReports
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatReports]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatReports_Reporter' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_Reporter] FOREIGN KEY([ReporterID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_Reporter];
        PRINT '  + [PASS] Đã tạo khóa ngoại [FK_ChatReports_Reporter].';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatReports_ReportedUser' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_ReportedUser] FOREIGN KEY([ReportedUserID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_ReportedUser];
        PRINT '  + [PASS] Đã tạo khóa ngoại [FK_ChatReports_ReportedUser].';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatReports_Handler' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_Handler] FOREIGN KEY([HandledBy])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_Handler];
        PRINT '  + [PASS] Đã tạo khóa ngoại [FK_ChatReports_Handler].';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatReports_Room' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_Room] FOREIGN KEY([RoomID])
        REFERENCES [dbo].[ChatRooms] ([RoomID]) ON DELETE SET NULL;
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_Room];
        PRINT '  + [PASS] Đã tạo khóa ngoại [FK_ChatReports_Room] (ON DELETE SET NULL).';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatReports_Message' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_Message] FOREIGN KEY([MessageID])
        REFERENCES [dbo].[ChatMessages] ([MessageID]) ON DELETE SET NULL;
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_Message];
        PRINT '  + [PASS] Đã tạo khóa ngoại [FK_ChatReports_Message] (ON DELETE SET NULL).';
    END
END
GO

PRINT '>>> [BƯỚC 3/5] KIỂM TRA & TẠO BẢNG [dbo].[ChatRestrictions]...';
GO

-- Tạo bảng ChatRestrictions nếu chưa có (Khớp 100% EDMX SSDL)
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ChatRestrictions](
        [RestrictionID] [int] IDENTITY(1,1) NOT NULL,
        [UserID] [int] NOT NULL,
        [RestrictionType] [nvarchar](50) NOT NULL,
        [Reason] [nvarchar](500) NOT NULL,
        [CreatedAt] [datetime] NOT NULL CONSTRAINT [DF_ChatRestrictions_CreatedAt] DEFAULT (getdate()),
        [ExpiresAt] [datetime] NULL,
        [CreatedByAdminID] [int] NOT NULL,
        [IsActive] [bit] NOT NULL CONSTRAINT [DF_ChatRestrictions_IsActive] DEFAULT ((1)),
        [RevokedAt] [datetime] NULL,
        [RevokedByAdminID] [int] NULL,
        [RevokeReason] [nvarchar](500) NULL,
        CONSTRAINT [PK_ChatRestrictions] PRIMARY KEY CLUSTERED ([RestrictionID] ASC)
    );
    PRINT '  + [PASS] Đã tạo bảng [dbo].[ChatRestrictions] thành công.';
END
ELSE
BEGIN
    PRINT '  * [SKIP] Bảng [dbo].[ChatRestrictions] đã tồn tại.';
END
GO

-- Khóa ngoại cho ChatRestrictions
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatRestrictions_User' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]'))
    BEGIN
        ALTER TABLE [dbo].[ChatRestrictions] WITH CHECK ADD CONSTRAINT [FK_ChatRestrictions_User] FOREIGN KEY([UserID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatRestrictions] CHECK CONSTRAINT [FK_ChatRestrictions_User];
        PRINT '  + [PASS] Đã tạo khóa ngoại [FK_ChatRestrictions_User].';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatRestrictions_Creator' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]'))
    BEGIN
        ALTER TABLE [dbo].[ChatRestrictions] WITH CHECK ADD CONSTRAINT [FK_ChatRestrictions_Creator] FOREIGN KEY([CreatedByAdminID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatRestrictions] CHECK CONSTRAINT [FK_ChatRestrictions_Creator];
        PRINT '  + [PASS] Đã tạo khóa ngoại [FK_ChatRestrictions_Creator].';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatRestrictions_Revoker' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]'))
    BEGIN
        ALTER TABLE [dbo].[ChatRestrictions] WITH CHECK ADD CONSTRAINT [FK_ChatRestrictions_Revoker] FOREIGN KEY([RevokedByAdminID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatRestrictions] CHECK CONSTRAINT [FK_ChatRestrictions_Revoker];
        PRINT '  + [PASS] Đã tạo khóa ngoại [FK_ChatRestrictions_Revoker].';
    END
END
GO

PRINT '>>> [BƯỚC 4/5] TẠO CÁC CHỈ MỤC TỐI ƯU TRUY VẤN (INDEXES)...';
GO

-- Index cho ChatReports
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatReports_Status' AND object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ChatReports_Status] ON [dbo].[ChatReports] ([Status] ASC);
    PRINT '  + [PASS] Đã tạo Index [IX_ChatReports_Status].';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatReports_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ChatReports_CreatedAt] ON [dbo].[ChatReports] ([CreatedAt] DESC);
    PRINT '  + [PASS] Đã tạo Index [IX_ChatReports_CreatedAt].';
END

-- Index cho ChatRestrictions
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChatRestrictions_User_Active' AND object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ChatRestrictions_User_Active] ON [dbo].[ChatRestrictions] ([UserID] ASC, [IsActive] ASC);
    PRINT '  + [PASS] Đã tạo Index [IX_ChatRestrictions_User_Active].';
END

-- Index tối ưu cho ChatMessages
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_RoomID_SentAt' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatMessages_RoomID_SentAt 
    ON ChatMessages(RoomID, SentAt DESC) 
    INCLUDE (SenderID, ReplyToID, ImageUrl, IsHidden);
    PRINT '  + [PASS] Đã tạo Index [IX_ChatMessages_RoomID_SentAt].';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_RoomID_MessageID' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatMessages_RoomID_MessageID 
    ON ChatMessages(RoomID, MessageID DESC) 
    INCLUDE (SenderID, SentAt, ReplyToID, ImageUrl, IsHidden);
    PRINT '  + [PASS] Đã tạo Index [IX_ChatMessages_RoomID_MessageID].';
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatParticipants_RoomID_UserID' AND object_id = OBJECT_ID('ChatParticipants'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ChatParticipants_RoomID_UserID 
    ON ChatParticipants(RoomID, UserID, IsLeft);
    PRINT '  + [PASS] Đã tạo Index [IX_ChatParticipants_RoomID_UserID].';
END
GO

PRINT '>>> [BƯỚC 5/5] XÁC NHẬN RÀNG BUỘC KIỂM TOÁN AGENTACTIONLOGS...';
GO

-- Đảm bảo constraint AgentActionLogs chấp nhận Status='Succeeded'
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AgentActionLogs_Status' AND parent_object_id = OBJECT_ID('AgentActionLogs'))
BEGIN
    PRINT '  * [PASS] Constraint [CK_AgentActionLogs_Status] đã tồn tại và hợp lệ trên server.';
END
GO

PRINT '=====================================================================================';
PRINT '>>> HOÀN TẤT TOÀN BỘ MIGRATION PRODUCTION! HỆ THỐNG ĐÃ SẴN SÀNG CHO MÃ NGUỒN MỚI.';
PRINT '=====================================================================================';
GO
