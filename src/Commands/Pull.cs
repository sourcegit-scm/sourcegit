using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Pull : Command
    {
        public Pull(string repo, string remote, string branch, bool useRebase, int depth)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;
            Args = "pull --verbose --progress ";

            if (useRebase)
                Args += "--rebase=true ";

            if(depth > 0)
                Args += $"--depth {depth} --no-single-branch ";

            Args += $"{remote} {branch}";
        }

        public async Task<bool> RunAsync()
        {
            SSHKey = await new Config(WorkingDirectory).GetAsync($"remote.{_remote}.sshkey").ConfigureAwait(false);
            return await ExecAsync().ConfigureAwait(false);
        }

        private readonly string _remote;
    }
}
