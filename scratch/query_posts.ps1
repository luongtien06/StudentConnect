$csFile = Get-Content 'Config\ConnectionStrings.config' -Raw
$csFile = $csFile -replace '(?s)<!--.*?-->', ''
if ($csFile -match 'data source\s*=\s*([^;]+);.*?initial catalog\s*=\s*([^;]+);.*?user id\s*=\s*([^;]+);.*?password\s*=\s*([^;"]+)') {
    $server = $matches[1].Trim()
    $db = $matches[2].Trim()
    $user = $matches[3].Trim()
    $pwd = $matches[4].Trim()
    $rawConn = "Server=$server;Database=$db;User Id=$user;Password=$pwd;TrustServerCertificate=True;"
    $conn = New-Object System.Data.SqlClient.SqlConnection($rawConn)
    $conn.Open()
    $cmd = $conn.CreateCommand()

    Write-Host '--- TOP 8 POSTS ---'
    $cmd.CommandText = 'SELECT TOP 8 PostID, UserID, Content, CreatedAt, UpdatedAt FROM dbo.Posts ORDER BY PostID DESC'
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $c = $reader['Content'].ToString()
        if ($c.Length -gt 50) { $c = $c.Substring(0, 50) }
        Write-Host ('PostID: ' + $reader['PostID'] + ' | User: ' + $reader['UserID'] + ' | CreatedAt: ' + $reader['CreatedAt'] + ' | Content: ' + $c)
    }
    $reader.Close()

    Write-Host "`n--- SQL TIME vs LOCAL MACHINE TIME ---"
    $cmd.CommandText = 'SELECT GETDATE() AS LocalTime, GETUTCDATE() AS UtcTime'
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host ('SQL Server GETDATE(): ' + $reader['LocalTime'] + ' | SQL Server GETUTCDATE(): ' + $reader['UtcTime'])
    }
    $reader.Close()
    Write-Host ('Machine Local DateTime.Now: ' + [DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss'))
    Write-Host ('Machine DateTime.UtcNow:    ' + [DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss'))

    $conn.Close()
} else {
    Write-Host "No match found"
}
