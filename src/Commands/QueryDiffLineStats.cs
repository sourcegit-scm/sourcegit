using System;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryDiffLineStats : Command
    {
        public QueryDiffLineStats(string repo, string start, string end, bool ignoreWhitespace, bool ignoreCRAtEOL)
            : this(repo, ignoreWhitespace, ignoreCRAtEOL, $"{(string.IsNullOrEmpty(start) ? "-R" : start)} {end}")
        {
        }

        public QueryDiffLineStats(string repo, bool staged, bool ignoreWhitespace, bool ignoreCRAtEOL)
            : this(repo, ignoreWhitespace, ignoreCRAtEOL, staged ? "--cached" : string.Empty)
        {
        }

        private QueryDiffLineStats(string repo, bool ignoreWhitespace, bool ignoreCRAtEOL, string suffix)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;

            var builder = new StringBuilder();
            builder.Append("diff --no-color --no-ext-diff --numstat -z ");
            if (ignoreCRAtEOL)
                builder.Append("--ignore-cr-at-eol ");
            if (ignoreWhitespace)
                builder.Append("--ignore-space-change --ignore-blank-lines ");
            builder.Append(suffix);
            Args = builder.ToString().TrimEnd();
        }

        public async Task<(int added, int deleted)> GetResultAsync()
        {
            try
            {
                var rs = await ReadToEndAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(rs.StdOut))
                    return (0, 0);

                int added = 0, deleted = 0;
                foreach (var token in rs.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = token.Split('\t', 3);
                    if (parts.Length < 2)
                        continue;

                    added += ParseCount(parts[0]);
                    deleted += ParseCount(parts[1]);
                }

                return (added, deleted);
            }
            catch
            {
                return (0, 0);
            }
        }

        private static int ParseCount(string value) => int.TryParse(value, out var parsed) ? parsed : 0;
    }
}
