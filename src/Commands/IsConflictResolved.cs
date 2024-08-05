namespace SourceGit.Commands
{
    public class IsConflictResolved : Command
    {
        public IsConflictResolved(string repo, Models.Change change)
        {
            var opt = new Models.DiffOption(change, true);

            WorkingDirectory = repo;
            Context = repo;
            Args = $"diff -a --ignore-cr-at-eol --check {opt}";
        }
    }
}
