using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands {
    /// <summary>
    ///     单个贮藏相关操作
    /// </summary>
    public class Stash : Command {

        public Stash(string repo) {
            Cwd = repo;
        }

        public bool Push(List<string> files, string message, bool includeUntracked) {
            StringBuilder builder = new StringBuilder();
            builder.Append("stash push ");
            if (includeUntracked) builder.Append("-u ");
            builder.Append("-m \"");
            builder.Append(message);
            builder.Append("\" ");

            if (files != null && files.Count > 0) {
                builder.Append("--");
                foreach (var f in files) {
                    builder.Append(" \"");
                    builder.Append(f);
                    builder.Append("\"");
                }
            }

            Args = builder.ToString();
            return Exec();
        }

        public bool Apply(string name) {
            Args = $"stash apply -q {name}";
            return Exec();
        }

        public bool Pop(string name) {
            Args = $"stash pop -q {name}";
            return Exec();
        }

        public bool Drop(string name) {
            Args = $"stash drop -q {name}";
            return Exec();
        }
    }
}
