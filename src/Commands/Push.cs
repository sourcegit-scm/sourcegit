using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Push : Command
    {
        public Push(string repo, string local, string remote, string remoteBranch, bool withTags, bool checkSubmodules, bool track, bool force)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder("push --progress --verbose ");
            if (withTags)
                builder.Append("--tags ");
            if (checkSubmodules)
                builder.Append("--recurse-submodules=check ");
            if (track)
                builder.Append("-u ");
            if (force)
                builder.Append("--force-with-lease ");
            Args = builder.Append(remote).Append(' ').Append(local).Append(':').Append(remoteBranch).ToString();
        }

        public Push(string repo, string remote, string refname, bool isDelete)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;
            Args = "push ";

            if (isDelete)
                Args += "--delete ";

            Args += $"{remote} {refname}";
        }

        public async Task<bool> RunAsync()
        {
            SSHKey = await new Config(WorkingDirectory).GetAsync($"remote.{_remote}.sshkey").ConfigureAwait(false);
            return await ExecAsync().ConfigureAwait(false);
        }

        private readonly string _remote;
    }
}
