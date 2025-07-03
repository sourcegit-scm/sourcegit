namespace SourceGit.Commands
{
    public class Push : Command
    {
        public Push(string repo, string local, string remote, string remoteBranch, bool withTags, bool checkSubmodules, bool track, bool force)
        {
            WorkingDirectory = repo;
            Context = repo;
            _remote = remote;
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
            WorkingDirectory = repo;
            Context = repo;
            _remote = remote;
            Args = "push ";

            if (isDelete)
                Args += "--delete ";

            Args += $"{remote} {refname}";
        }

        public override bool Exec()
        {
            SSHKey = new Config(Context).Get($"remote.{_remote}.sshkey");
            return base.Exec();
        }

        private readonly string _remote;
    }
}
