using System;
using System.IO;
using System.Text.RegularExpressions;

class PrintBatch9
{
    static void Main()
    {
        string sql = File.ReadAllText(@"Database\Scripts\Migration_Deploy_Production_Chat_And_Moderation.sql");
        string[] batches = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Console.WriteLine(batches[9]);
    }
}
