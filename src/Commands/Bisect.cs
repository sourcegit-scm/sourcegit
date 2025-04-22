namespace SourceGit.Commands
{
    public class Bisect : Command
    {
        public Bisect(string repo, string subcmd)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"bisect {subcmd}";
        }
    }
}
