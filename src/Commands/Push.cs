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

        public Push(string repo, string remote, string tag, bool isDelete)
        {
            WorkingDirectory = repo;
            Context = repo;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
            Args = "push ";

            if (isDelete)
                Args += "--delete ";

            Args += $"{remote} refs/tags/{tag}";
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler = null;
    }
}
