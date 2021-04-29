using System;

namespace SourceGit.Commands {
    /// <summary>
    ///     子模块
    /// </summary>
    public class Submodule : Command {
        private Action<string> onProgress = null;

        public Submodule(string cwd) {
            Cwd = cwd;
        }

        public bool Add(string url, string path, bool recursive, Action<string> handler) {
            Args = $"submodule add {url} {path}";
            onProgress = handler;
            if (!Exec()) return false;

            if (recursive) {
                Args = $"submodule update --init --recursive -- {path}";
                return Exec();
            } else {
                return true;
            }
        }

        public bool Update() {
            Args = $"submodule update --rebase --remote";
            return Exec();
        }

        public bool Delete(string path) {
            Args = $"submodule deinit -f {path}";
            if (!Exec()) return false;

            Args = $"rm -rf {path}";
            return Exec();
        }

        public override void OnReadline(string line) {
            onProgress?.Invoke(line);
        }
    }
}
