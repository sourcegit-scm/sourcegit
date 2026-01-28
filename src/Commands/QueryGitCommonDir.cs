using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryGitCommonDir : Command
    {
        public QueryGitCommonDir(string workDir)
        {
            WorkingDirectory = workDir;
            Args = "rev-parse --git-common-dir";
            RaiseError = false;
        }

        public async Task<string> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess || string.IsNullOrEmpty(rs.StdOut))
                return string.Empty;

            var dir = rs.StdOut.Trim();
            if (Path.IsPathRooted(dir))
                return dir;
            return Path.GetFullPath(Path.Combine(WorkingDirectory, dir));
        }
    }
}
