using System;

namespace SourceGit.Commands
{
    public class Pull : Command
    {
        public Pull(string repo, string remote, string branch, bool useRebase, bool noTags, bool prune, Action<string> outputHandler)
        {
            _outputHandler = outputHandler;
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            SSHKey = new Config(repo).Get($"remote.{remote}.sshkey");
            Args = "pull --verbose --progress --tags ";

            if (useRebase)
                Args += "--rebase ";
            if (noTags)
                Args += "--no-tags ";
            if (prune)
                Args += "--prune ";

            Args += $"{remote} {branch}";
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler;
    }
}
