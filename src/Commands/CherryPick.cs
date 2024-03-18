namespace SourceGit.Commands
{
    public class CherryPick : Command
    {
        public CherryPick(string repo, string commit, bool noCommit)
        {
            var mode = noCommit ? "-n" : "--ff";
            WorkingDirectory = repo;
            Context = repo;
            Args = $"cherry-pick {mode} {commit}";
        }
    }
}