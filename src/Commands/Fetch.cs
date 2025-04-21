namespace SourceGit.Commands
{
    public class Fetch : Command
    {
        public Fetch(string repo, string remote, bool noTags, bool force)
        {
            WorkingDirectory = repo;
            Context = repo;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
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
            SSHKey = new Config(repo).Get($"remote.{remote.Remote}.sshkey");
            Args = $"fetch --progress --verbose {remote.Remote} {remote.Name}:{local.Name}";
        }
    }
}
