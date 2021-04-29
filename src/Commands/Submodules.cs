using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     获取子模块列表
    /// </summary>
    public class Submodules : Command {
        private readonly Regex REG_FORMAT = new Regex(@"^[\-\+ ][0-9a-f]+\s(.*)\s\(.*\)$");
        private List<string> modules = new List<string>();

        public Submodules(string repo) {
            Cwd = repo;
            Args = "submodule status";
        }

        public List<string> Result() {
            Exec();
            return modules;
        }

        public override void OnReadline(string line) {
            var match = REG_FORMAT.Match(line);
            if (!match.Success) return;
            modules.Add(match.Groups[1].Value);
        }
    }
}
