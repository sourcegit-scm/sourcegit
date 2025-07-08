using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Fetch : Command
    {
        public Fetch(string repo, string remote, bool noTags, bool force)
        {
            _remoteKey = $"remote.{remote}.sshkey";

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder("fetch --progress --verbose ");
            builder.Append(noTags ? "--no-tags " : "--tags ");

            if (force)
                builder.Append("--force ");

            Args = builder.Append(remote).ToString();
        }

        public Fetch(string repo, Models.Branch local, Models.Branch remote)
        {
            _remoteKey = $"remote.{remote.Remote}.sshkey";

            WorkingDirectory = repo;
            Context = repo;
            Args = $"fetch --progress --verbose {remote.Remote} {remote.Name}:{local.Name}";
        }

        public async Task<bool> RunAsync()
        {
            SSHKey = await new Config(WorkingDirectory).GetAsync(_remoteKey).ConfigureAwait(false);
            return await ExecAsync().ConfigureAwait(false);
        }

        private readonly string _remoteKey;
    }
}
