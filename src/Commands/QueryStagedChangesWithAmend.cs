using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QueryStagedChangesWithAmend : Command
    {
        [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{4,64}) [0-9a-f]{4,64} ([ADMT])\d{0,6}\t(.*)$")]
        private static partial Regex REG_FORMAT1();
        [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{4,64}) [0-9a-f]{4,64} ([RC])\d{0,6}\t(.*\t.*)$")]
        private static partial Regex REG_FORMAT2();

        public QueryStagedChangesWithAmend(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public List<Models.Change> GetResult()
        {
            Args = "show --no-show-signature --format=\"%H %P\" -s HEAD";
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return [];

            var shas = rs.StdOut.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (shas.Length == 0)
                return [];

            var parent = shas.Length > 1 ? shas[1] : Models.EmptyTreeHash.Guess(shas[0]);
            Args = $"diff-index --cached -M {parent}";
            rs = ReadToEnd();
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
                        Path = match.Groups[4].Value,
                        DataForAmend = new Models.ChangeDataForAmend()
                        {
                            FileMode = match.Groups[1].Value,
                            ObjectHash = match.Groups[2].Value,
                            ParentSHA = parent,
                        },
                    };
                    var type = match.Groups[3].Value;
                    change.Set(type == "R" ? Models.ChangeState.Renamed : Models.ChangeState.Copied);
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
                            ParentSHA = parent,
                        },
                    };

                    var type = match.Groups[3].Value;
                    switch (type)
                    {
                        case "A":
                            change.Set(Models.ChangeState.Added);
                            break;
                        case "D":
                            change.Set(Models.ChangeState.Deleted);
                            break;
                        case "M":
                            change.Set(Models.ChangeState.Modified);
                            break;
                        case "T":
                            change.Set(Models.ChangeState.TypeChanged);
                            break;
                    }
                    changes.Add(change);
                }
            }

            return changes;
        }
    }
}
