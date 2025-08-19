using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class DiffStat : Command
    {
        public DiffStat(string repo, string range)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"diff --stat {range}";
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess && !string.IsNullOrEmpty(rs.StdOut))
                return rs.StdOut.Trim();
            return string.Empty;
        }
    }
}
