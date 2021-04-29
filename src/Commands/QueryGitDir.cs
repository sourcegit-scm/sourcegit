using System.IO;

namespace SourceGit.Commands {

    /// <summary>
    ///     取得GitDir
    /// </summary>
    public class QueryGitDir : Command {
        public QueryGitDir(string workDir) {
            Cwd = workDir;
            Args = "rev-parse --git-dir";
        }

        public string Result() {
            var rs = ReadToEnd().Output;
            if (string.IsNullOrEmpty(rs)) return null;

            rs = rs.Trim();
            if (Path.IsPathRooted(rs)) return rs;
            return Path.Combine(Cwd, rs);
        }
    }
}
