using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class Clone : Command
    {
        private readonly Action<string> _notifyProgress;

        public Clone(string ctx, string path, string url, string localName, string sshKey, IEnumerable<string> extraArgs, Action<string> ouputHandler)
        {
            Context = ctx;
            WorkingDirectory = path;
            TraitErrorAsOutput = true;
            SSHKey = sshKey;
            Args = ["clone", "--progress", "--verbose", "--recurse-submodules"];

            Args.AddRange(extraArgs);

            Args.Add(url);

            if (!string.IsNullOrEmpty(localName))
                Args.Add(localName);

            _notifyProgress = ouputHandler;
        }

        protected override void OnReadline(string line)
        {
            _notifyProgress?.Invoke(line);
        }
    }
}
