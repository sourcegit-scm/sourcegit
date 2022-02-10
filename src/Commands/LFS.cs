using System;
using System.IO;

namespace SourceGit.Commands {
    /// <summary>
    ///     LFS相关
    /// </summary>
    public class LFS {
        private string repo;

        private class PruneCmd : Command {
            private Action<string> handler;

            public PruneCmd(string repo, Action<string> onProgress) {
                Cwd = repo;
                Args = "lfs prune";
                TraitErrorAsOutput = true;
                handler = onProgress;
            }

            public override void OnReadline(string line) {
                handler?.Invoke(line);
            }
        }

        public LFS(string repo) {
            this.repo = repo;
        }

        public bool IsEnabled() {
            var path = Path.Combine(repo, ".git", "hooks", "pre-push");
            if (!File.Exists(path)) return false;

            var content = File.ReadAllText(path);
            return content.Contains("git lfs pre-push");
        }

        public bool IsFiltered(string path) {
            var cmd = new Command();
            cmd.Cwd = repo;
            cmd.Args = $"check-attr -a -z \"{path}\"";

            var rs = cmd.ReadToEnd();
            return rs.Output.Contains("filter\0lfs");
        }

        public void Prune(Action<string> onProgress) {
            new PruneCmd(repo, onProgress).Exec();
        }
    }
}
