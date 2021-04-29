using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     获取远程列表
    /// </summary>
    public class Remotes : Command {
        private static readonly Regex REG_REMOTE = new Regex(@"^([\w\.\-]+)\s*(\S+).*$");
        private List<Models.Remote> loaded = new List<Models.Remote>();

        public Remotes(string repo) {
            Cwd = repo;
            Args = "remote -v";
        }

        public List<Models.Remote> Result() {
            Exec();
            return loaded;
        }

        public override void OnReadline(string line) {
            var match = REG_REMOTE.Match(line);
            if (!match.Success) return;

            var remote = new Models.Remote() {
                Name = match.Groups[1].Value,
                URL = match.Groups[2].Value,
            };

            if (loaded.Find(x => x.Name == remote.Name) != null) return;
            loaded.Add(remote);
        }
    }
}
