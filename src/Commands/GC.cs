using System;

namespace SourceGit.Commands {
    /// <summary>
    ///     GC
    /// </summary>
    public class GC : Command {
        private Action<string> handler;

        public GC(string repo, Action<string> onProgress) {
            Cwd = repo;
            Args = "gc";
            TraitErrorAsOutput = true;
            handler = onProgress;
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }
}
