import subprocess

ps = """
$conn = New-Object System.Data.SqlClient.SqlConnection("data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) AS C FROM dbo.AgentActionLogs"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Output ("Total AgentActionLogs: " + $reader['C'])
}
$reader.Close()
$conn.Close()
"""
with open('scratch/temp_cols.ps1', 'w', encoding='utf-8') as f:
    f.write(ps)
res = subprocess.run(['powershell', '-File', 'scratch/temp_cols.ps1'], capture_output=True, text=True)
print(res.stdout)
