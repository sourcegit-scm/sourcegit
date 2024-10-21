using System;

namespace SourceGit.Commands
{
    public class Fetch : Command
    {
        public Fetch(string repo, string remote, bool prune, bool noTags, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
            Args = "fetch --progress --verbose ";

            if (prune)
                Args += "--prune ";

            if (noTags)
                Args += "--no-tags ";
            else
                Args += "--force ";

            Args += remote;
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler;
    }
}
