namespace SourceGit.Commands
{
    public class Reset : Command
    {
        public Reset(string repo, string revision, string mode)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"reset {mode} {revision}";
        }
    }
}
