using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class LFS
    {
        [GeneratedRegex(@"^(.+)\s+([\w.]+)\s+\w+:(\d+)$")]
        private static partial Regex REG_LOCK();

        private class SubCmd : Command
        {
            public SubCmd(string repo, string args, Models.ICommandLog log)
            {
                WorkingDirectory = repo;
                Context = repo;
                Args = args;
                Log = log;
            }
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

        public async Task<bool> InstallAsync(Models.ICommandLog log)
        {
            return await new SubCmd(_repo, "lfs install --local", log).ExecAsync();
        }

        public async Task<bool> TrackAsync(string pattern, bool isFilenameMode, Models.ICommandLog log)
        {
            var opt = isFilenameMode ? "--filename" : "";
            return await new SubCmd(_repo, $"lfs track {opt} \"{pattern}\"", log).ExecAsync();
        }

        public async Task FetchAsync(string remote, Models.ICommandLog log)
        {
            await new SubCmd(_repo, $"lfs fetch {remote}", log).ExecAsync();
        }

        public async Task PullAsync(string remote, Models.ICommandLog log)
        {
            await new SubCmd(_repo, $"lfs pull {remote}", log).ExecAsync();
        }

        public async Task PushAsync(string remote, Models.ICommandLog log)
        {
            await new SubCmd(_repo, $"lfs push {remote}", log).ExecAsync();
        }

        public async Task PruneAsync(Models.ICommandLog log)
        {
            await new SubCmd(_repo, "lfs prune", log).ExecAsync();
        }

        public async Task<List<Models.LFSLock>> LocksAsync(string remote)
        {
            var locks = new List<Models.LFSLock>();
            var cmd = new SubCmd(_repo, $"lfs locks --remote={remote}", null);
            var rs = await cmd.ReadToEndAsync();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var match = REG_LOCK().Match(line);
                    if (match.Success)
                    {
                        locks.Add(new Models.LFSLock()
                        {
                            File = match.Groups[1].Value,
                            User = match.Groups[2].Value,
                            ID = long.Parse(match.Groups[3].Value),
                        });
                    }
                }
            }

            return locks;
        }

        public async Task<bool> LockAsync(string remote, string file, Models.ICommandLog log)
        {
            return await new SubCmd(_repo, $"lfs lock --remote={remote} \"{file}\"", log).ExecAsync();
        }

        public async Task<bool> UnlockAsync(string remote, string file, bool force, Models.ICommandLog log)
        {
            var opt = force ? "-f" : "";
            return await new SubCmd(_repo, $"lfs unlock --remote={remote} {opt} \"{file}\"", log).ExecAsync();
        }

        public async Task<bool> UnlockAsync(string remote, long id, bool force, Models.ICommandLog log)
        {
            var opt = force ? "-f" : "";
            return await new SubCmd(_repo, $"lfs unlock --remote={remote} {opt} --id={id}", log).ExecAsync();
        }

        private readonly string _repo;
    }
}
