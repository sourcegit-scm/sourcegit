using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class IsCommitSHA : Command
    {
        public IsCommitSHA(string repo, string hash)
        {
            WorkingDirectory = repo;
            Args = $"cat-file -t {hash}";
        }

        public async Task<bool> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess && rs.StdOut.Trim().Equals("commit");
        }
    }
}
