using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class Branch
    {
        public static async Task<bool> CreateAsync(string repo, string name, string basedOn, bool force, Models.ICommandLog log)
        {
            var builder = new StringBuilder();
            builder.Append("branch ");
            if (force)
                builder.Append("-f ");
            builder.Append(name);
            builder.Append(" ");
            builder.Append(basedOn);

            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = builder.ToString();
            cmd.Log = log;
            return await cmd.ExecAsync().ConfigureAwait(false);
        }

        public static async Task<bool> RenameAsync(string repo, string name, string to, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"branch -M {name} {to}";
            cmd.Log = log;
            return await cmd.ExecAsync().ConfigureAwait(false);
        }

        public static async Task<bool> SetUpstreamAsync(string repo, string name, string upstream, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Log = log;

            if (string.IsNullOrEmpty(upstream))
                cmd.Args = $"branch {name} --unset-upstream";
            else
                cmd.Args = $"branch {name} -u {upstream}";

            return await cmd.ExecAsync().ConfigureAwait(false);
        }

        public static async Task<bool> DeleteLocalAsync(string repo, string name, Models.ICommandLog log)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"branch -D {name}";
            cmd.Log = log;
            return await cmd.ExecAsync().ConfigureAwait(false);
        }

        public static async Task<bool> DeleteRemoteAsync(string repo, string remote, string name, Models.ICommandLog log)
        {
            bool exists = await new Remote(repo).HasBranchAsync(remote, name).ConfigureAwait(false);
            if (exists)
                return await new Push(repo, remote, $"refs/heads/{name}", true) { Log = log }.ExecAsync().ConfigureAwait(false);

            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.Args = $"branch -D -r {remote}/{name}";
            cmd.Log = log;
            return await cmd.ExecAsync().ConfigureAwait(false);
        }
    }
}
