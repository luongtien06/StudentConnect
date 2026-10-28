
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "Mới nhất trong dbo.MarketPosts:"
$cmd.CommandText = "SELECT TOP 2 PostID, UserID, Title, CreatedAt FROM dbo.MarketPosts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Market PostID: " + $reader["PostID"] + " | Title: " + $reader["Title"] + " | CreatedAt: " + $reader["CreatedAt"])
}
$reader.Close()

Write-Host "`nMới nhất trong dbo.ConnectPosts:"
$cmd.CommandText = "SELECT TOP 2 PostID, UserID, Title, CreatedAt FROM dbo.ConnectPosts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Connect PostID: " + $reader["PostID"] + " | Title: " + $reader["Title"] + " | CreatedAt: " + $reader["CreatedAt"])
}
$reader.Close()
$conn.Close()
