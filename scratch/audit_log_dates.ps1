$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "=== LOGS BY DATE ==="
$cmd.CommandText = @"
SELECT 
    CAST(CreatedAt AS DATE) AS LogDate,
    Status,
    COUNT(*) AS Total
FROM dbo.AgentActionLogs
GROUP BY CAST(CreatedAt AS DATE), Status
ORDER BY LogDate DESC, Status
"@
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ($reader["LogDate"].ToString() + " | Status: " + $reader["Status"].ToString() + " | Total: " + $reader["Total"].ToString())
}
$reader.Close()

Write-Host "`n=== MIN AND MAX CREATED_AT ==="
$cmd.CommandText = "SELECT MIN(CreatedAt) AS MinDate, MAX(CreatedAt) AS MaxDate, COUNT(*) AS Total FROM dbo.AgentActionLogs"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Min: " + $reader["MinDate"].ToString() + " | Max: " + $reader["MaxDate"].ToString() + " | Total: " + $reader["Total"].ToString())
}
$reader.Close()

$conn.Close()
