using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    public class QuerySubmodules : Command {
        private readonly Regex REG_FORMAT = new Regex(@"^[\-\+ ][0-9a-f]+\s(.*)\s\(.*\)$");

        public QuerySubmodules(string repo) {
            WorkingDirectory = repo;
            Context = repo;
            Args = "submodule status";
        }

        public List<string> Result() {
            Exec();
            return _submodules;
        }

        protected override void OnReadline(string line) {
            var match = REG_FORMAT.Match(line);
            if (!match.Success) return;
            _submodules.Add(match.Groups[1].Value);
        }

        private List<string> _submodules = new List<string>();
    }
}
