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

        public async Task<bool> ResultAsync()
        {
            var rs = await ReadToEndAsync();
            return rs.IsSuccess && rs.StdOut.Trim().Equals("commit");
        }
    }
}
