using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     解析所有的分支
    /// </summary>
    public class Branches : Command {
        private static readonly string PREFIX_LOCAL = "refs/heads/";
        private static readonly string PREFIX_REMOTE = "refs/remotes/";
        private static readonly string CMD = "branch -l --all -v --format=\"$%(refname)$%(objectname)$%(HEAD)$%(upstream)$%(upstream:track)$%(contents:subject)\"";
        private static readonly Regex REG_FORMAT = new Regex(@"\$(.*)\$(.*)\$([\* ])\$(.*)\$(.*?)\$(.*)");
        private static readonly Regex REG_AHEAD = new Regex(@"ahead (\d+)");
        private static readonly Regex REG_BEHIND = new Regex(@"behind (\d+)");

        private List<Models.Branch> loaded = new List<Models.Branch>();

        public Branches(string path) {
            Cwd = path;
            Args = CMD;
        }

        public List<Models.Branch> Result() {
            Exec();
            return loaded;
        }

        public override void OnReadline(string line) {
            var match = REG_FORMAT.Match(line);
            if (!match.Success) return;

            var branch = new Models.Branch();
            var refName = match.Groups[1].Value;
            if (refName.EndsWith("/HEAD")) return;

            if (refName.StartsWith(PREFIX_LOCAL, StringComparison.Ordinal)) {
                branch.Name = refName.Substring(PREFIX_LOCAL.Length);
                branch.IsLocal = true;
            } else if (refName.StartsWith(PREFIX_REMOTE, StringComparison.Ordinal)) {
                var name = refName.Substring(PREFIX_REMOTE.Length);
                branch.Remote = name.Substring(0, name.IndexOf('/'));
                branch.Name = name.Substring(branch.Remote.Length + 1);
                branch.IsLocal = false;
            } else {
                branch.Name = refName;
                branch.IsLocal = true;
            }

            branch.FullName = refName;
            branch.Head = match.Groups[2].Value;
            branch.IsCurrent = match.Groups[3].Value == "*";
            branch.Upstream = match.Groups[4].Value;
            branch.UpstreamTrackStatus = ParseTrackStatus(match.Groups[5].Value);
            branch.HeadSubject = match.Groups[6].Value;

            loaded.Add(branch);
        }

        private string ParseTrackStatus(string data) {
            if (string.IsNullOrEmpty(data)) return "";

            string track = "";

            var ahead = REG_AHEAD.Match(data);
            if (ahead.Success) {
                track += ahead.Groups[1].Value + "↑ ";
            }

            var behind = REG_BEHIND.Match(data);
            if (behind.Success) {
                track += behind.Groups[1].Value + "↓";
            }

            return track.Trim();
        }
    }
}
