using System;
using System.Data.SqlClient;

class CheckCurrentStatus
{
    static void Main()
    {
        string connStr = "Server=sql9001.site4now.net;Database=db_acf6d2_studentdb;User Id=db_acf6d2_studentdb_admin;Password=Tien@2006;TrustServerCertificate=True;Connect Timeout=15;";
        using (var conn = new SqlConnection(connStr))
        {
            try
            {
                conn.Open();
                Console.WriteLine("Connect success to DB: " + conn.Database);

                // 1. Check if ChatRestrictions exists
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ChatRestrictions'";
                    int count = (int)cmd.ExecuteScalar();
                    Console.WriteLine("Table ChatRestrictions count in INFORMATION_SCHEMA: " + count);
                }

                // 2. Check if ChatReports exists
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='ChatReports'";
                    int count = (int)cmd.ExecuteScalar();
                    Console.WriteLine("Table ChatReports count in INFORMATION_SCHEMA: " + count);
                }

                // 3. Check ChatMessages columns
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ChatMessages' AND COLUMN_NAME IN ('IsHidden', 'HiddenReason', 'HiddenBy', 'HiddenAt')";
                    using (var r = cmd.ExecuteReader())
                    {
                        Console.WriteLine("Columns on ChatMessages found:");
                        while (r.Read())
                        {
                            Console.WriteLine(" - " + r[0]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
