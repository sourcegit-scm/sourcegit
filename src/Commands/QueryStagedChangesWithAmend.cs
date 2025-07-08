using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryStagedChangesWithAmend : Command
    {
        [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{40}) [0-9a-f]{40} ([ACDMT])\d{0,6}\t(.*)$")]
        private static partial Regex REG_FORMAT1();
        [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{40}) [0-9a-f]{40} R\d{0,6}\t(.*\t.*)$")]
        private static partial Regex REG_FORMAT2();

        public QueryStagedChangesWithAmend(string repo, string parent)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"diff-index --cached -M {parent}";
            _parent = parent;
        }

        public async Task<List<Models.Change>> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return [];

            var changes = new List<Models.Change>();
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_FORMAT2().Match(line);
                if (match.Success)
                {
                    var change = new Models.Change()
                    {
                        Path = match.Groups[3].Value,
                        DataForAmend = new Models.ChangeDataForAmend()
                        {
                            FileMode = match.Groups[1].Value,
                            ObjectHash = match.Groups[2].Value,
                            ParentSHA = _parent,
                        },
                    };
                    change.Set(Models.ChangeState.Renamed);
                    changes.Add(change);
                    continue;
                }

                match = REG_FORMAT1().Match(line);
                if (match.Success)
                {
                    var change = new Models.Change()
                    {
                        Path = match.Groups[4].Value,
                        DataForAmend = new Models.ChangeDataForAmend()
                        {
                            FileMode = match.Groups[1].Value,
                            ObjectHash = match.Groups[2].Value,
                            ParentSHA = _parent,
                        },
                    };

                    var type = match.Groups[3].Value;
                    var state = type switch
                    {
                        "A" => Models.ChangeState.Added,
                        "C" => Models.ChangeState.Copied,
                        "D" => Models.ChangeState.Deleted,
                        "M" => Models.ChangeState.Modified,
                        "T" => Models.ChangeState.TypeChanged,
                        _ => Models.ChangeState.None
                    };
                    if (state != Models.ChangeState.None)
                        change.Set(state);
                    changes.Add(change);
                }
            }

            return changes;
        }

        private readonly string _parent;
    }
}
