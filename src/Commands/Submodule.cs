using System;

namespace SourceGit.Commands
{
    public class Submodule : Command
    {
        public Submodule(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public bool Add(string url, string relativePath, bool recursive, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            Args = ["submodule", "add", url, relativePath];
            if (!Exec())
                return false;

            if (recursive)
            {
                Args = ["submodule", "update", "--init", "--recursive", "--", relativePath];
                return Exec();
            }
            else
            {
                Args = ["submodule", "update", "--init", "--", relativePath];
                return true;
            }
        }

        public bool Update(string module, bool init, bool recursive, bool useRemote, Action<string> outputHandler)
        {
            Args = ["submodule", "update"];

            if (init)
                Args.Add("--init");
            if (recursive)
                Args.Add("--recursive");
            if (useRemote)
                Args.Add("--remote");
            if (!string.IsNullOrEmpty(module))
                Args.AddRange(["--", module]);

            _outputHandler = outputHandler;
            return Exec();
        }

        public bool Delete(string relativePath)
        {
            Args = ["submodule", "deinit", "-f", relativePath];
            if (!Exec())
                return false;

            Args = ["rm", "-rf", relativePath];
            return Exec();
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private Action<string> _outputHandler;
    }
}
