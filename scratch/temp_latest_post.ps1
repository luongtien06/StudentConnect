
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 1 PostID, CreatedAt, Content FROM dbo.Posts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Latest PostID: " + $reader["PostID"] + " | CreatedAt: " + [DateTime]$reader["CreatedAt"] + " | Content: " + $reader["Content"])
}
$reader.Close()
$conn.Close()
