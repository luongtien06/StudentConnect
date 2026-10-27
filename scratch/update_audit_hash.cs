using System;
using System.IO;

class Program {
    static void Main() {
        string path = "scratch\\FinalAuditGeminiModeration.cs";
        string content = File.ReadAllText(path);
        string newHash = "335E1059BDC17E4380C23D12E125EEACF2549B9B3D7012DB40F31C420ACFA17A";
        content = content.Replace("70FFA8D830FCE0F82C61B27E92A74DB0634122A8D3E527A8B3728BC2E40CE178", newHash);
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        Console.WriteLine("Updated SHA256 in FinalAuditGeminiModeration.cs to: " + newHash);
    }
}
