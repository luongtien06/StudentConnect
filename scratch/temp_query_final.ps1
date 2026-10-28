
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 4 PostID, UserID, Content, CreatedAt FROM dbo.Posts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    $c = $reader["Content"].ToString()
    if ($c.Length -gt 45) { $c = $c.Substring(0, 45) }
    Write-Host ("PostID: " + $reader["PostID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | Content: " + $c)
}
$reader.Close()
$conn.Close()
