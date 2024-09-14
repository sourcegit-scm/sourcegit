using System;

namespace SourceGit.Commands
{
    public class Statistics : Command
    {
        public Statistics(string repo)
        {
            _statistics = new Models.Statistics();

            WorkingDirectory = repo;
            Context = repo;
            Args = [
                "log", "--date-order", "--branches", "--remotes",
                $"--since={_statistics.Since()}", "--pretty=format:%ct$%an"
            ];
        }

        public Models.Statistics Result()
        {
            Exec();
            _statistics.Complete();
            return _statistics;
        }

        protected override void OnReadline(string line)
        {
            var dateEndIdx = line.IndexOf('$', StringComparison.Ordinal);
            if (dateEndIdx == -1)
                return;

            var dateStr = line.Substring(0, dateEndIdx);
            if (double.TryParse(dateStr, out var date))
                _statistics.AddCommit(line.Substring(dateEndIdx + 1), date);
        }

        private readonly Models.Statistics _statistics = null;
    }
}
