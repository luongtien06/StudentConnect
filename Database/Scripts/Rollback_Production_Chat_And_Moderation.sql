-- =====================================================================================
-- KẾ HOẠCH ROLLBACK TOÀN DIỆN CHO PRODUCTION (BẢO VỆ DỮ LIỆU TỐI ĐA)
-- CƠ SỞ DỮ LIỆU ĐÍCH: db_acf6d2_studentdb (sql9001.site4now.net)
-- =====================================================================================
-- 
-- ── PHƯƠNG ÁN 1 (KHUYẾN NGHỊ CAO NHẤT): ROLLBACK ỨNG DỤNG - KHÔNG CHẠM SCHEMA ────────
-- Nếu phát hiện lỗi nghiệp vụ sau khi deploy:
-- 1. Khôi phục file nhị phân cũ: Copy 'bin/StudentConnect.dll.bak' đè lại 'bin/StudentConnect.dll'.
-- 2. Khôi phục file view cũ: 'Areas/Admin/Views/AdminChat/Index.cshtml'.
-- 3. Chạm nhẹ 'Web.config' (hoặc Recycle AppPool) để nạp lại phiên bản cũ.
-- * KẾT QUẢ: Ứng dụng hoạt động lại bình thường ngay lập tức.
-- * AN TOÀN: KHÔNG MẤT DỮ LIỆU. Do EF6 Database First tự động bỏ qua các bảng và cột thừa
--   trong DB mà model cũ không dùng đến, nên việc giữ nguyên schema mới là 100% tương thích ngược!
--
-- ── PHƯƠNG ÁN 2 (HẠ CẤP SCHEMA TRIỆT ĐỂ - CHỈ DÙNG KHI CÓ SỰ CỐ DATABASE) ──────────────
-- Script bên dưới chỉ dùng khi DBA thực sự muốn gỡ bỏ hoàn toàn các bảng và cột mới.
-- Script đã được trang bị khóa an toàn chống xóa nhầm dữ liệu đang có.
-- =====================================================================================

USE [db_acf6d2_studentdb];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- KHÓA AN TOÀN: MẶC ĐỊNH = 0 ĐỂ CHỐNG CHẠY NHẦM
-- Nếu thực sự chấp nhận xóa bỏ toàn bộ báo cáo vi phạm và lệnh cấm chat, DBA đổi thành 1:
DECLARE @ConfirmDataLoss INT = 0;

PRINT '>>> BẮT ĐẦU KIỂM TRA ĐIỀU KIỆN AN TOÀN TRƯỚC KHI ROLLBACK SCHEMA...';

-- 1. KIỂM TRA DỮ LIỆU TRONG ChatReports
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatReports]') AND type in (N'U'))
BEGIN
    DECLARE @ReportCount INT = 0;
    SELECT @ReportCount = COUNT(*) FROM [dbo].[ChatReports];
    IF @ReportCount > 0 AND @ConfirmDataLoss = 0
    BEGIN
        RAISERROR(N'DỪNG KHẨN CẤP: Bảng [dbo].[ChatReports] đang chứa %d bản ghi tố cáo vi phạm! Nếu xóa sẽ mất toàn bộ lịch sử kiểm duyệt. Hãy sao lưu bảng trước hoặc đặt @ConfirmDataLoss = 1 để tiếp tục.', 16, 1, @ReportCount);
        RETURN;
    END
END

-- 2. KIỂM TRA DỮ LIỆU TRONG ChatRestrictions
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]') AND type in (N'U'))
BEGIN
    DECLARE @RestCount INT = 0;
    SELECT @RestCount = COUNT(*) FROM [dbo].[ChatRestrictions];
    IF @RestCount > 0 AND @ConfirmDataLoss = 0
    BEGIN
        RAISERROR(N'DỪNG KHẨN CẤP: Bảng [dbo].[ChatRestrictions] đang chứa %d lệnh cấm/tắt tiếng! Nếu xóa sẽ mất toàn bộ quyết định kỷ luật của BQT. Hãy đặt @ConfirmDataLoss = 1 để tiếp tục.', 16, 1, @RestCount);
        RETURN;
    END
END

PRINT '>>> ĐÃ XÁC MINH ĐIỀU KIỆN. BẮT ĐẦU THÁO DỠ CÁC ĐỐI TƯỢNG MODERATION...';

-- 3. XÓA CÁC CHỈ MỤC TỐI ƯU ĐÃ TẠO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_RoomID_MessageID' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    DROP INDEX IX_ChatMessages_RoomID_MessageID ON [dbo].[ChatMessages];
    PRINT '  - [DROP INDEX] IX_ChatMessages_RoomID_MessageID';
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatMessages_RoomID_SentAt' AND object_id = OBJECT_ID('ChatMessages'))
BEGIN
    DROP INDEX IX_ChatMessages_RoomID_SentAt ON [dbo].[ChatMessages];
    PRINT '  - [DROP INDEX] IX_ChatMessages_RoomID_SentAt';
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ChatParticipants_RoomID_UserID' AND object_id = OBJECT_ID('ChatParticipants'))
BEGIN
    DROP INDEX IX_ChatParticipants_RoomID_UserID ON [dbo].[ChatParticipants];
    PRINT '  - [DROP INDEX] IX_ChatParticipants_RoomID_UserID';
END

-- 4. XÓA BẢNG ChatRestrictions
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatRestrictions]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[ChatRestrictions];
    PRINT '  - [DROP TABLE] [dbo].[ChatRestrictions] thành công.';
END

-- 5. XÓA BẢNG ChatReports
IF EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatReports]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[ChatReports];
    PRINT '  - [DROP TABLE] [dbo].[ChatReports] thành công.';
END

-- 6. GỠ BỎ RÀNG BUỘC VÀ 4 CỘT MODERATION TRÊN [dbo].[ChatMessages]
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_ChatMessages_IsHidden' AND parent_object_id = OBJECT_ID(N'[dbo].[ChatMessages]'))
BEGIN
    ALTER TABLE [dbo].[ChatMessages] DROP CONSTRAINT [DF_ChatMessages_IsHidden];
    PRINT '  - [DROP CONSTRAINT] DF_ChatMessages_IsHidden';
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND name = 'HiddenAt')
BEGIN
    ALTER TABLE [dbo].[ChatMessages] DROP COLUMN [HiddenAt];
    PRINT '  - [DROP COLUMN] HiddenAt';
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND name = 'HiddenBy')
BEGIN
    ALTER TABLE [dbo].[ChatMessages] DROP COLUMN [HiddenBy];
    PRINT '  - [DROP COLUMN] HiddenBy';
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND name = 'HiddenReason')
BEGIN
    ALTER TABLE [dbo].[ChatMessages] DROP COLUMN [HiddenReason];
    PRINT '  - [DROP COLUMN] HiddenReason';
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ChatMessages]') AND name = 'IsHidden')
BEGIN
    ALTER TABLE [dbo].[ChatMessages] DROP COLUMN [IsHidden];
    PRINT '  - [DROP COLUMN] IsHidden';
END

PRINT '=====================================================================================';
PRINT '>>> HOÀN THÀNH ROLLBACK SCHEMA. CƠ SỞ DỮ LIỆU ĐÃ TRỞ VỀ NGUYÊN TRẠNG BAN ĐẦU.';
PRINT '=====================================================================================';
GO
