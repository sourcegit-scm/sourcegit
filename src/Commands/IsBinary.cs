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
            Args = $"diff {Models.Commit.EmptyTreeSHA1} {commit} --numstat -- \"{path}\"";
            RaiseError = false;
        }

        public async Task<bool> ResultAsync()
        {
            return REG_TEST().IsMatch((await ReadToEndAsync()).StdOut);
        }
    }
}
