namespace SourceGit.Commands
{
    public class Add : Command
    {
        public Add(string repo, bool includeUntracked)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = includeUntracked ? "add ." : "add -u .";
        }

        public Add(string repo, Models.Change change)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"add -- {change.Path.Quoted()}";
        }

        public Add(string repo, string pathspecFromFile)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"add --pathspec-from-file={pathspecFromFile.Quoted()}";
        }
    }
}
