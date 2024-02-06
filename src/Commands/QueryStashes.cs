using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    public class QueryStashes : Command {
        private static readonly Regex REG_STASH = new Regex(@"^Reflog: refs/(stash@\{\d+\}).*$");
        
        public QueryStashes(string repo) {
            WorkingDirectory = repo;
            Context = repo;
            Args = "stash list --pretty=raw";
        }

        public List<Models.Stash> Result() {
            Exec();
            if (_current != null) _stashes.Add(_current);
            return _stashes;
        }

        protected override void OnReadline(string line) {
            if (line.StartsWith("commit ", StringComparison.Ordinal)) {
                if (_current != null && !string.IsNullOrEmpty(_current.Name)) _stashes.Add(_current);
                _current = new Models.Stash() { SHA = line.Substring(7, 8) };
                return;
            }

            if (_current == null) return;

            if (line.StartsWith("Reflog: refs/stash@", StringComparison.Ordinal)) {
                var match = REG_STASH.Match(line);
                if (match.Success) _current.Name = match.Groups[1].Value;
            } else if (line.StartsWith("Reflog message: ", StringComparison.Ordinal)) {
                _current.Message = line.Substring(16);
            } else if (line.StartsWith("author ", StringComparison.Ordinal)) {
                Models.User user = Models.User.Invalid;
                ulong time = 0;
                Models.Commit.ParseUserAndTime(line.Substring(7), ref user, ref time);
                _current.Author = user;
                _current.Time = time;
            }
        }

        private List<Models.Stash> _stashes = new List<Models.Stash>();
        private Models.Stash _current = null;
    }
}
