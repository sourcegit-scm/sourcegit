using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryLocalChanges : Command
    {
        [GeneratedRegex(@"^(\s?[\w\?]{1,4})\s+(.+)$")]
        private static partial Regex REG_FORMAT();
        private static readonly string[] UNTRACKED = ["no", "all"];

        public QueryLocalChanges(string repo, bool includeUntracked = true)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"--no-optional-locks status -u{UNTRACKED[includeUntracked ? 1 : 0]} --ignore-submodules=dirty --porcelain";
        }

        public async Task<List<Models.Change>> GetResultAsync()
        {
            var outs = new List<Models.Change>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
            {
                App.RaiseException(Context, rs.StdErr);
                return outs;
            }

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_FORMAT().Match(line);
                if (!match.Success)
                    continue;

                var change = new Models.Change() { Path = match.Groups[2].Value };
                var status = match.Groups[1].Value;

                var index = status[0] switch
                {
                    'M' => Models.ChangeState.Modified,
                    'T' => Models.ChangeState.TypeChanged,
                    'A' => Models.ChangeState.Added,
                    'D' => Models.ChangeState.Deleted,
                    'R' => Models.ChangeState.Renamed,
                    'C' => Models.ChangeState.Copied,
                    'U' => Models.ChangeState.Untracked,
                    _ => Models.ChangeState.None
                };
                var workTree = Models.ChangeState.None;
                if (status.Length > 1)
                {
                    workTree = status[1] switch
                    {
                        'M' => Models.ChangeState.Modified,
                        'T' => Models.ChangeState.TypeChanged,
                        'A' => Models.ChangeState.Added,
                        'D' => Models.ChangeState.Deleted,
                        'R' => Models.ChangeState.Renamed,
                        'C' => Models.ChangeState.Copied,
                        'U' or '?' => Models.ChangeState.Untracked,
                        _ => Models.ChangeState.None
                    };
                }
                change.ConflictReason = status switch
                {
                    "DD" => Models.ConflictReason.BothDeleted,
                    "AU" => Models.ConflictReason.AddedByUs,
                    "UD" => Models.ConflictReason.DeletedByThem,
                    "UA" => Models.ConflictReason.AddedByThem,
                    "DU" => Models.ConflictReason.DeletedByUs,
                    "AA" => Models.ConflictReason.BothAdded,
                    "UU" => Models.ConflictReason.BothModified,
                    _ => Models.ConflictReason.None
                };
                if (change.ConflictReason != Models.ConflictReason.None)
                    change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                else
                    change.Set(index, workTree);

                if (change.Index != Models.ChangeState.None || change.WorkTree != Models.ChangeState.None)
                    outs.Add(change);
            }

            return outs;
        }
    }
}
