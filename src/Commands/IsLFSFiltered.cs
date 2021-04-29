namespace SourceGit.Commands {
    /// <summary>
    ///     检测目录是否被LFS管理
    /// </summary>
    public class IsLFSFiltered : Command {
        public IsLFSFiltered(string cwd, string path) {
            Cwd = cwd;
            Args = $"check-attr -a -z \"{path}\"";
        }

        public bool Result() {
            var rs = ReadToEnd();
            return rs.Output.Contains("filter\0lfs");
        }
    }
}
