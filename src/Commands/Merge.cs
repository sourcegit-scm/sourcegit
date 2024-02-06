using System;

namespace SourceGit.Commands {
    public class Merge : Command {
        public Merge(string repo, string source, string mode, Action<string> outputHandler) {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            Args = $"merge --progress {source} {mode}";
        }

        protected override void OnReadline(string line) {
            _outputHandler?.Invoke(line);
        }

        private Action<string> _outputHandler = null;
    }
}
