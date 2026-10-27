using System;
using System.Data.SqlClient;
using System.Xml;
using System.Data.Common;

class Program {
    static void Main() {
        var xml = new XmlDocument();
        xml.Load("Config\\ConnectionStrings.config");
        var connNode = xml.SelectSingleNode("//connectionStrings/add[1]");
        string rawConnStr = connNode.Attributes["connectionString"].Value;
        
        string sqlConnStr = rawConnStr;
        if (rawConnStr.Contains("provider connection string=")) {
            int idx = rawConnStr.IndexOf("provider connection string=");
            string sub = rawConnStr.Substring(idx + "provider connection string=".Length).Trim('"', '\'', ' ');
            if (sub.EndsWith("\"")) sub = sub.Substring(0, sub.Length - 1);
            sqlConnStr = sub;
        }
        
        using (var conn = new SqlConnection(sqlConnStr)) {
            conn.Open();
            Console.WriteLine("Connected to: " + conn.DataSource + " / " + conn.Database);
            string[] tables = new string[] { "ChatRestrictions", "ChatReports", "ChatParticipants", "ChatRooms", "ChatMessages" };
            foreach (var t in tables) {
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @name", conn)) {
                    cmd.Parameters.AddWithValue("@name", t);
                    int count = (int)cmd.ExecuteScalar();
                    Console.WriteLine("Table " + t + ": " + (count > 0 ? "EXISTS" : "DOES NOT EXIST"));
                }
            }
        }
    }
}
