namespace SourceGit.Commands {
    /// <summary>
    ///     清理指令
    /// </summary>
    public class Clean : Command {

        public Clean(string repo) {
            Cwd = repo;
            Args = "clean -qfd";
        }
    }
}
