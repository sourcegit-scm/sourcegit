namespace SourceGit.Commands {
    public class Revert : Command {
        public Revert(string repo, string commit, bool autoCommit) {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"revert {commit} --no-edit";
            if (!autoCommit) Args += "  --no-commit";
        }
    }
}
