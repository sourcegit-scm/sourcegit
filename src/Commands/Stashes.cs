using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     解析当前仓库中的贮藏
    /// </summary>
    public class Stashes : Command {
        private static readonly Regex REG_STASH = new Regex(@"^Reflog: refs/(stash@\{\d+\}).*$");
        private List<Models.Stash> parsed = new List<Models.Stash>();
        private Models.Stash current = null;

        public Stashes(string path) {
            Cwd = path;
            Args = "stash list --pretty=raw";
        }

        public List<Models.Stash> Result() {
            Exec();
            if (current != null) parsed.Add(current);
            return parsed;
        }

        public override void OnReadline(string line) {
            if (line.StartsWith("commit ", StringComparison.Ordinal)) {
                if (current != null && !string.IsNullOrEmpty(current.Name)) parsed.Add(current);
                current = new Models.Stash() { SHA = line.Substring(7, 8) };
                return;
            }

            if (current == null) return;

            if (line.StartsWith("Reflog: refs/stash@", StringComparison.Ordinal)) {
                var match = REG_STASH.Match(line);
                if (match.Success) current.Name = match.Groups[1].Value;
            } else if (line.StartsWith("Reflog message: ", StringComparison.Ordinal)) {
                current.Message = line.Substring(16);
            } else if (line.StartsWith("author ", StringComparison.Ordinal)) {
                Models.User user = Models.User.Invalid;
                ulong time = 0;
                Models.Commit.ParseUserAndTime(line.Substring(7), ref user, ref time);
                current.Author = user;
                current.Time = time;
            }
        }
    }
}
