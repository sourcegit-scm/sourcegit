using System;

namespace SourceGit.Commands
{
    public class Push : Command
    {
        public Push(string repo, string local, string remote, string remoteBranch, bool withTags, bool checkSubmodules, bool track, bool force, Action<string> onProgress)
        {
            _outputHandler = onProgress;

            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
            Args = ["push", "--progress", "--verbose"];

            if (withTags)
                Args.Add("--tags");
            if (checkSubmodules)
                Args.Add("--recurse-submodules=check");
            if (track)
                Args.Add("-u");
            if (force)
                Args.Add("--force-with-lease");

            Args.AddRange([remote, $"{local}:{remoteBranch}"]);
        }

        public Push(string repo, string remote, string tag, bool isDelete)
        {
            WorkingDirectory = repo;
            Context = repo;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
            Args = ["push"];

            if (isDelete)
                Args.Add("--delete");

            Args.AddRange([remote, $"refs/tags/{tag}"]);
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler = null;
    }
}
