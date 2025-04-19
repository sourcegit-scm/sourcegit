using System;

namespace SourceGit.Commands
{
    public class Statistics : Command
    {
        public Statistics(string repo, int max)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --date-order --branches --remotes -{max} --format=%ct$%aN±%aE";
        }

        public Models.Statistics Result()
        {
            var statistics = new Models.Statistics();
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return statistics;

            var start = 0;
            var end = rs.StdOut.IndexOf('\n', start);
            while (end > 0)
            {
                ParseLine(statistics, rs.StdOut.Substring(start, end - start));
                start = end + 1;
                end = rs.StdOut.IndexOf('\n', start);
            }

            if (start < rs.StdOut.Length)
                ParseLine(statistics, rs.StdOut.Substring(start));

            statistics.Complete();
            return statistics;
        }

        private void ParseLine(Models.Statistics statistics, string line)
        {
            var dateEndIdx = line.IndexOf('$', StringComparison.Ordinal);
            if (dateEndIdx == -1)
                return;

            var dateStr = line.AsSpan().Slice(0, dateEndIdx);
            if (double.TryParse(dateStr, out var date))
                statistics.AddCommit(line.Substring(dateEndIdx + 1), date);
        }
    }
}
