namespace SourceGit.Commands {
    /// <summary>
    ///     合并分支
    /// </summary>
    public class Merge : Command {

        public Merge(string repo, string source, string mode) {
            Cwd = repo;
            Args = $"merge {source} {mode}";
        }
    }
}
