using System;

namespace SourceGit.Commands {

    /// <summary>
    ///     存档命令
    /// </summary>
    public class Archive : Command {
        private Action<string> handler;

        public Archive(string repo, string revision, string to, Action<string> onProgress) {
            Cwd = repo;
            Args = $"archive --format=zip --verbose --output=\"{to}\" {revision}";
            TraitErrorAsOutput = true;
            handler = onProgress;
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }
}
