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
            public SubCmd(string repo, string args, Action<string> onProgress)
            {
                WorkingDirectory = repo;
                Context = repo;
                Args = args;
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
            return new SubCmd(_repo, $"lfs install", null).Exec();
        }

        public bool Track(string pattern, bool isFilenameMode = false)
        {
            var opt = isFilenameMode ? "--filename" : "";
            return new SubCmd(_repo, $"lfs track {opt} \"{pattern}\"", null).Exec();
        }

        public void Fetch(string remote, Action<string> outputHandler)
        {
            new SubCmd(_repo, $"lfs fetch {remote}", outputHandler).Exec();
        }

        public void Pull(string remote, Action<string> outputHandler)
        {
            new SubCmd(_repo, $"lfs pull {remote}", outputHandler).Exec();
        }

        public void Push(string remote, Action<string> outputHandler)
        {
            new SubCmd(_repo, $"lfs push {remote}", outputHandler).Exec();
        }

        public void Prune(Action<string> outputHandler)
        {
            new SubCmd(_repo, "lfs prune", outputHandler).Exec();
        }

        public List<Models.LFSLock> Locks(string remote)
        {
            var locks = new List<Models.LFSLock>();
            var cmd = new SubCmd(_repo, $"lfs locks --remote={remote}", null);
            var rs = cmd.ReadToEnd();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(new char[] {'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);
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
            return new SubCmd(_repo, $"lfs lock --remote={remote} \"{file}\"", null).Exec();
        }

        public bool Unlock(string remote, string file, bool force)
        {
            var opt = force ? "-f" : "";
            return new SubCmd(_repo, $"lfs unlock --remote={remote} {opt} \"{file}\"", null).Exec();
        }

        public bool Unlock(string remote, long id, bool force)
        {
            var opt = force ? "-f" : "";
            return new SubCmd(_repo, $"lfs unlock --remote={remote} {opt} --id={id}", null).Exec();
        }

        private readonly string _repo;
    }
}
