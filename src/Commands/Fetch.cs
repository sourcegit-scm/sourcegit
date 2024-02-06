using System;

namespace SourceGit.Commands {
    public class Fetch : Command {
        public Fetch(string repo, string remote, bool prune, Action<string> outputHandler) {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (!string.IsNullOrEmpty(sshKey)) {
                Args = $"-c core.sshCommand=\"ssh -i '{sshKey}'\" ";
            } else {
                Args = "-c credential.helper=manager ";
            }

            Args += "fetch --progress --verbose ";
            if (prune) Args += "--prune ";
            Args += remote;
        }

        public Fetch(string repo, string remote, string localBranch, string remoteBranch, Action<string> outputHandler) {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (!string.IsNullOrEmpty(sshKey)) {
                Args = $"-c core.sshCommand=\"ssh -i '{sshKey}'\" ";
            } else {
                Args = "-c credential.helper=manager ";
            }

            Args += $"fetch --progress --verbose {remote} {remoteBranch}:{localBranch}";
        }

        protected override void OnReadline(string line) {
            _outputHandler?.Invoke(line);
        }

        private Action<string> _outputHandler;
    }
}
