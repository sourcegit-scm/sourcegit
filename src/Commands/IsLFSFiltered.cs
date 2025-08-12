using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class IsLFSFiltered : Command
    {
        public IsLFSFiltered(string repo, string path)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"check-attr -z filter {path.Quoted()}";
            RaiseError = false;
        }

        public IsLFSFiltered(string repo, string sha, string path)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"check-attr --source {sha} -z filter {path.Quoted()}";
            RaiseError = false;
        }

        public bool GetResult()
        {
            return Parse(ReadToEnd());
        }

        public async Task<bool> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return Parse(rs);
        }

        private bool Parse(Result rs)
        {
            return rs.IsSuccess && rs.StdOut.Contains("filter\0lfs");
        }
    }
}
