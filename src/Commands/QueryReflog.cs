using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryReflog : Command
    {
        public QueryReflog(string repo, int maxCount = 0)
        {
            WorkingDirectory = repo;
            Context = repo;

            var limit = maxCount > 0 ? $"-n {maxCount} " : string.Empty;
            Args = $"log -g -z {limit}--no-show-signature --date=unix --format=%H%n%gd%n%gs HEAD";
        }

        public async Task<List<Models.ReflogEntry>> GetResultAsync()
        {
            var outs = new List<Models.ReflogEntry>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return outs;

            var items = rs.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in items)
            {
                var parts = item.Split('\n', 3);
                if (parts.Length < 2)
                    continue;

                // With `--date=unix`, `%gd` is printed as `HEAD@{<unix timestamp>}`
                var entry = new Models.ReflogEntry();
                entry.SHA = parts[0];
                entry.Selector = $"HEAD@{{{outs.Count}}}";

                var selector = parts[1];
                var start = selector.IndexOf('{');
                var end = selector.LastIndexOf('}');
                if (start > 0 && end > start && ulong.TryParse(selector.AsSpan(start + 1, end - start - 1), out var time))
                    entry.Time = time;

                if (parts.Length > 2)
                    entry.Message = parts[2].TrimEnd();

                outs.Add(entry);
            }

            return outs;
        }
    }
}
