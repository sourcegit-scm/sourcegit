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
            TraitErrorAsOutput = true;
            handler = onProgress;
            needStash = autoStash;

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (!string.IsNullOrEmpty(sshKey)) {
                Envs.Add("GIT_SSH_COMMAND", $"ssh -i '{sshKey}'");
                Args = "";
            } else {
                Args = "-c credential.helper=manager-core ";
            }

            Args += "pull --verbose --progress --tags ";
            if (useRebase) Args += "--rebase ";
            Args += $"{remote} {branch}";
        }

        public bool Run() {
            if (needStash) {
                var changes = new LocalChanges(Cwd).Result();
                if (changes.Count > 0) {
                    if (!new Stash(Cwd).Push(changes, "PULL_AUTO_STASH")) {
                        return false;
                    }
                } else {
                    needStash = false;
                }
            }

            var succ = Exec();
            if (succ && needStash) new Stash(Cwd).Pop("stash@{0}");
            return succ;
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }
}
