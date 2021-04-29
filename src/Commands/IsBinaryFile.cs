using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     查询指定版本下的某文件是否是二进制文件
    /// </summary>
    public class IsBinaryFile : Command {
        private static readonly Regex REG_TEST = new Regex(@"^\-\s+\-\s+.*$");
        public IsBinaryFile(string repo, string commit, string path) {
            Cwd = repo;
            Args = $"diff 4b825dc642cb6eb9a060e54bf8d69288fbee4904 {commit} --numstat -- \"{path}\"";
        }

        public bool Result() {
            return REG_TEST.IsMatch(ReadToEnd().Output);
        }
    }
}
