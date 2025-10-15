using System.IO;
using System.Threading.Tasks;

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

        public async Task<Models.Statistics> ReadAsync()
        {
            var statistics = new Models.Statistics();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return statistics;

            var sr = new StringReader(rs.StdOut);
            while (sr.ReadLine() is { } line)
                ParseLine(statistics, line);

            statistics.Complete();
            return statistics;
        }

        private void ParseLine(Models.Statistics statistics, string line)
        {
            var parts = line.Split('$', 2);
            if (parts.Length == 2 && double.TryParse(parts[0], out var date))
                statistics.AddCommit(parts[1], date);
        }
    }
}
