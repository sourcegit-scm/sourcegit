using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    public class QuerySubmodules : Command {
        private readonly Regex REG_FORMAT1 = new Regex(@"^[\-\+ ][0-9a-f]+\s(.*)\s\(.*\)$");
        private readonly Regex REG_FORMAT2 = new Regex(@"^[\-\+ ][0-9a-f]+\s(.*)$");

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
            var match = REG_FORMAT1.Match(line);
            if (match.Success) {
                _submodules.Add(match.Groups[1].Value);
                return;
            }
            
            match = REG_FORMAT2.Match(line);
            if (match.Success) {
                _submodules.Add(match.Groups[1].Value);
            }
        }

        private List<string> _submodules = new List<string>();
    }
}
