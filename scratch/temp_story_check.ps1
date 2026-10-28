
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 1 StoryID, CreatedAt, ExpiresAt, Caption FROM dbo.Stories WHERE Caption LIKE '%[Story24h_1793173883]%'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    $c = [DateTime]$reader["CreatedAt"]
    $e = [DateTime]$reader["ExpiresAt"]
    $diff = ($e - $c).TotalHours
    Write-Host ("StoryID: " + $reader["StoryID"] + " | CreatedAt: " + $c.ToString("yyyy-MM-dd HH:mm:ss") + " | ExpiresAt: " + $e.ToString("yyyy-MM-dd HH:mm:ss") + " | Diff: " + $diff + " hours")
}
$reader.Close()
$conn.Close()
