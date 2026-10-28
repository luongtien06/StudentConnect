import os

path = 'Areas/Admin/Controllers/AdminAiAgentController.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

old_part1 = """            var runs = await db.AgentTaskRuns
                .Where(r => r.TaskID == taskId)
                .OrderByDescending(r => r.StartedAt)
                .Take(50)
                .Select(r => new
                {
                    r.RunID,
                    r.TaskID,
                    r.Status,
                    StartedAt = r.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                    FinishedAt = r.FinishedAt.HasValue ? r.FinishedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—",
                    r.AttemptCount,
                    r.ResultSummary,
                    r.ErrorMessage
                })
                .ToListAsync();"""

new_part1 = """            var rawRuns = await db.AgentTaskRuns
                .Where(r => r.TaskID == taskId)
                .OrderByDescending(r => r.StartedAt)
                .Take(50)
                .ToListAsync();

            var runs = rawRuns.Select(r => new
            {
                r.RunID,
                r.TaskID,
                r.Status,
                StartedAt = r.StartedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                FinishedAt = r.FinishedAt.HasValue ? r.FinishedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "—",
                r.AttemptCount,
                r.ResultSummary,
                r.ErrorMessage
            }).ToList();"""

old_part2 = """            var logs = await query.OrderByDescending(l => l.CreatedAt).Take(200).Select(l => new
            {
                l.LogID,
                UserName = l.User != null ? (l.User.DisplayName ?? l.User.Username) : ("User #" + l.UserID),
                l.ActionName,
                l.TargetType,
                l.TargetID,
                l.Status,
                InputSnippet = l.InputSummary != null && l.InputSummary.Length > 80 ? l.InputSummary.Substring(0, 80) + "..." : l.InputSummary,
                ResultSnippet = l.ResultSummary != null && l.ResultSummary.Length > 80 ? l.ResultSummary.Substring(0, 80) + "..." : l.ResultSummary,
                l.ErrorMessage,
                CreatedAtFormatted = l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")
            }).ToListAsync();"""

new_part2 = """            var rawLogs = await query.OrderByDescending(l => l.CreatedAt).Take(200).ToListAsync();

            var logs = rawLogs.Select(l => new
            {
                l.LogID,
                UserName = l.User != null ? (l.User.DisplayName ?? l.User.Username) : ("User #" + l.UserID),
                l.ActionName,
                l.TargetType,
                l.TargetID,
                l.Status,
                InputSnippet = l.InputSummary != null && l.InputSummary.Length > 80 ? l.InputSummary.Substring(0, 80) + "..." : l.InputSummary,
                ResultSnippet = l.ResultSummary != null && l.ResultSummary.Length > 80 ? l.ResultSummary.Substring(0, 80) + "..." : l.ResultSummary,
                l.ErrorMessage,
                CreatedAtFormatted = l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss")
            }).ToList();"""

norm_content = content.replace('\r\n', '\n')
p1_found = old_part1 in norm_content
p2_found = old_part2 in norm_content
print(f"P1 found: {p1_found}, P2 found: {p2_found}")

if p1_found and p2_found:
    norm_content = norm_content.replace(old_part1, new_part1).replace(old_part2, new_part2)
    if '\r\n' in content:
        norm_content = norm_content.replace('\n', '\r\n')
    with open(path, 'w', encoding='utf-8') as f:
        f.write(norm_content)
    print("Successfully updated AdminAiAgentController.cs")
else:
    print("Could not find matching blocks!")
