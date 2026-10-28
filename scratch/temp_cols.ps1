$conn = New-Object System.Data.SqlClient.SqlConnection("data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 1 LogID, ActionName, TargetType, Status, CreatedAt, InputSummary, ResultSummary FROM dbo.AgentActionLogs"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Output "LogID: $($reader['LogID'])"
    Write-Output "ActionName: $($reader['ActionName'])"
    Write-Output "TargetType: $($reader['TargetType'])"
    Write-Output "Status: $($reader['Status'])"
    Write-Output "CreatedAt: $($reader['CreatedAt'])"
    Write-Output "InputSummary: $($reader['InputSummary'])"
    Write-Output "ResultSummary: $($reader['ResultSummary'])"
}
$reader.Close()
$conn.Close()
