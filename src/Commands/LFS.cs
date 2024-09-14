using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class LFS
    {
        [GeneratedRegex(@"^(.+)\s+(\w+)\s+\w+:(\d+)$")]
        private static partial Regex REG_LOCK();

        class SubCmd : Command
        {
            public SubCmd(string repo, IEnumerable<string> args, Action<string> onProgress)
            {
                WorkingDirectory = repo;
                Context = repo;
                Args = new List<string>(args);
                TraitErrorAsOutput = true;
                _outputHandler = onProgress;
            }

            protected override void OnReadline(string line)
            {
                _outputHandler?.Invoke(line);
            }

            private readonly Action<string> _outputHandler;
        }

        public LFS(string repo)
        {
            _repo = repo;
        }

        public bool IsEnabled()
        {
            var path = Path.Combine(_repo, ".git", "hooks", "pre-push");
            if (!File.Exists(path))
                return false;

            var content = File.ReadAllText(path);
            return content.Contains("git lfs pre-push");
        }

        public bool Install()
        {
            return new SubCmd(_repo, ["lfs", "install", "--local"], null).Exec();
        }

        public bool Track(string pattern, bool isFilenameMode = false)
        {
            var opt = isFilenameMode ? "--filename" : "";
            List<string> args = ["lfs", "track"];
            if (isFilenameMode)
                args.Add("--filename");
            args.Add(pattern);
            return new SubCmd(_repo, args, null).Exec();
        }

        public void Fetch(string remote, Action<string> outputHandler)
        {
            new SubCmd(_repo, ["lfs", "fetch", remote], outputHandler).Exec();
        }

        public void Pull(string remote, Action<string> outputHandler)
        {
            new SubCmd(_repo, ["lfs", "pull", remote], outputHandler).Exec();
        }

        public void Push(string remote, Action<string> outputHandler)
        {
            new SubCmd(_repo, ["lfs", "push", remote], outputHandler).Exec();
        }

        public void Prune(Action<string> outputHandler)
        {
            new SubCmd(_repo, ["lfs", "prune"], outputHandler).Exec();
        }

        public List<Models.LFSLock> Locks(string remote)
        {
            var locks = new List<Models.LFSLock>();
            var cmd = new SubCmd(_repo, ["lfs", "locks", $"--remote={remote}"], null);
            var rs = cmd.ReadToEnd();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var match = REG_LOCK().Match(line);
                    if (match.Success)
                    {
                        locks.Add(new Models.LFSLock()
                        {
                            File = match.Groups[1].Value,
                            User = match.Groups[2].Value,
                            ID = long.Parse(match.Groups[3].Value),
                        });
                    }
                }
            }

            return locks;
        }

        public bool Lock(string remote, string file)
        {
            return new SubCmd(_repo, ["lfs", "lock", $"--remote={remote}", file], null).Exec();
        }

        public bool Unlock(string remote, string file, bool force)
        {
            List<string> args = ["lfs", "unlock", $"--remote={remote}"];
            if (force)
                args.Add("-f");
            args.Add(file);
            return new SubCmd(_repo, args, null).Exec();
        }

        public bool Unlock(string remote, long id, bool force)
        {
            List<string> args = ["lfs", "unlock", $"--remote={remote}"];
            if (force)
                args.Add("-f");
            args.Add($"--id={id}");
            return new SubCmd(_repo, args, null).Exec();
        }

        private readonly string _repo;
    }
}
