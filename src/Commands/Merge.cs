using System;

namespace SourceGit.Commands
{
    public class Merge : Command
    {
        public Merge(string repo, string source, string mode, string strategy, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            if (strategy != null)
                strategy = string.Concat("--strategy=", strategy);
            Args = $"merge --progress {strategy} {source} {mode}";
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler = null;
    }
}
