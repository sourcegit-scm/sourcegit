using System;

namespace SourceGit.Commands
{
    public class Fetch : Command
    {
        public Fetch(string repo, string remote, string branch, bool noTags, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");

            Args = string.IsNullOrEmpty(branch)
                ? "fetch --progress --verbose "
                : $"fetch {remote} {branch}:{branch} --progress --verbose ";

            if (noTags)
                Args += "--no-tags ";
            else
                Args += "--force ";

            if (string.IsNullOrEmpty(branch))
                Args += remote;
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler;
    }
}
