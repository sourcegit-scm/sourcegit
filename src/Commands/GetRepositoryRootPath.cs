namespace SourceGit.Commands {
    /// <summary>
    ///     取得一个库的根路径
    /// </summary>
    public class GetRepositoryRootPath : Command {
        public GetRepositoryRootPath(string path) {
            Cwd = path;
            Args = "rev-parse --show-toplevel";
        }

        public string Result() {
            var rs = ReadToEnd().Output;
            if (string.IsNullOrEmpty(rs)) return null;
            return rs.Trim();
        }
    }
}
