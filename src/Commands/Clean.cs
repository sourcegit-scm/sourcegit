using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands {
    public class Clean : Command {
        public Clean(string repo) {
            WorkingDirectory = repo;
            Context = repo;
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

            WorkingDirectory = repo;
            Context = repo;
            Args = builder.ToString();
        }
    }
}
