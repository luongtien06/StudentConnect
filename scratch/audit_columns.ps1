
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "=== 1. SQL SERVER COLUMN DATA TYPES ==="
$cmd.CommandText = @"
SELECT 
    TABLE_NAME, 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE, 
    CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE COLUMN_NAME IN ('CreatedAt', 'UpdatedAt', 'ExpiresAt', 'NextRunAt', 'StartedAt', 'FinishedAt')
  AND TABLE_NAME IN ('Posts', 'MarketPosts', 'ConnectPosts', 'Stories', 'AgentTasks', 'AgentTaskRuns')
ORDER BY TABLE_NAME, COLUMN_NAME;
"@

$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ($reader["TABLE_NAME"] + "." + $reader["COLUMN_NAME"] + " -> " + $reader["DATA_TYPE"] + " (Nullable: " + $reader["IS_NULLABLE"] + ")")
}
$reader.Close()
$conn.Close()
