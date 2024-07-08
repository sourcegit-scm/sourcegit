using System;

namespace SourceGit.Commands
{
    public class Pull : Command
    {
        public Pull(string repo, string remote, string branch, bool useRebase, bool noTags, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (string.IsNullOrEmpty(sshKey))
                Args = "-c credential.helper=manager ";
            else
                UseSSHKey(sshKey);

            Args += "pull --verbose --progress --tags ";
            if (useRebase)
                Args += "--rebase ";
            if (noTags)
                Args += "--no-tags ";

            Args += $"{remote} {branch}";
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler;
    }
}
