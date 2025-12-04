using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
            var builder = new StringBuilder(1024);
            builder.Append("worktree add ");
            if (!string.IsNullOrEmpty(tracking))
                builder.Append("--track ");
            if (!string.IsNullOrEmpty(name))
                builder.Append(createNew ? "-b " : "-B ").Append(name).Append(' ');
            builder.Append(fullpath.Quoted()).Append(' ');

            if (!string.IsNullOrEmpty(tracking))
                builder.Append(tracking);
            else if (!string.IsNullOrEmpty(name) && !createNew)
                builder.Append(name);

            Args = builder.ToString();
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
