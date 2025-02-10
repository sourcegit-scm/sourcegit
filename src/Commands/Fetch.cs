using System;

namespace SourceGit.Commands
{
    public class Fetch : Command
    {
        public Fetch(string repo, string remote, bool noTags, bool force, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
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

        public Fetch(string repo, Models.Branch local, Models.Branch remote, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            SSHKey = new Config(repo).Get($"remote.{remote.Remote}.sshkey");
            Args = $"fetch --progress --verbose {remote.Remote} {remote.Name}:{local.Name}";
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler;
    }
}
