import re
import subprocess

with open('Config/ConnectionStrings.config', 'r', encoding='utf-8') as f:
    text = f.read()

m = re.findall(r'provider connection string=&quot;(.*?)&quot;', text, re.DOTALL)
if m:
    conn_str = m[-1].strip().replace('\n', ' ').replace('\t', ' ')
    conn_str = re.sub(r'\s+', ' ', conn_str).strip()
    
    ps_script = f'''
$rawConn = "{conn_str}"
$conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "================ ALL POSTS ================"
$cmd.CommandText = "SELECT PostID, UserID, CreatedAt, Content FROM dbo.Posts ORDER BY PostID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    $c = $reader["Content"].ToString()
    if ($c.Length -gt 40) {{ $c = $c.Substring(0, 40) }}
    Write-Host ("Post #" + $reader["PostID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | " + $c)
}}
$reader.Close()

Write-Host "`n================ ALL MARKET POSTS ================"
$cmd.CommandText = "SELECT PostID, UserID, CreatedAt, Title, Price FROM dbo.MarketPosts ORDER BY PostID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    Write-Host ("MarketPost #" + $reader["PostID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | Title: " + $reader["Title"] + " | Price: " + $reader["Price"])
}}
$reader.Close()

Write-Host "`n================ ALL CONNECT POSTS ================"
$cmd.CommandText = "SELECT PostID, UserID, CreatedAt, Title FROM dbo.ConnectPosts ORDER BY PostID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    Write-Host ("ConnectPost #" + $reader["PostID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | Title: " + $reader["Title"])
}}
$reader.Close()

Write-Host "`n================ ALL STORIES ================"
$cmd.CommandText = "SELECT StoryID, UserID, CreatedAt, ExpiresAt, Caption FROM dbo.Stories ORDER BY StoryID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    Write-Host ("Story #" + $reader["StoryID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | ExpiresAt: " + $reader["ExpiresAt"] + " | Caption: " + $reader["Caption"])
}}
$reader.Close()

Write-Host "`n================ AGENT TASKS (SCHEDULER) ================"
$cmd.CommandText = "SELECT TaskID, UserID, TaskName, ScheduleType, ScheduleConfig, TimeZone, Status, NextRunAt, LastRunAt, CreatedAt FROM dbo.AgentTasks ORDER BY TaskID ASC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    Write-Host ("Task #" + $reader["TaskID"] + " | Name: " + $reader["TaskName"] + " | Type: " + $reader["ScheduleType"] + " | TZ: " + $reader["TimeZone"] + " | Status: " + $reader["Status"] + " | NextRunAt: " + $reader["NextRunAt"] + " | CreatedAt: " + $reader["CreatedAt"])
}}
$reader.Close()

Write-Host "`n================ AGENT TASK RUNS ================"
$cmd.CommandText = "SELECT TOP 10 RunID, TaskID, Status, StartedAt, FinishedAt, CreatedAt FROM dbo.AgentTaskRuns ORDER BY RunID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    Write-Host ("Run #" + $reader["RunID"] + " | Task #" + $reader["TaskID"] + " | Status: " + $reader["Status"] + " | StartedAt: " + $reader["StartedAt"] + " | FinishedAt: " + $reader["FinishedAt"] + " | CreatedAt: " + $reader["CreatedAt"])
}}
$reader.Close()

$conn.Close()
'''
    with open('scratch/temp_query_records.ps1', 'w', encoding='utf-8') as ps_file:
        ps_file.write(ps_script)

    res = subprocess.run(['powershell', '-File', 'scratch/temp_query_records.ps1'], capture_output=True, text=True)
    print(res.stdout)
    if res.stderr:
        print('ERR:', res.stderr)
