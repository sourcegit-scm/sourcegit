using System;
using System.IO;

namespace SourceGit.Commands
{
    public class LFS
    {
        class PruneCmd : Command
        {
            public PruneCmd(string repo, Action<string> onProgress)
            {
                WorkingDirectory = repo;
                Context = repo;
                Args = "lfs prune";
                TraitErrorAsOutput = true;
                _outputHandler = onProgress;
            }

            protected override void OnReadline(string line)
            {
                _outputHandler?.Invoke(line);
            }

            private readonly Action<string> _outputHandler;
        }

        public LFS(string repo)
        {
            _repo = repo;
        }

        public bool IsEnabled()
        {
            var path = Path.Combine(_repo, ".git", "hooks", "pre-push");
            if (!File.Exists(path))
                return false;

            var content = File.ReadAllText(path);
            return content.Contains("git lfs pre-push");
        }

        public void Prune(Action<string> outputHandler)
        {
            new PruneCmd(_repo, outputHandler).Exec();
        }

        private readonly string _repo;
    }
}
