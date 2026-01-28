namespace SourceGit.Commands
{
    public class Restore : Command
    {
        public Restore(string repo, string pathspecFile)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"restore --progress --worktree --recurse-submodules --pathspec-from-file={pathspecFile.Quoted()}";
        }
    }
}
