using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Worktree : Command
    {
        public Worktree(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task<List<Models.Worktree>> ReadAllAsync()
        {
            Args = "worktree list --porcelain";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            var worktrees = new List<Models.Worktree>();
            Models.Worktree last = null;
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("worktree ", StringComparison.Ordinal))
                    {
                        last = new Models.Worktree() { FullPath = line.Substring(9).Trim() };
                        last.RelativePath = Path.GetRelativePath(WorkingDirectory, last.FullPath);
                        worktrees.Add(last);
                        continue;
                    }

                    if (last == null)
                        continue;

                    if (line.StartsWith("bare", StringComparison.Ordinal))
                    {
                        worktrees.Remove(last);
                        last = null;
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
                }
            }

            return worktrees;
        }

        public async Task<bool> AddAsync(string fullpath, string name, bool createNew, string tracking)
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

            Args += $"{fullpath.Quoted()} ";

            if (!string.IsNullOrEmpty(tracking))
                Args += tracking;
            else if (!string.IsNullOrEmpty(name) && !createNew)
                Args += name;

            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> PruneAsync()
        {
            Args = "worktree prune -v";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> LockAsync(string fullpath)
        {
            Args = $"worktree lock {fullpath.Quoted()}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> UnlockAsync(string fullpath)
        {
            Args = $"worktree unlock {fullpath.Quoted()}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> RemoveAsync(string fullpath, bool force)
        {
            if (force)
                Args = $"worktree remove -f {fullpath.Quoted()}";
            else
                Args = $"worktree remove {fullpath.Quoted()}";

            return await ExecAsync().ConfigureAwait(false);
        }
    }
}
