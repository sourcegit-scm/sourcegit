using System;

namespace SourceGit.Commands {

    /// <summary>
    ///     克隆
    /// </summary>
    public class Clone : Command {
        private Action<string> handler = null;
        private Action<string> onError = null;

        public Clone(string path, string url, string localName, string sshKey, string extraArgs, Action<string> outputHandler, Action<string> errHandler) {
            Cwd = path;
            TraitErrorAsOutput = true;
            handler = outputHandler;
            onError = errHandler;

            if (!string.IsNullOrEmpty(sshKey)) {
                Envs.Add("GIT_SSH_COMMAND", $"ssh -i '{sshKey}'");
                Args = "";
            } else {
                Args = "-c credential.helper=manager-core ";
            }

            Args += "clone --progress --verbose --recurse-submodules ";

            if (!string.IsNullOrEmpty(extraArgs)) Args += $"{extraArgs} ";
            Args += $"{url} ";
            if (!string.IsNullOrEmpty(localName)) Args += localName;
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }

        public override void OnException(string message) {
            onError?.Invoke(message);
        }
    }
}
