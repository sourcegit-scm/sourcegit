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
            Args = "push --progress --verbose ";

            if (withTags)
                Args += "--tags ";
            if (checkSubmodules)
                Args += "--recurse-submodules=check ";
            if (track)
                Args += "-u ";
            if (force)
                Args += "--force-with-lease ";

            Args += $"{remote} {local}:{remoteBranch}";
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
