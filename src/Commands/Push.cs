using System;

namespace SourceGit.Commands
{
    public class Push : Command
    {
        public Push(string repo, string local, string remote, string remoteBranch, bool withTags, bool force, bool track, Action<string> onProgress)
        {
            WorkingDirectory = repo;
            Context = repo;
            TraitErrorAsOutput = true;
            _outputHandler = onProgress;

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (!string.IsNullOrEmpty(sshKey))
            {
                Args = $"-c core.sshCommand=\"ssh -i '{sshKey}'\" ";
            }
            else
            {
                Args = "-c credential.helper=manager ";
            }

            Args += "push --progress --verbose ";

            if (withTags)
                Args += "--tags ";
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

            var sshKey = new Config(repo).Get($"remote.{remote}.sshkey");
            if (!string.IsNullOrEmpty(sshKey))
            {
                Args = $"-c core.sshCommand=\"ssh -i '{sshKey}'\" ";
            }
            else
            {
                Args = "-c credential.helper=manager ";
            }

            Args += "push ";
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
