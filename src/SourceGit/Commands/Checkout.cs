using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class Checkout : Command
    {
        public Checkout(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Branch(string branch, Action<string> onProgress)
        {
            Args = $"checkout --progress {branch}";
            TraitErrorAsOutput = true;
            _outputHandler = onProgress;
            return Exec();
        }

        public bool Branch(string branch, string basedOn, Action<string> onProgress)
        {
            Args = $"checkout --progress -b {branch} {basedOn}";
            TraitErrorAsOutput = true;
            _outputHandler = onProgress;
            return Exec();
        }

        public bool File(string file, bool useTheirs)
        {
            if (useTheirs)
            {
                Args = $"checkout --theirs -- \"{file}\"";
            }
            else
            {
                Args = $"checkout --ours -- \"{file}\"";
            }

            return Exec();
        }

        public bool FileWithRevision(string file, string revision)
        {
            Args = $"checkout {revision} -- \"{file}\"";
            return Exec();
        }

        public bool Files(List<string> files)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("checkout -f -q --");
            foreach (var f in files)
            {
                builder.Append(" \"");
                builder.Append(f);
                builder.Append("\"");
            }
            Args = builder.ToString();
            return Exec();
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private Action<string> _outputHandler;
    }
}
