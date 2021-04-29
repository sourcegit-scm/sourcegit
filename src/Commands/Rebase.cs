namespace SourceGit.Commands {
    /// <summary>
    ///     变基命令
    /// </summary>
    public class Rebase : Command {

        public Rebase(string repo, string basedOn, bool autoStash) {
            Cwd = repo;
            Args = "rebase ";
            if (autoStash) Args += "--autostash ";
            Args += basedOn;
        }
    }
}
