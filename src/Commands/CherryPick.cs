namespace SourceGit.Commands {
    /// <summary>
    ///     遴选命令
    /// </summary>
    public class CherryPick : Command {

        public CherryPick(string repo, string commit, bool noCommit) {
            var mode = noCommit ? "-n" : "--ff";
            Cwd = repo;
            Args = $"cherry-pick {mode} {commit}";
        }
    }
}
