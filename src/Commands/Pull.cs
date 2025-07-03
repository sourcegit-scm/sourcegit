namespace SourceGit.Commands
{
    public class Pull : Command
    {
        public Pull(string repo, string remote, string branch, bool useRebase)
        {
            WorkingDirectory = repo;
            Context = repo;
            _remote = remote;
            Args = "pull --verbose --progress ";

            if (useRebase)
                Args += "--rebase=true ";

            Args += $"{remote} {branch}";
        }

        public override bool Exec()
        {
            SSHKey = new Config(Context).Get($"remote.{_remote}.sshkey");
            return base.Exec();
        }

        private readonly string _remote;
    }
}
