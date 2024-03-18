using System;

namespace SourceGit.Commands
{
    public class Clone : Command
    {
        private readonly Action<string> _notifyProgress;

        public Clone(string ctx, string path, string url, string localName, string sshKey, string extraArgs, Action<string> ouputHandler)
        {
            Context = ctx;
            WorkingDirectory = path;
            TraitErrorAsOutput = true;

            if (string.IsNullOrEmpty(sshKey))
            {
                Args = "-c credential.helper=manager ";
            }
            else
            {
                Args = $"-c core.sshCommand=\"ssh -i '{sshKey}'\" ";
            }

            Args += "clone --progress --verbose --recurse-submodules ";

            if (!string.IsNullOrEmpty(extraArgs)) Args += $"{extraArgs} ";
            Args += $"{url} ";
            if (!string.IsNullOrEmpty(localName)) Args += localName;

            _notifyProgress = ouputHandler;
        }

        protected override void OnReadline(string line)
        {
            _notifyProgress?.Invoke(line);
        }
    }
}