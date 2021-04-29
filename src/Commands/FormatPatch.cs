namespace SourceGit.Commands {
    /// <summary>
    ///     将Commit另存为Patch文件
    /// </summary>
    public class FormatPatch : Command {

        public FormatPatch(string repo, string commit, string path) {
            Cwd = repo;
            Args = $"format-patch {commit} -1 -o \"{path}\"";
        }
    }
}
