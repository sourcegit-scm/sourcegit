using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class IsAncestor : Command
    {
        public IsAncestor(string repo, string checkPoint, string endPoint)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"merge-base --is-ancestor {checkPoint} {endPoint}";
        }

        public async Task<bool> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess;
        }
    }
}
