using System;
using System.Data.SqlClient;

class TestCompileError
{
    static void Main()
    {
        string connStr = "Server=localhost\\MSSQLSERVER01;Database=TDMUEcoSystem;Integrated Security=True;";
        using (var conn = new SqlConnection(connStr))
        {
            conn.Open();
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DummyTestTable]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[DummyTestTable] ( ID int );
END
ELSE
BEGIN
    ALTER TABLE [dbo].[DummyTestTable] ALTER COLUMN [ID] bigint;
END
";
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("Executed successfully!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQL Error: " + ex.Message);
            }
            finally
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "IF OBJECT_ID('DummyTestTable') IS NOT NULL DROP TABLE DummyTestTable;";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { }
            }
        }
    }
}
