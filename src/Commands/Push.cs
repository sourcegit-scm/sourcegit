using System;

namespace SourceGit.Commands {
    /// <summary>
    ///     推送
    /// </summary>
    public class Push : Command {
        private Action<string> handler = null;

        public Push(string repo, string local, string remote, string remoteBranch, bool withTags, bool force, bool track, Action<string> onProgress) {
            Cwd = repo;
            TraitErrorAsOutput = true;
            handler = onProgress;

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (!string.IsNullOrEmpty(sshKey)) {
                Envs.Add("GIT_SSH_COMMAND", $"ssh -i '{sshKey}'");
                Args = "";
            } else {
                Args = "-c credential.helper=manager-core ";
            }

            Args += "push --progress --verbose ";

            if (withTags) Args += "--tags ";
            if (track) Args += "-u ";
            if (force) Args += "--force-with-lease ";

            Args += $"{remote} {local}:{remoteBranch}";
        }

        public Push(string repo, string remote, string branch) {
            Cwd = repo;

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (!string.IsNullOrEmpty(sshKey)) {
                Envs.Add("GIT_SSH_COMMAND", $"ssh -i '{sshKey}'");
                Args = "";
            } else {
                Args = "-c credential.helper=manager-core ";
            }

            Args += $"push {remote} --delete {branch}";
        }

        public Push(string repo, string remote, string tag, bool isDelete) {
            Cwd = repo;

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (!string.IsNullOrEmpty(sshKey)) {
                Envs.Add("GIT_SSH_COMMAND", $"ssh -i '{sshKey}'");
                Args = "";
            } else {
                Args = "-c credential.helper=manager-core ";
            }

            Args += "push ";
            if (isDelete) Args += "--delete ";
            Args += $"{remote} refs/tags/{tag}";
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }
}
