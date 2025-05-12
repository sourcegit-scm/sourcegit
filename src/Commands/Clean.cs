namespace SourceGit.Commands
{
    public class Clean : Command
    {
        public Clean(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "clean -qfdx";
        }
    }
}
