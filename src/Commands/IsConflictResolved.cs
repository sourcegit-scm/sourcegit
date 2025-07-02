using System.Threading.Tasks;

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

        public bool Result()
        {
            return ReadToEnd().IsSuccess;
        }

        public async Task<bool> ResultAsync()
        {
            return (await ReadToEndAsync()).IsSuccess;
        }
    }
}
