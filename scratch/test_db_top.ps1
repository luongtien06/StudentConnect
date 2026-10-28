$connStr = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 5 LogID, ActionName FROM dbo.AgentActionLogs ORDER BY LogID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Output "$($reader['LogID']) | $($reader['ActionName'])"
}
$reader.Close()
$conn.Close()
