using System.Collections.Generic;
using System.Linq;

namespace StudentConnect.Helpers
{
    public static class ConnectImageHelper
    {
        public static List<string> GetPaths(string storedPaths)
        {
            return string.IsNullOrWhiteSpace(storedPaths)
                ? new List<string>()
                : storedPaths.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Trim())
                    .Where(path => path.Length > 0)
                    .ToList();
        }

        public static string GetFirstPath(string storedPaths)
        {
            return GetPaths(storedPaths).FirstOrDefault();
        }

        public static string JoinPaths(IEnumerable<string> paths)
        {
            return string.Join("|", (paths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim()));
        }
    }
}