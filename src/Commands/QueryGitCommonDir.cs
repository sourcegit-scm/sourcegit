using System.IO;

namespace SourceGit.Commands
{
    public class QueryGitCommonDir : Command
    {
        public QueryGitCommonDir(string workDir)
        {
            WorkingDirectory = workDir;
            Args = "rev-parse --git-common-dir";
        }

        public string GetResult()
        {
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return null;

            var stdout = rs.StdOut.Trim();
            if (string.IsNullOrEmpty(stdout))
                return null;

            return Path.IsPathRooted(stdout) ? stdout : Path.GetFullPath(Path.Combine(WorkingDirectory, stdout));
        }
    }
}
