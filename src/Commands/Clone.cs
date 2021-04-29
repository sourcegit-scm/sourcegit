using System;

namespace SourceGit.Commands {

    /// <summary>
    ///     克隆
    /// </summary>
    public class Clone : Command {
        private Action<string> handler = null;

        public Clone(string path, string url, string localName, string extraArgs, Action<string> outputHandler) {
            Cwd = path;
            TraitErrorAsOutput = true;
            Args = "-c credential.helper=manager clone --progress --verbose --recurse-submodules ";
            handler = outputHandler;

            if (!string.IsNullOrEmpty(extraArgs)) Args += $"{extraArgs} ";
            Args += $"{url} ";
            if (!string.IsNullOrEmpty(localName)) Args += localName;
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }
}
