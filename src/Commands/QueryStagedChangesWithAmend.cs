using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QueryStagedChangesWithAmend : Command
    {
        [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{4,64}) [0-9a-f]{4,64} ([ADMT])\d{0,6}$")]
        private static partial Regex REG_FORMAT1();
        [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{4,64}) [0-9a-f]{4,64} ([RC])\d{0,6}$")]
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
            Args = $"diff-index --cached -M -z {parent}";
            rs = ReadToEnd();
            if (!rs.IsSuccess)
                return [];

            var changes = new List<Models.Change>();
            var lines = rs.StdOut.Split('\0');
            var waitingForOriginalPath = false;
            var waitingForPath = false;
            foreach (var line in lines)
            {
                if (waitingForOriginalPath)
                {
                    if (changes.Count > 0)
                        changes[^1].OriginalPath = line;

                    waitingForOriginalPath = false;
                    continue;
                }

                if (waitingForPath)
                {
                    if (changes.Count > 0)
                        changes[^1].Path = line;

                    waitingForPath = false;
                    continue;
                }

                var match = REG_FORMAT1().Match(line);
                if (match.Success)
                {
                    var change = new Models.Change()
                    {
                        Index = Models.Change.ParseState(match.Groups[3].Value[0]),
                        DataForAmend = new Models.ChangeDataForAmend()
                        {
                            FileMode = match.Groups[1].Value,
                            ObjectHash = match.Groups[2].Value,
                            ParentSHA = parent,
                        },
                    };

                    changes.Add(change);
                    waitingForPath = true;
                    continue;
                }

                match = REG_FORMAT2().Match(line);
                if (match.Success)
                {
                    var change = new Models.Change()
                    {
                        Index = Models.Change.ParseState(match.Groups[3].Value[0]),
                        DataForAmend = new Models.ChangeDataForAmend()
                        {
                            FileMode = match.Groups[1].Value,
                            ObjectHash = match.Groups[2].Value,
                            ParentSHA = parent,
                        },
                    };

                    changes.Add(change);
                    waitingForPath = true;
                    waitingForOriginalPath = true;
                    continue;
                }
            }

            return changes;
        }
    }
}
