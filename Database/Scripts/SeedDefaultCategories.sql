-- Run once on an existing production database if the web application's SQL login
-- is not allowed to insert the default categories automatically.

DECLARE @MarketCategories TABLE (CategoryName NVARCHAR(255));
INSERT INTO @MarketCategories (CategoryName) VALUES
(N'Sách & Tài liệu'),
(N'Đồ điện tử'),
(N'Đồ dùng học tập'),
(N'Thời trang'),
(N'Đồ dùng cá nhân'),
(N'Khác');

INSERT INTO MarketCategories (CategoryName)
SELECT source.CategoryName
FROM @MarketCategories AS source
WHERE NOT EXISTS (
    SELECT 1 FROM MarketCategories AS target WHERE target.CategoryName = source.CategoryName
);

DECLARE @ConnectCategories TABLE (CategoryName NVARCHAR(255));
INSERT INTO @ConnectCategories (CategoryName) VALUES
(N'Ẩm thực & Buffet'),
(N'Nước uống & Coffee'),
(N'Địa điểm vui chơi'),
(N'Hỗ trợ');

INSERT INTO ConnectCategories (CategoryName)
SELECT source.CategoryName
FROM @ConnectCategories AS source
WHERE NOT EXISTS (
    SELECT 1 FROM ConnectCategories AS target WHERE target.CategoryName = source.CategoryName
);
