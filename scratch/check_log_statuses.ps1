$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Status, COUNT(*) AS Total FROM dbo.AgentActionLogs GROUP BY Status"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ($reader["Status"].ToString() + ": " + $reader["Total"].ToString())
}
$reader.Close()
$conn.Close()
