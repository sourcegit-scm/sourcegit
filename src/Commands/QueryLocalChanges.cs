using System.Collections.Generic;
using System.Text.RegularExpressions;

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
            Args = $"status -u{UNTRACKED[includeUntracked ? 1 : 0]} --ignore-submodules=dirty --porcelain";
        }

        public List<Models.Change> Result()
        {
            Exec();
            return _changes;
        }

        protected override void OnReadline(string line)
        {
            var match = REG_FORMAT().Match(line);
            if (!match.Success)
                return;

            var change = new Models.Change() { Path = match.Groups[2].Value };
            var status = match.Groups[1].Value;

            switch (status)
            {
                case " M":
                    change.Set(Models.ChangeState.None, Models.ChangeState.Modified);
                    break;
                case " T":
                    change.Set(Models.ChangeState.None, Models.ChangeState.TypeChanged);
                    break;
                case " A":
                    change.Set(Models.ChangeState.None, Models.ChangeState.Added);
                    break;
                case " D":
                    change.Set(Models.ChangeState.None, Models.ChangeState.Deleted);
                    break;
                case " R":
                    change.Set(Models.ChangeState.None, Models.ChangeState.Renamed);
                    break;
                case " C":
                    change.Set(Models.ChangeState.None, Models.ChangeState.Copied);
                    break;
                case "M":
                    change.Set(Models.ChangeState.Modified);
                    break;
                case "MM":
                    change.Set(Models.ChangeState.Modified, Models.ChangeState.Modified);
                    break;
                case "MT":
                    change.Set(Models.ChangeState.Modified, Models.ChangeState.TypeChanged);
                    break;
                case "MD":
                    change.Set(Models.ChangeState.Modified, Models.ChangeState.Deleted);
                    break;
                case "T":
                    change.Set(Models.ChangeState.TypeChanged);
                    break;
                case "TM":
                    change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.Modified);
                    break;
                case "TT":
                    change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.TypeChanged);
                    break;
                case "TD":
                    change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.Deleted);
                    break;
                case "A":
                    change.Set(Models.ChangeState.Added);
                    break;
                case "AM":
                    change.Set(Models.ChangeState.Added, Models.ChangeState.Modified);
                    break;
                case "AT":
                    change.Set(Models.ChangeState.Added, Models.ChangeState.TypeChanged);
                    break;
                case "AD":
                    change.Set(Models.ChangeState.Added, Models.ChangeState.Deleted);
                    break;
                case "D":
                    change.Set(Models.ChangeState.Deleted);
                    break;
                case "R":
                    change.Set(Models.ChangeState.Renamed);
                    break;
                case "RM":
                    change.Set(Models.ChangeState.Renamed, Models.ChangeState.Modified);
                    break;
                case "RT":
                    change.Set(Models.ChangeState.Renamed, Models.ChangeState.TypeChanged);
                    break;
                case "RD":
                    change.Set(Models.ChangeState.Renamed, Models.ChangeState.Deleted);
                    break;
                case "C":
                    change.Set(Models.ChangeState.Copied);
                    break;
                case "CM":
                    change.Set(Models.ChangeState.Copied, Models.ChangeState.Modified);
                    break;
                case "CT":
                    change.Set(Models.ChangeState.Copied, Models.ChangeState.TypeChanged);
                    break;
                case "CD":
                    change.Set(Models.ChangeState.Copied, Models.ChangeState.Deleted);
                    break;
                case "DR":
                    change.Set(Models.ChangeState.Deleted, Models.ChangeState.Renamed);
                    break;
                case "DC":
                    change.Set(Models.ChangeState.Deleted, Models.ChangeState.Copied);
                    break;
                case "DD":
                    change.Set(Models.ChangeState.Deleted, Models.ChangeState.Deleted);
                    break;
                case "AU":
                    change.Set(Models.ChangeState.Added, Models.ChangeState.Unmerged);
                    break;
                case "UD":
                    change.Set(Models.ChangeState.Unmerged, Models.ChangeState.Deleted);
                    break;
                case "UA":
                    change.Set(Models.ChangeState.Unmerged, Models.ChangeState.Added);
                    break;
                case "DU":
                    change.Set(Models.ChangeState.Deleted, Models.ChangeState.Unmerged);
                    break;
                case "AA":
                    change.Set(Models.ChangeState.Added, Models.ChangeState.Added);
                    break;
                case "UU":
                    change.Set(Models.ChangeState.Unmerged, Models.ChangeState.Unmerged);
                    break;
                case "??":
                    change.Set(Models.ChangeState.Untracked, Models.ChangeState.Untracked);
                    break;
                default:
                    return;
            }

            _changes.Add(change);
        }

        private readonly List<Models.Change> _changes = new List<Models.Change>();
    }
}
