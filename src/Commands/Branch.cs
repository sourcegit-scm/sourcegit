using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Branch : Command
    {
        public Branch(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task<bool> CreateAsync(string name, string basedOn, bool force)
        {
            var builder = new StringBuilder();
            builder.Append("branch ");
            if (force)
                builder.Append("-f ");
            builder.Append(name);
            builder.Append(" ");
            builder.Append(basedOn);

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> RenameAsync(string name, string to)
        {
            Args = $"branch -M {name} {to}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> SetUpstreamAsync(string name, string upstream)
        {
            if (string.IsNullOrEmpty(upstream))
                Args = $"branch {name} --unset-upstream";
            else
                Args = $"branch {name} -u {upstream}";

            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteLocalAsync(string name)
        {
            Args = $"branch -D {name}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteRemoteAsync(string remote, string name)
        {
            Args = $"branch -D -r {remote}/{name}";
            return await ExecAsync().ConfigureAwait(false);
        }
    }
}
