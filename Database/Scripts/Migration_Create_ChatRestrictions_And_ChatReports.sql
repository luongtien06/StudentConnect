-- =====================================================================================
-- MIGRATION SCRIPT: TẠO BẢNG ChatRestrictions VÀ ChatReports (IDEMPOTENT)
-- ÁP DỤNG CHO: Database Server (db_acf6d2_studentdb) hoặc bất kỳ database nào chưa có
-- TÁC VỤ: Tạo 2 bảng kiểm duyệt tin nhắn (Chat moderation) an toàn, không xóa dữ liệu
-- =====================================================================================

USE [db_acf6d2_studentdb];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- 1. BẢNG ChatReports (Báo cáo vi phạm tin nhắn / người dùng)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatReports]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ChatReports](
        [ReportID] [int] IDENTITY(1,1) NOT NULL,
        [ReporterID] [int] NOT NULL,
        [ReportedUserID] [int] NULL,
        [RoomID] [int] NULL,
        [MessageID] [int] NULL,
        [Reason] [nvarchar](100) NOT NULL,
        [Details] [nvarchar](1000) NULL,
        [EvidenceSnippet] [nvarchar](1000) NULL,
        [Status] [nvarchar](30) NOT NULL CONSTRAINT [DF_ChatReports_Status] DEFAULT (N'Pending'),
        [CreatedAt] [datetime] NOT NULL CONSTRAINT [DF_ChatReports_CreatedAt] DEFAULT (getdate()),
        [HandledBy] [int] NULL,
        [HandledAt] [datetime] NULL,
        [HandlingNotes] [nvarchar](1000) NULL,
        [ActionTaken] [nvarchar](100) NULL,
        CONSTRAINT [PK_ChatReports] PRIMARY KEY CLUSTERED ([ReportID] ASC)
    );
    PRINT 'Đã tạo bảng [dbo].[ChatReports] thành công.';
END
ELSE
BEGIN
    PRINT 'Bảng [dbo].[ChatReports] đã tồn tại, bỏ qua tạo mới.';
END
GO

-- 2. BẢNG ChatRestrictions (Chặn / Cấm / Giới hạn người dùng trong phòng chat)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]') AND type in (N'U'))
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
    PRINT 'Đã tạo bảng [dbo].[ChatRestrictions] thành công.';
END
ELSE
BEGIN
    PRINT 'Bảng [dbo].[ChatRestrictions] đã tồn tại, bỏ qua tạo mới.';
END
GO

-- 3. RÀNG BUỘC KHÓA NGOẠI (FOREIGN KEYS) CHO ChatReports
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatReports]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ChatReports_Reporter]') AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_Reporter] FOREIGN KEY([ReporterID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_Reporter];
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ChatReports_ReportedUser]') AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_ReportedUser] FOREIGN KEY([ReportedUserID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_ReportedUser];
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ChatReports_Handler]') AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_Handler] FOREIGN KEY([HandledBy])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_Handler];
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ChatReports_Room]') AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_Room] FOREIGN KEY([RoomID])
        REFERENCES [dbo].[ChatRooms] ([RoomID]) ON DELETE SET NULL;
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_Room];
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ChatReports_Message]') AND parent_object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
    BEGIN
        ALTER TABLE [dbo].[ChatReports] WITH CHECK ADD CONSTRAINT [FK_ChatReports_Message] FOREIGN KEY([MessageID])
        REFERENCES [dbo].[ChatMessages] ([MessageID]) ON DELETE SET NULL;
        ALTER TABLE [dbo].[ChatReports] CHECK CONSTRAINT [FK_ChatReports_Message];
    END
END
GO

-- 4. RÀNG BUỘC KHÓA NGOẠI (FOREIGN KEYS) CHO ChatRestrictions
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ChatRestrictions_User]') AND parent_object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]'))
    BEGIN
        ALTER TABLE [dbo].[ChatRestrictions] WITH CHECK ADD CONSTRAINT [FK_ChatRestrictions_User] FOREIGN KEY([UserID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatRestrictions] CHECK CONSTRAINT [FK_ChatRestrictions_User];
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ChatRestrictions_Creator]') AND parent_object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]'))
    BEGIN
        ALTER TABLE [dbo].[ChatRestrictions] WITH CHECK ADD CONSTRAINT [FK_ChatRestrictions_Creator] FOREIGN KEY([CreatedByAdminID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatRestrictions] CHECK CONSTRAINT [FK_ChatRestrictions_Creator];
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_ChatRestrictions_Revoker]') AND parent_object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]'))
    BEGIN
        ALTER TABLE [dbo].[ChatRestrictions] WITH CHECK ADD CONSTRAINT [FK_ChatRestrictions_Revoker] FOREIGN KEY([RevokedByAdminID])
        REFERENCES [dbo].[Users] ([UserID]);
        ALTER TABLE [dbo].[ChatRestrictions] CHECK CONSTRAINT [FK_ChatRestrictions_Revoker];
    END
END
GO

-- 5. CHỈ MỤC TỐI ƯU TRUY VẤN
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ChatReports_Status' AND object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ChatReports_Status] ON [dbo].[ChatReports] ([Status] ASC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ChatReports_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[ChatReports]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ChatReports_CreatedAt] ON [dbo].[ChatReports] ([CreatedAt] DESC);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_ChatRestrictions_User_Active' AND object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ChatRestrictions_User_Active] ON [dbo].[ChatRestrictions] ([UserID] ASC, [IsActive] ASC);
END
GO

PRINT 'Hoàn thành migration tạo bảng ChatReports & ChatRestrictions!';
GO
