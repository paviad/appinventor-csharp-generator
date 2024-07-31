using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AiaGenerator {
    public static class StringListExtensions {
        public static IEnumerable<string> Indent(this IEnumerable<string> lst) {
            return lst.Select(r => $"    {r}");
        }

        public static IEnumerable<string> ReturnLast(this IEnumerable<string> lst) {
            var rc = lst.ToList();
            if (rc.Count > 0) {
                rc[rc.Count - 1] = $"return {rc[rc.Count - 1]}";
            }

            return rc;
        }

        public static IEnumerable<string> Semicolon(this IEnumerable<string> lst) {
            bool NeedsSemicolon(string line) => !string.IsNullOrWhiteSpace(line) && !Regex.IsMatch(line.Trim(), @"[{};]$");

            return lst.Select(r => NeedsSemicolon(r) ? $"{r};" : r);
        }
    }
}
