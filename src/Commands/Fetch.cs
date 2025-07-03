namespace SourceGit.Commands
{
    public class Fetch : Command
    {
        public Fetch(string repo, string remote, bool noTags, bool force)
        {
            WorkingDirectory = repo;
            Context = repo;
            _remote = remote;
            Args = "fetch --progress --verbose ";

            if (noTags)
                Args += "--no-tags ";
            else
                Args += "--tags ";

            if (force)
                Args += "--force ";

            Args += remote;
        }

        public Fetch(string repo, Models.Branch local, Models.Branch remote)
        {
            WorkingDirectory = repo;
            Context = repo;
            _remote = remote.Remote;
            Args = $"fetch --progress --verbose {remote.Remote} {remote.Name}:{local.Name}";
        }

        public override bool Exec()
        {
            SSHKey = new Config(Context).Get($"remote.{_remote}.sshkey");
            return base.Exec();
        }

        private readonly string _remote;
    }
}
