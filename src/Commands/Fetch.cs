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
            Args = ["fetch", "--progress", "--verbose"];

            if (prune)
                Args.Add("--prune");

            if (noTags)
                Args.Add("--no-tags");
            else
                Args.Add("--force");

            Args.Add(remote);

            Models.AutoFetchManager.Instance.MarkFetched(repo);
        }

        public Fetch(string repo, string remote, string localBranch, string remoteBranch, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
            Args = ["fetch", "--progress", "--verbose", remote, $"{remoteBranch}:{localBranch}"];
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler;
    }
}
