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
            Args = $"diff --no-color --no-ext-diff -a --ignore-cr-at-eol --check {opt}";
        }

        public bool GetResult()
        {
            return ReadToEnd().IsSuccess;
        }

        public async Task<bool> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess;
        }
    }
}
