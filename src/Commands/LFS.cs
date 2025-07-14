using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class LFS : Command
    {
        [GeneratedRegex(@"^(.+)\s+([\w.]+)\s+\w+:(\d+)$")]
        private static partial Regex REG_LOCK();

        public LFS(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task<bool> InstallAsync()
        {
            Args = "lfs install --local";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> TrackAsync(string pattern, bool isFilenameMode)
        {
            var builder = new StringBuilder();
            builder.Append("lfs track ");
            builder.Append(isFilenameMode ? "--filename " : string.Empty);
            builder.Append(pattern.Quoted());

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task FetchAsync(string remote)
        {
            Args = $"lfs fetch {remote}";
            await ExecAsync().ConfigureAwait(false);
        }

        public async Task PullAsync(string remote)
        {
            Args = $"lfs pull {remote}";
            await ExecAsync().ConfigureAwait(false);
        }

        public async Task PushAsync(string remote)
        {
            Args = $"lfs push {remote}";
            await ExecAsync().ConfigureAwait(false);
        }

        public async Task PruneAsync()
        {
            Args = "lfs prune";
            await ExecAsync().ConfigureAwait(false);
        }

        public async Task<List<Models.LFSLock>> GetLocksAsync(string remote)
        {
            Args = $"lfs locks --remote={remote}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            var locks = new List<Models.LFSLock>();

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

        public async Task<bool> LockAsync(string remote, string file)
        {
            Args = $"lfs lock --remote={remote} {file.Quoted()}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> UnlockAsync(string remote, string file, bool force)
        {
            var builder = new StringBuilder();
            builder
                .Append("lfs unlock --remote=")
                .Append(remote)
                .Append(force ? " -f " : " ")
                .Append(file.Quoted());

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }
    }
}
