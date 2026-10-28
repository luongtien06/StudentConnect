
 = New-Object System.Data.SqlClient.SqlConnection('data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True')
.Open()
 = .CreateCommand()
.CommandText = 'SELECT * FROM dbo.AgentActionLogs'
 = .ExecuteReader()
while (.Read()) {
    for ( = 0;  -lt .FieldCount; ++) {
        Write-Output (.GetName() + ': ' + .GetValue().ToString())
    }
}
.Close()
.Close()
