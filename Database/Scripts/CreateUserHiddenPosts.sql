-- Script to create UserHiddenPosts table for per-user hidden posts
-- Run this against the application's database (TDMUEcoSystemEntities connection)

IF OBJECT_ID('dbo.UserHiddenPosts', 'U') IS NOT NULL
BEGIN
	PRINT 'Table UserHiddenPosts already exists.';
END
ELSE
BEGIN
	CREATE TABLE dbo.UserHiddenPosts
	(
		Id INT IDENTITY(1,1) PRIMARY KEY,
		UserID INT NOT NULL,
		PostID INT NOT NULL,
		HiddenAt DATETIME NOT NULL DEFAULT(GETDATE())
	);

	CREATE NONCLUSTERED INDEX IX_UserHiddenPosts_User_Post ON dbo.UserHiddenPosts(UserID, PostID);

	PRINT 'Table UserHiddenPosts created.';
END
