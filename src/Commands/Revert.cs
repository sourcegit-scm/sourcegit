namespace SourceGit.Commands {
    /// <summary>
    ///     撤销提交
    /// </summary>
    public class Revert : Command {

        public Revert(string repo, string commit, bool autoCommit) {
            Cwd = repo;
            Args = $"revert {commit} --no-edit";
            if (!autoCommit) Args += "  --no-commit";
        }
    }
}
