using System;
using System.IO;

namespace SourceGit.Commands {
    /// <summary>
    ///     子树相关操作
    /// </summary>
    public class SubTree : Command {
        private Action<string> handler = null;

        public SubTree(string repo) {
            Cwd = repo;
            TraitErrorAsOutput = true;
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }

        public bool Add(string prefix, string source, string revision, bool squash, Action<string> onProgress) {
            var path = Path.Combine(Cwd, prefix);
            if (Directory.Exists(path)) return true;

            handler = onProgress;
            Args = $"subtree add --prefix=\"{prefix}\" {source} {revision}";
            if (squash) Args += " --squash";
            return Exec();
        }

        public void Pull(string prefix, string source, string branch, bool squash, Action<string> onProgress) {
            handler = onProgress;
            Args = $"subtree pull --prefix=\"{prefix}\" {source} {branch}";
            if (squash) Args += " --squash";
            Exec();
        }

        public void Push(string prefix, string source, string branch, Action<string> onProgress) {
            handler = onProgress;
            Args = $"subtree push --prefix=\"{prefix}\" {source} {branch}";
            Exec();
        }
    }
}
