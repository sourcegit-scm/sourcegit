using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class DiffAll : Command
    {
        public DiffAll(string repo, string range)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"diff {range}";
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync();
            return rs.IsSuccess ? rs.StdOut : string.Empty;
        }
    }
}
