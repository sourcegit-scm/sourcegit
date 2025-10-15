using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class IsBinary : Command
    {
        [GeneratedRegex(@"^\-\s+\-\s+.*$")]
        private static partial Regex REG_TEST();

        public IsBinary(string repo, string commit, string path)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"diff {Models.Commit.EmptyTreeSHA1} {commit} --numstat -- {path.Quoted()}";
            RaiseError = false;
        }

        public async Task<bool> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return REG_TEST().IsMatch(rs.StdOut.Trim());
        }
    }
}
