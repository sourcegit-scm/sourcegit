using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class LFS : Command
    {
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
            Args = $"lfs locks --json --remote={remote}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (rs.IsSuccess)
            {
                try
                {
                    var locks = JsonSerializer.Deserialize(rs.StdOut, JsonCodeGen.Default.ListLFSLock);
                    return locks;
                }
                catch
                {
                    // Ignore exceptions.
                }
            }

            return [];
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

        public async Task<bool> UnlockMultipleAsync(string remote, List<string> files, bool force)
        {
            var builder = new StringBuilder();
            builder
                .Append("lfs unlock --remote=")
                .Append(remote)
                .Append(force ? " -f" : " ");

            foreach (string file in files)
                builder.Append(' ').Append(file.Quoted());

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }
    }
}
