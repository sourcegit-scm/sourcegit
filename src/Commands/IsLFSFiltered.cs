using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class IsLFSFiltered : Command
    {
        public IsLFSFiltered(string repo, string path)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"check-attr -z filter \"{path}\"";
            RaiseError = false;
        }

        public IsLFSFiltered(string repo, string sha, string path)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"check-attr --source {sha} -z filter \"{path}\"";
            RaiseError = false;
        }

        public async Task<bool> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess && rs.StdOut.Contains("filter\0lfs");
        }
    }
}
