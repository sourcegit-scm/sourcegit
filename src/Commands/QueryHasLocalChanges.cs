using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Lightweight check used by repositories in inactive tabs: only tells whether there are local changes,
    ///     without parsing them, so the tab's dirty indicator stays up to date.
    /// </summary>
    public class QueryHasLocalChanges : Command
    {
        public QueryHasLocalChanges(string repo, bool includeUntracked)
        {
            WorkingDirectory = repo;
            Context = repo;
            RaiseError = false;
            Args = includeUntracked
                ? "--no-optional-locks status -unormal --ignore-submodules=dirty --porcelain"
                : "--no-optional-locks status -uno --ignore-submodules=dirty --porcelain";
        }

        public async Task<bool?> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return null;

            return !string.IsNullOrWhiteSpace(rs.StdOut);
        }
    }
}
