namespace SourceGit.Commands
{
    public class Pull : Command
    {
        public Pull(string repo, string remote, string branch, bool useRebase)
        {
            WorkingDirectory = repo;
            Context = repo;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
            Args = "pull --verbose --progress ";

            if (useRebase)
                Args += "--rebase=true ";

            Args += $"{remote} {branch}";
        }
    }
}
