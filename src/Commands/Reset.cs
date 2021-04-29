using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands {
    /// <summary>
    ///     重置命令
    /// </summary>
    public class Reset : Command {

        public Reset(string repo) {
            Cwd = repo;
            Args = "reset";
        }

        public Reset(string repo, string revision, string mode) {
            Cwd = repo;
            Args = $"reset {mode} {revision}";
        }

        public Reset(string repo, List<string> files) {
            Cwd = repo;

            StringBuilder builder = new StringBuilder();
            builder.Append("reset --");
            foreach (var f in files) {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
        }
    }
}
