using System;

namespace SourceGit.Commands {
    /// <summary>
    ///     合并分支
    /// </summary>
    public class Merge : Command {
        private Action<string> handler = null;

        public Merge(string repo, string source, string mode, Action<string> onProgress) {
            Cwd = repo;
            Args = $"merge --progress {source} {mode}";
            TraitErrorAsOutput = true;
            handler = onProgress;
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }
}
