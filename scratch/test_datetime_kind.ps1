$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()

$cmd.CommandText = "SELECT TOP 1 PostID, CreatedAt FROM dbo.Posts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    $dt = [DateTime]$reader["CreatedAt"]
    Write-Host ("PostID: " + $reader["PostID"])
    Write-Host ("CreatedAt Value: " + $dt.ToString("o"))
    Write-Host ("CreatedAt Kind: " + $dt.Kind.ToString())
}
$reader.Close()

$cmd.CommandText = "SELECT TOP 1 TaskID, NextRunAt, CreatedAt FROM dbo.AgentTasks ORDER BY TaskID DESC"
$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    $dtNext = [DateTime]$reader["NextRunAt"]
    $dtCreated = [DateTime]$reader["CreatedAt"]
    Write-Host ("`nAgentTask TaskID: " + $reader["TaskID"])
    Write-Host ("NextRunAt Kind: " + $dtNext.Kind.ToString())
    Write-Host ("CreatedAt Kind: " + $dtCreated.Kind.ToString())
}
$reader.Close()
$conn.Close()
