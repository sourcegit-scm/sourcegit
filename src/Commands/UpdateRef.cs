using System;

namespace SourceGit.Commands
{
    public class UpdateRef : Command
    {
        public UpdateRef(string repo, string refName, string toRevision, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;

            WorkingDirectory = repo;
            Context = repo;
            Args = $"update-ref {refName} {toRevision}";
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private Action<string> _outputHandler;
    }
}
