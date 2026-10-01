using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Statistics : Command
    {
        public Statistics(string repo, int max, Models.Branch specBranch)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            builder
                .Append("log --date-order -")
                .Append(max)
                .Append(" --format=%ct$%aN±%aE ");

            if (specBranch == null || string.IsNullOrEmpty(specBranch.FullName))
                builder.Append("--branches --remotes");
            else
                builder.Append(specBranch.FullName);

            Args = builder.ToString();
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
