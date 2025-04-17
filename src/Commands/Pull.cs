namespace SourceGit.Commands
{
    public class Pull : Command
    {
        public Pull(string repo, string remote, string branch, bool useRebase, bool noTags)
        {
            WorkingDirectory = repo;
            Context = repo;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
            Args = "pull --verbose --progress ";

            if (useRebase)
                Args += "--rebase=true ";

            if (noTags)
                Args += "--no-tags ";

            Args += $"{remote} {branch}";
        }
    }
}
