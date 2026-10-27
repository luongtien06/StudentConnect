using System;
using System.Data.SqlClient;

class TestPermissions
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
                    cmd.CommandText = "CREATE TABLE [dbo].[__TestPerm] ( ID int );";
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("CREATE TABLE permission: GRANTED");

                    cmd.CommandText = "DROP TABLE [dbo].[__TestPerm];";
                    cmd.ExecuteNonQuery();
                    Console.WriteLine("DROP TABLE permission: GRANTED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Permission Error: " + ex.Message);
            }
        }
    }
}
