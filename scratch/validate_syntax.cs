using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Data.SqlClient;

class ValidateSyntaxOnServer
{
    static void Main()
    {
        string connStr = "Server=sql9001.site4now.net;Database=db_acf6d2_studentdb;User Id=db_acf6d2_studentdb_admin;Password=Tien@2006;TrustServerCertificate=True;Connect Timeout=15;";
        string sql = File.ReadAllText(@"Database\Scripts\Migration_Deploy_Production_Chat_And_Moderation.sql");
        string[] batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        using (var conn = new SqlConnection(connStr))
        {
            conn.Open();
            Console.WriteLine("Connected to server. Checking batches syntax with SET NOEXEC ON...");

            for (int i = 0; i < batches.Length; i++)
            {
                string b = batches[i].Trim();
                if (string.IsNullOrEmpty(b)) continue;

                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SET NOEXEC ON;\n" + b + "\nSET NOEXEC OFF;";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"BATCH {i} SYNTAX ERROR: {ex.Message}");
                    Console.WriteLine("Batch content snippet:\n" + b.Substring(0, Math.Min(b.Length, 200)));
                }
            }
            Console.WriteLine("All batches checked!");
        }
    }
}
