using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Lists the untracked and/or ignored paths that <c>git clean</c> would remove, WITHOUT
    ///     deleting anything. The discard "safetynet" uses this to move those paths into the
    ///     trash bin (see <see cref="Models.TrashBin"/>) instead of letting <c>git clean</c>
    ///     erase them permanently.
    ///
    ///     Output is machine-readable and locale-independent (porcelain, NUL-separated). The
    ///     default untracked mode (<c>-unormal</c>) reports a wholly-untracked directory as a
    ///     single <c>dir/</c> entry, so the whole directory can be trashed in one move without
    ///     leaving empty parent folders behind.
    /// </summary>
    public class QueryCleanablePaths : Command
    {
        public QueryCleanablePaths(string repo, Models.CleanMode mode)
        {
            WorkingDirectory = repo;
            Context = repo;
            _mode = mode;

            Args = "--no-optional-locks status --porcelain -z --ignored --ignore-submodules=dirty";
        }

        public async Task<List<string>> GetResultAsync()
        {
            var paths = new List<string>();

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess || string.IsNullOrEmpty(rs.StdOut))
                return paths;

            var entries = rs.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                // Porcelain -z entry format: "XY <path>", where XY is the two-char status code.
                if (entry.Length < 4)
                    continue;

                var isUntracked = entry[0] == '?' && entry[1] == '?';
                var isIgnored = entry[0] == '!' && entry[1] == '!';

                var wanted = _mode switch
                {
                    Models.CleanMode.OnlyUntrackedFiles => isUntracked,
                    Models.CleanMode.OnlyIgnoredFiles => isIgnored,
                    _ => isUntracked || isIgnored,
                };

                if (wanted)
                    paths.Add(entry.Substring(3));
            }

            return paths;
        }

        private readonly Models.CleanMode _mode;
    }
}
