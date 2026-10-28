$connStr = "data source=localhost\MSSQLSERVER01;initial catalog=TDMUEcoSystem;user id=sa;password=260126;MultipleActiveResultSets=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 10 LogID, ActionName, CreatedAt, Status FROM dbo.AgentActionLogs ORDER BY LogID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Output "$($reader['LogID']) | $($reader['ActionName']) | $($reader['CreatedAt']) | $($reader['Status'])"
}
$reader.Close()
$conn.Close()
