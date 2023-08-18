using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands {
    /// <summary>
    ///     检出
    /// </summary>
    public class Checkout : Command {
        private Action<string> handler = null;

        public Checkout(string repo) {
            Cwd = repo;
        }

        public bool Branch(string branch, Action<string> onProgress) {
            Args = $"checkout --progress {branch}";
            TraitErrorAsOutput = true;
            handler = onProgress;
            return Exec();
        }

        public bool Branch(string branch, string basedOn, Action<string> onProgress) {
            Args = $"checkout --progress -b {branch} {basedOn}";
            TraitErrorAsOutput = true;
            handler = onProgress;
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

        public bool FileWithRevision(string file, string revision) {
            Args = $"checkout {revision} -- \"{file}\"";
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

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }
}
