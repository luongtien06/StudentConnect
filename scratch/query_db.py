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
Write-Host "--- TOP 20 POSTS ---"
$cmd.CommandText = "SELECT TOP 20 PostID, UserID, Content, CreatedAt, UpdatedAt FROM dbo.Posts ORDER BY PostID DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    $c = $reader["Content"].ToString()
    if ($c.Length -gt 50) {{ $c = $c.Substring(0, 50) }}
    Write-Host ("PostID: " + $reader["PostID"] + " | User: " + $reader["UserID"] + " | CreatedAt: " + $reader["CreatedAt"] + " | Content: " + $c)
}}
$reader.Close()

Write-Host "`n--- SQL TIME vs LOCAL MACHINE TIME ---"
$cmd.CommandText = "SELECT GETDATE() AS LocalTime, GETUTCDATE() AS UtcTime"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {{
    Write-Host ("SQL Server GETDATE(): " + $reader["LocalTime"] + " | SQL Server GETUTCDATE(): " + $reader["UtcTime"])
}}
$reader.Close()
Write-Host ("Machine Local DateTime.Now: " + [DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss"))
Write-Host ("Machine DateTime.UtcNow:    " + [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
$conn.Close()
'''
    with open('scratch/temp_query.ps1', 'w', encoding='utf-8') as ps_file:
        ps_file.write(ps_script)

    res = subprocess.run(['powershell', '-File', 'scratch/temp_query.ps1'], capture_output=True, text=True)
    print(res.stdout)
    if res.stderr:
        print('ERR:', res.stderr)
else:
    print('No provider connection string found')
