using System;

namespace SourceGit.Commands {

    /// <summary>
    ///     拉回
    /// </summary>
    public class Pull : Command {
        private Action<string> handler = null;
        private bool needStash = false;

        public Pull(string repo, string remote, string branch, bool useRebase, bool autoStash, Action<string> onProgress) {
            Cwd = repo;
            Args = "-c credential.helper=manager pull --verbose --progress --tags ";
            TraitErrorAsOutput = true;
            handler = onProgress;

            if (useRebase) Args += "--rebase ";
            if (autoStash) {
                if (useRebase) Args += "--autostash ";
                else needStash = true;
            }

            Args += $"{remote} {branch}";
        }

        public bool Run() {
            if (needStash) {
                var changes = new LocalChanges(Cwd).Result();
                if (changes.Count > 0) {
                    if (!new Stash(Cwd).Push(null, "PULL_AUTO_STASH", true)) {
                        return false;
                    }
                } else {
                    needStash = false;
                }
            }

            var succ = Exec();
            if (needStash) new Stash(Cwd).Pop("stash@{0}");
            return succ;
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }
}
