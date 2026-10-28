
$conn = New-Object System.Data.SqlClient.SqlConnection("data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 1 StoryID, CreatedAt, ExpiresAt, Caption FROM dbo.Stories WHERE Caption LIKE '%[Story_TZ_1793173746]%'"
$reader = $cmd.ExecuteReader()
$results = @()
while ($reader.Read()) {
    $row = @{}
    for ($i = 0; $i -lt $reader.FieldCount; $i++) {
        $row[$reader.GetName($i)] = $reader.GetValue($i).ToString()
    }
    $results += (New-Object PSObject -Property $row)
}
$reader.Close()
$conn.Close()
$results | ConvertTo-Json -Compress
