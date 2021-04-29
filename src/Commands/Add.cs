using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands {
    /// <summary>
    ///     `git add`命令
    /// </summary>
    public class Add : Command {
        public Add(string repo) {
            Cwd = repo;
            Args = "add .";
        }

        public Add(string repo, List<string> paths) {
            StringBuilder builder = new StringBuilder();
            builder.Append("add --");
            foreach (var p in paths) {
                builder.Append(" \"");
                builder.Append(p);
                builder.Append("\"");
            }

            Cwd = repo;
            Args = builder.ToString();
        }
    }
}
