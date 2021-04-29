using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands {
    /// <summary>
    ///     清理指令
    /// </summary>
    public class Clean : Command {

        public Clean(string repo) {
            Cwd = repo;
            Args = "clean -qfd";
        }

        public Clean(string repo, List<string> files) {
            StringBuilder builder = new StringBuilder();
            builder.Append("clean -qfd --");
            foreach (var f in files) {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }

            Cwd = repo;
            Args = builder.ToString();
        }
    }
}
