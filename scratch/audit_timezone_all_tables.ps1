$connStr = "Server=db16147.site4now.net;Database=db_aa0d6e_tdmuecosystem;Uid=db_aa0d6e_tdmuecosystem_admin;Pwd=TDmuEcoSystem2026@Pass;Encrypt=no;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "================ ALL POSTS (OLD VS NEW) ================"
$cmd.CommandText = "SELECT PostID, UserID, CreatedAt, Content FROM dbo.Posts ORDER BY PostID ASC"
$reader = $cmd.ExecuteReader()
$posts = @()
while ($reader.Read()) {
    $posts += [PSCustomObject]@{
        PostID = $reader["PostID"]
        UserID = $reader["UserID"]
        CreatedAt = $reader["CreatedAt"]
        Content = $reader["Content"].ToString()
    }
}
$reader.Close()

Write-Host ("Total Posts: " + $posts.Count)
Write-Host "--- FIRST 10 POSTS ---"
$posts | Select-Object -First 10 | ForEach-Object {
    $c = $_.Content
    if ($c.Length -gt 40) { $c = $c.Substring(0, 40) }
    Write-Host ("Post #" + $_.PostID + " | CreatedAt: " + $_.CreatedAt + " | " + $c)
}
Write-Host "--- LAST 10 POSTS ---"
$posts | Select-Object -Last 10 | ForEach-Object {
    $c = $_.Content
    if ($c.Length -gt 40) { $c = $c.Substring(0, 40) }
    Write-Host ("Post #" + $_.PostID + " | CreatedAt: " + $_.CreatedAt + " | " + $c)
}

Write-Host "`n================ ALL MARKET POSTS ================"
$cmd.CommandText = "SELECT MarketPostID, UserID, CreatedAt, Title FROM dbo.MarketPosts ORDER BY MarketPostID ASC"
$reader = $cmd.ExecuteReader()
$markets = @()
while ($reader.Read()) {
    $markets += [PSCustomObject]@{
        MarketPostID = $reader["MarketPostID"]
        UserID = $reader["UserID"]
        CreatedAt = $reader["CreatedAt"]
        Title = $reader["Title"].ToString()
    }
}
$reader.Close()
Write-Host ("Total MarketPosts: " + $markets.Count)
$markets | ForEach-Object {
    Write-Host ("Market #" + $_.MarketPostID + " | CreatedAt: " + $_.CreatedAt + " | " + $_.Title)
}

Write-Host "`n================ ALL CONNECT POSTS ================"
$cmd.CommandText = "SELECT ConnectPostID, UserID, CreatedAt, Title FROM dbo.ConnectPosts ORDER BY ConnectPostID ASC"
$reader = $cmd.ExecuteReader()
$connects = @()
while ($reader.Read()) {
    $connects += [PSCustomObject]@{
        ConnectPostID = $reader["ConnectPostID"]
        UserID = $reader["UserID"]
        CreatedAt = $reader["CreatedAt"]
        Title = $reader["Title"].ToString()
    }
}
$reader.Close()
Write-Host ("Total ConnectPosts: " + $connects.Count)
$connects | ForEach-Object {
    Write-Host ("Connect #" + $_.ConnectPostID + " | CreatedAt: " + $_.CreatedAt + " | " + $_.Title)
}

Write-Host "`n================ ALL STORIES ================"
$cmd.CommandText = "SELECT StoryID, UserID, CreatedAt, ExpiresAt FROM dbo.Stories ORDER BY StoryID ASC"
$reader = $cmd.ExecuteReader()
$stories = @()
while ($reader.Read()) {
    $stories += [PSCustomObject]@{
        StoryID = $reader["StoryID"]
        UserID = $reader["UserID"]
        CreatedAt = $reader["CreatedAt"]
        ExpiresAt = $reader["ExpiresAt"]
    }
}
$reader.Close()
Write-Host ("Total Stories: " + $stories.Count)
$stories | ForEach-Object {
    Write-Host ("Story #" + $_.StoryID + " | CreatedAt: " + $_.CreatedAt + " | ExpiresAt: " + $_.ExpiresAt)
}

Write-Host "`n================ AGENT TASKS (SCHEDULER) ================"
$cmd.CommandText = "SELECT TaskId, UserId, ScheduleCron, TimeZone, NextRunUtc, CreatedAtUtc, IsActive FROM dbo.AgentTasks ORDER BY TaskId ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("TaskId: " + $reader["TaskId"] + " | Cron: " + $reader["ScheduleCron"] + " | TimeZone: " + $reader["TimeZone"] + " | NextRunUtc: " + $reader["NextRunUtc"] + " | CreatedAtUtc: " + $reader["CreatedAtUtc"] + " | IsActive: " + $reader["IsActive"])
}
$reader.Close()

Write-Host "`n================ AGENT TASK RUNS ================"
$cmd.CommandText = "SELECT TOP 5 RunId, TaskId, Status, StartedAtUtc, CompletedAtUtc FROM dbo.AgentTaskRuns ORDER BY RunId DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host ("RunId: " + $reader["RunId"] + " | TaskId: " + $reader["TaskId"] + " | Status: " + $reader["Status"] + " | StartedAtUtc: " + $reader["StartedAtUtc"] + " | CompletedAtUtc: " + $reader["CompletedAtUtc"])
}
$reader.Close()

$conn.Close()
