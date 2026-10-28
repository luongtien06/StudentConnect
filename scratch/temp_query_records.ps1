
$rawConn = "data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "================ ALL POSTS ================"
$cmd.CommandText = "SELECT PostID, UserID, CreatedAt, Content FROM dbo.Posts ORDER BY PostID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    $c = $reader["Content"].ToString()
    if ($c.Length -gt 40) { $c = $c.Substring(0, 40) }
    Write-Host ("Post #" + $reader["PostID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | " + $c)
}
$reader.Close()

Write-Host "`n================ ALL MARKET POSTS ================"
$cmd.CommandText = "SELECT PostID, UserID, CreatedAt, Title, Price FROM dbo.MarketPosts ORDER BY PostID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("MarketPost #" + $reader["PostID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | Title: " + $reader["Title"] + " | Price: " + $reader["Price"])
}
$reader.Close()

Write-Host "`n================ ALL CONNECT POSTS ================"
$cmd.CommandText = "SELECT PostID, UserID, CreatedAt, Title FROM dbo.ConnectPosts ORDER BY PostID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("ConnectPost #" + $reader["PostID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | Title: " + $reader["Title"])
}
$reader.Close()

Write-Host "`n================ ALL STORIES ================"
$cmd.CommandText = "SELECT StoryID, UserID, CreatedAt, ExpiresAt, Caption FROM dbo.Stories ORDER BY StoryID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Story #" + $reader["StoryID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | ExpiresAt: " + $reader["ExpiresAt"] + " | Caption: " + $reader["Caption"])
}
$reader.Close()

Write-Host "`n================ AGENT TASKS (SCHEDULER) ================"
$cmd.CommandText = "SELECT TaskID, UserID, TaskName, ScheduleType, ScheduleConfig, TimeZone, Status, NextRunAt, LastRunAt, CreatedAt FROM dbo.AgentTasks ORDER BY TaskID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Task #" + $reader["TaskID"] + " | Name: " + $reader["TaskName"] + " | Type: " + $reader["ScheduleType"] + " | TZ: " + $reader["TimeZone"] + " | Status: " + $reader["Status"] + " | NextRunAt: " + $reader["NextRunAt"] + " | CreatedAt: " + $reader["CreatedAt"])
}
$reader.Close()

Write-Host "`n================ AGENT TASK RUNS ================"
$cmd.CommandText = "SELECT TOP 10 RunID, TaskID, Status, StartedAt, FinishedAt, CreatedAt FROM dbo.AgentTaskRuns ORDER BY RunID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("Run #" + $reader["RunID"] + " | Task #" + $reader["TaskID"] + " | Status: " + $reader["Status"] + " | StartedAt: " + $reader["StartedAt"] + " | FinishedAt: " + $reader["FinishedAt"] + " | CreatedAt: " + $reader["CreatedAt"])
}
$reader.Close()

$conn.Close()
