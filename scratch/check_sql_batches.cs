using System;
using System.IO;
using System.Text.RegularExpressions;

class CheckSqlBatches
{
    static void Main()
    {
        string sql = File.ReadAllText(@"Database\Scripts\Migration_Deploy_Production_Chat_And_Moderation.sql");
        string[] batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        Console.WriteLine("Total batches: " + batches.Length);
        for (int i = 0; i < batches.Length; i++)
        {
            string b = batches[i].Trim();
            if (string.IsNullOrEmpty(b)) continue;
            Console.WriteLine($"--- Batch {i} (len: {b.Length}) ---");
            Console.WriteLine(b.Substring(0, Math.Min(b.Length, 80)).Replace("\r", "").Replace("\n", " ") + "...");
        }
    }
}
