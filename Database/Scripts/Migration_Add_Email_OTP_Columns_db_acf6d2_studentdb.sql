-- =====================================================================================
-- MIGRATION SCRIPT: BỔ SUNG CÁC CỘT XÁC MINH EMAIL OTP CHO BẢNG dbo.Users
-- Dự án: TDMUEcoSystem (StudentConnect)
-- Database đích: db_acf6d2_studentdb (sql9001.site4now.net)
-- Mục đích: Đồng bộ schema CSDL máy chủ tương thích hoàn toàn với Entity Framework model và code OTP
-- Đặc tính: IDEMPOTENT (Chạy an toàn nhiều lần, không mất dữ liệu, không sửa các cột hiện có)
-- =====================================================================================

-- LƯU Ý CHO QUẢN TRỊ VIÊN:
-- 1. Hãy mở kết nối đến máy chủ sql9001.site4now.net (database db_acf6d2_studentdb).
-- 2. Chọn đúng database [db_acf6d2_studentdb] trên trình soạn thảo SQL (SSMS / DBeaver / Site4Now Web Query).
-- 3. Thực thi script này. Script sẽ kiểm tra từng cột, chỉ thêm các cột còn thiếu và in kết quả đối soát.

SET NOCOUNT ON;

PRINT '=====================================================================================';
PRINT 'BẮT ĐẦU ĐỒNG BỘ SCHEMA CHO BẢNG dbo.Users TRÊN DATABASE: ' + DB_NAME();
PRINT 'Thời gian thực thi: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '=====================================================================================';

-- Kiểm tra bảng dbo.Users tồn tại
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    DECLARE @currentDb NVARCHAR(128) = DB_NAME();
    RAISERROR(N'LỖI: Không tìm thấy bảng dbo.Users trong database [%s]! Vui lòng chọn đúng database.', 16, 1, @currentDb);
    RETURN;
END
GO

-- -------------------------------------------------------------------------------------
-- 1. Cột IsEmailVerified: Trạng thái xác minh email sinh viên qua OTP
-- Kiểu: BIT NULL (EF: Nullable<bool>)
-- Quy ước code:
--   - NULL: Tài khoản legacy tạo trước đợt nâng cấp OTP (được phép đăng nhập bình thường)
--   - 1 (True): Đã xác thực OTP email sinh viên thành công
--   - 0 (False): Tài khoản mới đăng ký, đang chờ xác minh OTP (bị chặn đăng nhập cho đến khi nhập đúng OTP)
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'IsEmailVerified'
)
BEGIN
    ALTER TABLE dbo.Users ADD IsEmailVerified BIT NULL;
    PRINT '-> [THÊM MỚI THÀNH CÔNG] Cột IsEmailVerified (BIT NULL)';
END
ELSE
BEGIN
    PRINT '-> [ĐÃ TỒN TẠI] Cột IsEmailVerified';
END
GO

-- -------------------------------------------------------------------------------------
-- 2. Cột EmailVerifiedAt: Thời điểm tài khoản hoàn tất xác minh email sinh viên
-- Kiểu: DATETIME NULL (EF: Nullable<DateTime>)
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'EmailVerifiedAt'
)
BEGIN
    ALTER TABLE dbo.Users ADD EmailVerifiedAt DATETIME NULL;
    PRINT '-> [THÊM MỚI THÀNH CÔNG] Cột EmailVerifiedAt (DATETIME NULL)';
END
ELSE
BEGIN
    PRINT '-> [ĐÃ TỒN TẠI] Cột EmailVerifiedAt';
END
GO

-- -------------------------------------------------------------------------------------
-- 3. Cột OtpHash: Chuỗi băm SHA-256 kèm Salt và Pepper của mã OTP 6 số (Tuyệt đối không lưu plain-text)
-- Kiểu: NVARCHAR(256) NULL (EF: string)
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'OtpHash'
)
BEGIN
    ALTER TABLE dbo.Users ADD OtpHash NVARCHAR(256) NULL;
    PRINT '-> [THÊM MỚI THÀNH CÔNG] Cột OtpHash (NVARCHAR(256) NULL)';
END
ELSE
BEGIN
    PRINT '-> [ĐÃ TỒN TẠI] Cột OtpHash';
END
GO

-- -------------------------------------------------------------------------------------
-- 4. Cột OtpExpiry: Thời điểm hết hạn của mã OTP hiện tại (mặc định 5 phút kể từ khi gửi)
-- Kiểu: DATETIME NULL (EF: Nullable<DateTime>)
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'OtpExpiry'
)
BEGIN
    ALTER TABLE dbo.Users ADD OtpExpiry DATETIME NULL;
    PRINT '-> [THÊM MỚI THÀNH CÔNG] Cột OtpExpiry (DATETIME NULL)';
END
ELSE
BEGIN
    PRINT '-> [ĐÃ TỒN TẠI] Cột OtpExpiry';
END
GO

-- -------------------------------------------------------------------------------------
-- 5. Cột OtpAttempts: Số lần người dùng đã nhập sai OTP (Giới hạn tối đa 5 lần trước khi hủy mã)
-- Kiểu: INT NULL, DEFAULT 0 (EF: Nullable<int>)
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'OtpAttempts'
)
BEGIN
    ALTER TABLE dbo.Users ADD OtpAttempts INT NULL CONSTRAINT DF_Users_OtpAttempts DEFAULT ((0));
    PRINT '-> [THÊM MỚI THÀNH CÔNG] Cột OtpAttempts (INT NULL, DEFAULT 0)';
END
ELSE
BEGIN
    PRINT '-> [ĐÃ TỒN TẠI] Cột OtpAttempts';
END
GO

-- -------------------------------------------------------------------------------------
-- 6. Cột OtpLastSent: Thời điểm gửi mã OTP gần nhất (dùng để áp dụng thời gian chờ gửi lại Cooldown 60s)
-- Kiểu: DATETIME NULL (EF: Nullable<DateTime>)
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'OtpLastSent'
)
BEGIN
    ALTER TABLE dbo.Users ADD OtpLastSent DATETIME NULL;
    PRINT '-> [THÊM MỚI THÀNH CÔNG] Cột OtpLastSent (DATETIME NULL)';
END
ELSE
BEGIN
    PRINT '-> [ĐÃ TỒN TẠI] Cột OtpLastSent';
END
GO

-- =====================================================================================
-- BƯỚC ĐỐI SOÁT VÀ XÁC MINH SCHEMA SAU KHI CHẠY MIGRATION
-- =====================================================================================
PRINT '';
PRINT '=====================================================================================';
PRINT 'KẾT QUẢ ĐỐI SOÁT TOÀN BỘ 18 CỘT CỦA BẢNG dbo.Users SAU KHI ĐỒNG BỘ:';
PRINT '=====================================================================================';

SELECT 
    c.column_id AS [STT],
    c.name AS [Tên Cột],
    t.name AS [Kiểu Dữ Liệu],
    CASE 
        WHEN t.name IN ('nvarchar', 'nchar') THEN c.max_length / 2
        WHEN t.name IN ('varchar', 'char') THEN c.max_length
        ELSE NULL 
    END AS [Độ Dài],
    CASE WHEN c.is_nullable = 1 THEN 'YES' ELSE 'NO' END AS [Cho Phép NULL],
    OBJECT_DEFINITION(c.default_object_id) AS [Giá Trị Mặc Định]
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.Users')
ORDER BY c.column_id;

-- Kiểm tra số lượng người dùng và phân bổ trạng thái
SELECT 
    COUNT(*) AS [Tổng Số Tài Khoản Hiện Có],
    SUM(CASE WHEN IsEmailVerified IS NULL THEN 1 ELSE 0 END) AS [Tài Khoản Legacy (NULL - Không Bị Khóa)],
    SUM(CASE WHEN IsEmailVerified = 1 THEN 1 ELSE 0 END) AS [Tài Khoản Đã Xác Minh (1)],
    SUM(CASE WHEN IsEmailVerified = 0 THEN 1 ELSE 0 END) AS [Tài Khoản Chờ Nhập OTP (0)]
FROM dbo.Users;
GO
PRINT '=====================================================================================';
PRINT 'ĐỒNG BỘ SCHEMA BẢNG dbo.Users HOÀN TẤT AN TOÀN!';
PRINT '=====================================================================================';
