using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Pull : Command
    {
        public Pull(string repo, string remote, string branch, bool useRebase)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(512);
            builder.Append("pull --verbose --progress ");
            if (useRebase)
                builder.Append("--rebase=true ");
            builder.Append(remote).Append(' ').Append(branch);

            Args = builder.ToString();
        }

        public async Task<bool> RunAsync()
        {
            SSHKey = await new Config(WorkingDirectory).GetAsync($"remote.{_remote}.sshkey").ConfigureAwait(false);
            return await ExecAsync().ConfigureAwait(false);
        }

        private readonly string _remote;
    }
}
