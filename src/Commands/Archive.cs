using System;

namespace SourceGit.Commands
{
    public class Archive : Command
    {
        public Archive(string repo, string revision, string saveTo, Action<string> outputHandler)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"archive --format=zip --verbose --output=\"{saveTo}\" {revision}";
            TraitErrorAsOutput = true;
            _outputHandler = outputHandler;
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler;
    }
}