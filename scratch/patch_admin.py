import os

path = r'e:\LTWEB\StudentConnect\Areas\Admin\Controllers\AdminChatController.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# 1. Replace auto-expire
old_expire = """            // Auto-expire out-of-date restrictions

            var expiredRestrictions = db.ChatRestrictions

                .Where(r => r.IsActive && r.ExpiresAt.HasValue && r.ExpiresAt.Value <= now)

                .ToList();

            if (expiredRestrictions.Any())

            {

                foreach (var exp in expiredRestrictions)

                {

                    exp.IsActive = false;

                    exp.RevokedAt = now;

                    exp.RevokeReason = "Tự động hết hiệu lực theo thời hạn";

                }

                db.SaveChanges();

            }"""

new_expire = """            // Auto-expire out-of-date restrictions (Defensive: an toàn nếu bảng chưa tạo trên DB)
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
                        exp.RevokeReason = "Tự động hết hiệu lực theo thời hạn";
                    }
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] Auto-expire ChatRestrictions skipped: " + ex.Message);
            }"""

assert old_expire in text, "old_expire not matched"
text = text.replace(old_expire, new_expire, 1)

# 2. Replace stats
old_stats = """            stats.TotalReports = db.ChatReports.Count();

            stats.PendingReports = db.ChatReports.Count(r => r.Status == "Pending");

            stats.ProcessingReports = db.ChatReports.Count(r => r.Status == "Processing");

            stats.ResolvedReports = db.ChatReports.Count(r => r.Status == "Resolved");

            stats.RejectedReports = db.ChatReports.Count(r => r.Status == "Rejected");



            stats.ActiveBannedUsersCount = db.ChatRestrictions

                .Where(r => r.IsActive && r.RestrictionType == "Ban")

                .Select(r => r.UserID).Distinct().Count();

            stats.ActiveMutedUsersCount = db.ChatRestrictions

                .Where(r => r.IsActive && r.RestrictionType == "Mute")

                .Select(r => r.UserID).Distinct().Count();"""

new_stats = """            try
            {
                stats.TotalReports = db.ChatReports.Count();
                stats.PendingReports = db.ChatReports.Count(r => r.Status == "Pending");
                stats.ProcessingReports = db.ChatReports.Count(r => r.Status == "Processing");
                stats.ResolvedReports = db.ChatReports.Count(r => r.Status == "Resolved");
                stats.RejectedReports = db.ChatReports.Count(r => r.Status == "Rejected");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] ChatReports stats skipped: " + ex.Message);
            }

            try
            {
                stats.ActiveBannedUsersCount = db.ChatRestrictions
                    .Where(r => r.IsActive && r.RestrictionType == "Ban")
                    .Select(r => r.UserID).Distinct().Count();
                stats.ActiveMutedUsersCount = db.ChatRestrictions
                    .Where(r => r.IsActive && r.RestrictionType == "Mute")
                    .Select(r => r.UserID).Distinct().Count();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] ChatRestrictions stats skipped: " + ex.Message);
            }"""

assert old_stats in text, "old_stats not matched"
text = text.replace(old_stats, new_stats, 1)

# 3. Replace tabs in Index
old_tabs = """            // ── TAB 3: BÁO CÁO VI PHẠM (Top 60) ──

            vm.Reports = GetReportViewModels(db.ChatReports.OrderByDescending(r => r.CreatedAt).Take(60).ToList());



            // ── TAB 4: THÀNH VIÊN VÀ HẠN CHẾ (Top 50) ──

            vm.Members = GetMemberViewModels(null, null, 1, 50).members;

            vm.ActiveRestrictions = GetActiveRestrictionViewModels();"""

new_tabs = """            // ── TAB 3: BÁO CÁO VI PHẠM (Top 60) ──
            try
            {
                vm.Reports = GetReportViewModels(db.ChatReports.OrderByDescending(r => r.CreatedAt).Take(60).ToList());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] Reports load skipped: " + ex.Message);
                vm.Reports = new List<ChatReportItemViewModel>();
            }

            // ── TAB 4: THÀNH VIÊN VÀ HẠN CHẾ (Top 50) ──
            try
            {
                vm.Members = GetMemberViewModels(null, null, 1, 50).members;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] Members load skipped: " + ex.Message);
                vm.Members = new List<ChatMemberItemViewModel>();
            }

            try
            {
                vm.ActiveRestrictions = GetActiveRestrictionViewModels();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] ActiveRestrictions load skipped: " + ex.Message);
                vm.ActiveRestrictions = new List<ChatRestrictionItemViewModel>();
            }"""

assert old_tabs in text, "old_tabs not matched"
text = text.replace(old_tabs, new_tabs, 1)

# 4. Wrap GetActiveRestrictionViewModels in try-catch
old_garv = """        private List<ChatRestrictionItemViewModel> GetActiveRestrictionViewModels()

        {

            var now = DateTime.Now;

            var list = db.ChatRestrictions"""

new_garv = """        private List<ChatRestrictionItemViewModel> GetActiveRestrictionViewModels()

        {
            try
            {

            var now = DateTime.Now;

            var list = db.ChatRestrictions"""

assert old_garv in text, "old_garv not matched"
text = text.replace(old_garv, new_garv, 1)

old_garv_end = """                    IsActive = true

                };

            }).ToList();

        }"""

new_garv_end = """                    IsActive = true

                };

            }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] GetActiveRestrictionViewModels error: " + ex.Message);
                return new List<ChatRestrictionItemViewModel>();
            }

        }"""

assert old_garv_end in text, "old_garv_end not matched"
text = text.replace(old_garv_end, new_garv_end, 1)

# 5. Wrap GetMemberViewModels in try-catch
old_gmv = """        private (List<ChatMemberItemViewModel> members, int totalCount) GetMemberViewModels(string keyword, string filterStatus, int page, int pageSize)

        {

            var now = DateTime.Now;"""

new_gmv = """        private (List<ChatMemberItemViewModel> members, int totalCount) GetMemberViewModels(string keyword, string filterStatus, int page, int pageSize)

        {
            try
            {

            var now = DateTime.Now;"""

assert old_gmv in text, "old_gmv not matched"
text = text.replace(old_gmv, new_gmv, 1)

old_gmv_end = """            return (list, totalCount);

        }"""

new_gmv_end = """            return (list, totalCount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[AdminChat] GetMemberViewModels error: " + ex.Message);
                return (new List<ChatMemberItemViewModel>(), 0);
            }

        }"""

assert old_gmv_end in text, "old_gmv_end not matched"
text = text.replace(old_gmv_end, new_gmv_end, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print("Applied defensive patches successfully!")
