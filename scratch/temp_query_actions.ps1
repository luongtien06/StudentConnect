
 = New-Object System.Data.SqlClient.SqlConnection('data source=sql9001.site4now.net; initial catalog=db_acf6d2_studentdb; user id=db_acf6d2_studentdb_admin; password=Tien@2006; MultipleActiveResultSets=True; App=EntityFramework; TrustServerCertificate=True')
.Open()
 = .CreateCommand()
.CommandText = 'SELECT ActionName, COUNT(*) AS C, COUNT(RunID) AS FromTasks, COUNT(ConversationID) AS FromChat FROM dbo.AgentActionLogs GROUP BY ActionName ORDER BY C DESC'
 = .ExecuteReader()
while (.Read()) {
    Write-Output (['ActionName'].ToString() + ' | Tổng: ' + ['C'].ToString() + ' | Từ Task: ' + ['FromTasks'].ToString() + ' | Từ Chat: ' + ['FromChat'].ToString())
}
.Close()
.Close()
