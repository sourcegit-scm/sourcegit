using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Branch : Command
    {
        public Branch(string repo, string name)
        {
            WorkingDirectory = repo;
            Context = repo;
            _name = name;
        }

        public async Task<bool> CreateAsync(string basedOn, bool force)
        {
            var builder = new StringBuilder();
            builder.Append("branch ");
            if (force)
                builder.Append("-f ");
            builder.Append(_name);
            builder.Append(" ");
            builder.Append(basedOn);

            Args = builder.ToString();
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> RenameAsync(string to)
        {
            Args = $"branch -M {_name} {to}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> SetUpstreamAsync(Models.Branch tracking)
        {
            if (tracking == null)
                Args = $"branch {_name} --unset-upstream";
            else
                Args = $"branch {_name} -u {tracking.FriendlyName}";

            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteLocalAsync()
        {
            Args = $"branch -D {_name}";
            return await ExecAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteRemoteAsync(string remote)
        {
            Args = $"branch -D -r {remote}/{_name}";
            return await ExecAsync().ConfigureAwait(false);
        }

        private readonly string _name;
    }
}
