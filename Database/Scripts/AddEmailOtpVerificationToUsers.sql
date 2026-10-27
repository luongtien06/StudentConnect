-- ===================================================================
-- Migration: Add Email OTP Verification Columns to Users Table
-- Project: TDMUEcoSystem (StudentConnect)
-- ===================================================================

-- Note: Ensure you are connected to the correct database context before running.
-- For local: USE TDMUEcoSystem;
-- For server: USE db_acf6d2_studentdb;
-- GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'IsEmailVerified')
BEGIN
    ALTER TABLE dbo.Users ADD IsEmailVerified BIT NULL;
    PRINT 'Added IsEmailVerified column to Users';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'EmailVerifiedAt')
BEGIN
    ALTER TABLE dbo.Users ADD EmailVerifiedAt DATETIME NULL;
    PRINT 'Added EmailVerifiedAt column to Users';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'OtpHash')
BEGIN
    ALTER TABLE dbo.Users ADD OtpHash NVARCHAR(256) NULL;
    PRINT 'Added OtpHash column to Users';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'OtpExpiry')
BEGIN
    ALTER TABLE dbo.Users ADD OtpExpiry DATETIME NULL;
    PRINT 'Added OtpExpiry column to Users';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'OtpAttempts')
BEGIN
    ALTER TABLE dbo.Users ADD OtpAttempts INT NULL CONSTRAINT DF_Users_OtpAttempts DEFAULT ((0));
    PRINT 'Added OtpAttempts column to Users';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'OtpLastSent')
BEGIN
    ALTER TABLE dbo.Users ADD OtpLastSent DATETIME NULL;
    PRINT 'Added OtpLastSent column to Users';
END
GO
