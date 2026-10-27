using System;
using System.Data.SqlClient;

class CheckMasterTables
{
    static void Main()
    {
        string connStr = "Server=sql9001.site4now.net;Database=db_acf6d2_studentdb;User Id=db_acf6d2_studentdb_admin;Password=Tien@2006;TrustServerCertificate=True;Connect Timeout=15;";
        using (var conn = new SqlConnection(connStr))
        {
            conn.Open();
            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT DB_NAME(), SCHEMA_NAME();";
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read()) Console.WriteLine($"Current DB: {r[0]}, Default Schema: {r[1]}");
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
