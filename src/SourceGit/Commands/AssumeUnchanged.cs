using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class AssumeUnchanged
    {
        partial class ViewCommand : Command
        {

            [GeneratedRegex(@"^(\w)\s+(.+)$")]
            private static partial Regex REG();

            public ViewCommand(string repo)
            {
                WorkingDirectory = repo;
                Args = "ls-files -v";
                RaiseError = false;
            }

            public List<string> Result()
            {
                Exec();
                return _outs;
            }

            protected override void OnReadline(string line)
            {
                var match = REG().Match(line);
                if (!match.Success) return;

                if (match.Groups[1].Value == "h")
                {
                    _outs.Add(match.Groups[2].Value);
                }
            }

            private readonly List<string> _outs = new List<string>();
        }

        class ModCommand : Command
        {
            public ModCommand(string repo, string file, bool bAdd)
            {
                var mode = bAdd ? "--assume-unchanged" : "--no-assume-unchanged";

                WorkingDirectory = repo;
                Context = repo;
                Args = $"update-index {mode} -- \"{file}\"";
            }
        }

        public AssumeUnchanged(string repo)
        {
            _repo = repo;
        }

        public List<string> View()
        {
            return new ViewCommand(_repo).Result();
        }

        public void Add(string file)
        {
            new ModCommand(_repo, file, true).Exec();
        }

        public void Remove(string file)
        {
            new ModCommand(_repo, file, false).Exec();
        }

        private readonly string _repo;
    }
}