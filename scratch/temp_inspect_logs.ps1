
$conn = New-Object System.Data.SqlClient.SqlConnection("data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True")
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "=== COLUMNS OF dbo.AgentActionLogs ==="
$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AgentActionLogs'"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ($reader["COLUMN_NAME"] + " (" + $reader["DATA_TYPE"] + ", Nullable: " + $reader["IS_NULLABLE"] + ")")
}
$reader.Close()

Write-Host "`n=== FOREIGN KEYS ON dbo.AgentActionLogs ==="
$cmd.CommandText = @"
SELECT 
    f.name AS ForeignKey,
    OBJECT_NAME(f.parent_object_id) AS TableName,
    COL_NAME(fc.parent_object_id, fc.parent_column_id) AS ColumnName,
    OBJECT_NAME (f.referenced_object_id) AS ReferenceTableName,
    COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS ReferenceColumnName,
    f.delete_referential_action_desc
FROM sys.foreign_keys AS f
INNER JOIN sys.foreign_key_columns AS fc 
    ON f.OBJECT_ID = fc.constraint_object_id
WHERE f.parent_object_id = OBJECT_ID('dbo.AgentActionLogs')
"@
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ($reader["ForeignKey"] + ": " + $reader["ColumnName"] + " -> " + $reader["ReferenceTableName"] + "(" + $reader["ReferenceColumnName"] + ") DeleteAction: " + $reader["delete_referential_action_desc"])
}
$reader.Close()

Write-Host "`n=== FOREIGN KEYS REFERENCING dbo.AgentActionLogs ==="
$cmd.CommandText = @"
SELECT 
    f.name AS ForeignKey,
    OBJECT_NAME(f.parent_object_id) AS TableName,
    COL_NAME(fc.parent_object_id, fc.parent_column_id) AS ColumnName,
    OBJECT_NAME (f.referenced_object_id) AS ReferenceTableName,
    COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS ReferenceColumnName,
    f.delete_referential_action_desc
FROM sys.foreign_keys AS f
INNER JOIN sys.foreign_key_columns AS fc 
    ON f.OBJECT_ID = fc.constraint_object_id
WHERE f.referenced_object_id = OBJECT_ID('dbo.AgentActionLogs')
"@
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ($reader["ForeignKey"] + ": " + $reader["TableName"] + "(" + $reader["ColumnName"] + ") -> " + $reader["ReferenceTableName"] + " DeleteAction: " + $reader["delete_referential_action_desc"])
}
$reader.Close()

Write-Host "`n=== SAMPLE RECORDS IN dbo.AgentActionLogs ==="
$cmd.CommandText = "SELECT TOP 10 LogID, UserID, ActionName, TargetType, TargetID, Status, CreatedAt FROM dbo.AgentActionLogs ORDER BY LogID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("LogID: " + $reader["LogID"] + " | User: " + $reader["UserID"] + " | Action: " + $reader["ActionName"] + " | Status: " + $reader["Status"] + " | CreatedAt: " + $reader["CreatedAt"])
}
$reader.Close()

$cmd.CommandText = "SELECT COUNT(*) AS Total FROM dbo.AgentActionLogs"
$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    Write-Host ("`nTotal AgentActionLogs: " + $reader["Total"])
}
$reader.Close()

$conn.Close()
