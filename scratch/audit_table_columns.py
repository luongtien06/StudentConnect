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

$tables = @("Posts", "MarketPosts", "ConnectPosts", "Stories", "AgentTasks", "AgentTaskRuns")
foreach ($tbl in $tables) {{
    Write-Host "`n--- COLUMNS FOR $tbl ---"
    $cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '$tbl'"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {{
        Write-Host ($reader["COLUMN_NAME"] + " (" + $reader["DATA_TYPE"] + ")")
    }}
    $reader.Close()
}}
$conn.Close()
'''
    with open('scratch/temp_cols.ps1', 'w', encoding='utf-8') as ps_file:
        ps_file.write(ps_script)

    res = subprocess.run(['powershell', '-File', 'scratch/temp_cols.ps1'], capture_output=True, text=True)
    print(res.stdout)
    if res.stderr:
        print('ERR:', res.stderr)
