using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands {
    /// <summary>
    ///     检出
    /// </summary>
    public class Checkout : Command {

        public Checkout(string repo) {
            Cwd = repo;
        }

        public bool Branch(string branch) {
            Args = $"checkout {branch}";
            return Exec();
        }

        public bool Branch(string branch, string basedOn) {
            Args = $"checkout -b {branch} {basedOn}";
            return Exec();
        }

        public bool File(string file, bool useTheirs) {
            if (useTheirs) {
                Args = $"checkout --theirs -- \"{file}\"";
            } else {
                Args = $"checkout --ours -- \"{file}\"";
            }

            return Exec();
        }

        public bool Files(List<string> files) {
            StringBuilder builder = new StringBuilder();
            builder.Append("checkout -f -q --");
            foreach (var f in files) {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
            return Exec();
        }
    }
}
