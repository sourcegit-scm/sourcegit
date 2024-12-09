using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Merge : Command
    {
        public Merge(string repo, string source, string mode, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            Args = $"merge --progress {source} {mode}";
        }

        public Merge(string repo, List<string> targets, bool autoCommit, string strategy, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;

            var builder = new StringBuilder();
            builder.Append("merge --progress ");
            if (!string.IsNullOrEmpty(strategy))
                builder.Append($"--strategy={strategy} ");
            if (!autoCommit)
                builder.Append("--no-commit ");

            foreach (var t in targets)
            {
                builder.Append(t);
                builder.Append(' ');
            }

            Args = builder.ToString();
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler = null;
    }
}
