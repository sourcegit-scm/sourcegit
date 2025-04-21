namespace SourceGit.Commands
{
    public class GC : Command
    {
        public GC(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "gc --prune=now";
        }
    }
}
