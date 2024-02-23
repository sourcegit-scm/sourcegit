using System;

namespace SourceGit.Commands {
    public class Statistics : Command {
        public Statistics(string repo) {
            _statistics = new Models.Statistics();

            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --date-order --branches --remotes --since=\"{_statistics.Since()}\" --date=unix --pretty=format:\"%ad$%an\"";
        }

        public Models.Statistics Result() {
            Exec();
            _statistics.Complete();
            return _statistics;
        }

        protected override void OnReadline(string line) {
            var dateEndIdx = line.IndexOf('$', StringComparison.Ordinal);
            if (dateEndIdx == -1) return;

            var dateStr = line.Substring(0, dateEndIdx);
            var date = 0.0;
            if (!double.TryParse(dateStr, out date)) return;

            _statistics.AddCommit(line.Substring(dateEndIdx + 1), date);
        }

        private Models.Statistics _statistics = null;
    }
}
