using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class Worktree : Command
    {
        [GeneratedRegex(@"^(\w)\s(\d+)$")]
        private static partial Regex REG_AHEAD_BEHIND();

        public Worktree(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public List<Models.Worktree> List()
        {
            Args = "worktree list --porcelain";

            var rs = ReadToEnd();
            var worktrees = new List<Models.Worktree>();
            var last = null as Models.Worktree;
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("worktree ", StringComparison.Ordinal))
                    {
                        last = new Models.Worktree() { FullPath = line.Substring(9).Trim() };
                        worktrees.Add(last);
                    }
                    else if (line.StartsWith("bare", StringComparison.Ordinal))
                    {
                        last.IsBare = true;
                    }
                    else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
                    {
                        last.Head = line.Substring(5).Trim();
                    }
                    else if (line.StartsWith("branch ", StringComparison.Ordinal))
                    {
                        last.Branch = line.Substring(7).Trim();
                    }
                    else if (line.StartsWith("detached", StringComparison.Ordinal))
                    {
                        last.IsDetached = true;
                    }
                    else if (line.StartsWith("locked", StringComparison.Ordinal))
                    {
                        last.IsLocked = true;
                    }
                    else if (line.StartsWith("prunable", StringComparison.Ordinal))
                    {
                        last.IsPrunable = true;
                    }
                }
            }

            return worktrees;
        }

        public bool Add(string fullpath, string name, bool createNew, string tracking, Action<string> outputHandler)
        {
            Args = "worktree add ";

            if (!string.IsNullOrEmpty(tracking))
                Args += "--track ";

            if (!string.IsNullOrEmpty(name))
            {
                if (createNew)
                    Args += $"-b {name} ";
                else
                    Args += $"-B {name} ";
            }

            Args += $"\"{fullpath}\" ";

            if (!string.IsNullOrEmpty(tracking))
                Args += tracking;

            _outputHandler = outputHandler;
            return Exec();
        }

        public bool Prune(Action<string> outputHandler)
        {
            Args = "worktree prune -v";
            _outputHandler = outputHandler;
            return Exec();
        }

        public bool Lock(string fullpath)
        {
            Args = $"worktree lock \"{fullpath}\"";
            return Exec();
        }

        public bool Unlock(string fullpath)
        {
            Args = $"worktree unlock \"{fullpath}\"";
            return Exec();
        }

        public bool Remove(string fullpath, bool force, Action<string> outputHandler)
        {
            if (force)
                Args = $"worktree remove -f \"{fullpath}\"";
            else
                Args = $"worktree remove \"{fullpath}\"";

            _outputHandler = outputHandler;
            return Exec();
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private Action<string> _outputHandler = null;
    }
}
