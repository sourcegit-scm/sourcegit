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

        public Reset(string repo, string pathspec)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"reset HEAD --pathspec-from-file={pathspec.Quoted()}";
        }
    }
}
