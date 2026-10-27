using System;
using System.IO;

class PatchAdminDefensive
{
    static void Main()
    {
        string path = @"Areas\Admin\Controllers\AdminChatController.cs";
        string content = File.ReadAllText(path);

        // 1. Wrap auto-expire in try-catch
        string oldExpire = @"            // Auto-expire out-of-date restrictions

            var expiredRestrictions = db.ChatRestrictions

                .Where(r => r.IsActive && r.ExpiresAt.HasValue && r.ExpiresAt.Value <= now)

                .ToList();

            if (expiredRestrictions.Any())

            {

                foreach (var exp in expiredRestrictions)

                {

                    exp.IsActive = false;

                    exp.RevokedAt = now;

                    exp.RevokeReason = ""Tự động hết hiệu lực theo thời hạn"";

                }

                db.SaveChanges();

            }";

        string newExpire = @"            // Auto-expire out-of-date restrictions (Bọc try-catch chống sập khi DB chưa migration)
            try
            {
                var expiredRestrictions = db.ChatRestrictions
                    .Where(r => r.IsActive && r.ExpiresAt.HasValue && r.ExpiresAt.Value <= now)
                    .ToList();

                if (expiredRestrictions.Any())
                {
                    foreach (var exp in expiredRestrictions)
                    {
                        exp.IsActive = false;
                        exp.RevokedAt = now;
                        exp.RevokeReason = ""Tự động hết hiệu lực theo thời hạn"";
                    }
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(""[AdminChat] Auto-expire ChatRestrictions skipped: "" + ex.Message);
            }";

        // Normalize newlines for search
        string normContent = content.Replace("\r\n", "\n");
        string normOldExpire = oldExpire.Replace("\r\n", "\n");

        if (normContent.Contains(normOldExpire))
        {
            normContent = normContent.Replace(normOldExpire, newExpire.Replace("\r\n", "\n"));
            File.WriteAllText(path, normContent.Replace("\n", "\r\n"));
            Console.WriteLine("Auto-expire wrapped in try-catch successfully.");
        }
        else
        {
            Console.WriteLine("oldExpire pattern not matched.");
        }
    }
}
