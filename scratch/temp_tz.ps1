
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT GETDATE() AS SqlLocal, GETUTCDATE() AS SqlUtc"
$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    Write-Host ("Cloud SQL Server GETDATE(): " + $reader["SqlLocal"])
    Write-Host ("Cloud SQL Server GETUTCDATE(): " + $reader["SqlUtc"])
}
$reader.Close()
$conn.Close()

Write-Host ("Local OS TimeZone Id: " + [TimeZoneInfo]::Local.Id)
Write-Host ("Local OS TimeZone BaseUtcOffset: " + [TimeZoneInfo]::Local.BaseUtcOffset.ToString())
Write-Host ("Local Machine DateTime.Now: " + [DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss"))
Write-Host ("Local Machine DateTime.UtcNow: " + [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
