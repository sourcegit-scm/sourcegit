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

        public void Fetch(Action<string> outputHandler)
        {
            new SubCmd(_repo, $"lfs fetch", outputHandler).Exec();
        }

        public void Pull(Action<string> outputHandler)
        {
            new SubCmd(_repo, $"lfs pull", outputHandler).Exec();
        }

        public void Prune(Action<string> outputHandler)
        {
            new SubCmd(_repo, "lfs prune", outputHandler).Exec();
        }

        public List<Models.LFSLock> Locks()
        {
            var locks = new List<Models.LFSLock>();
            var cmd = new SubCmd(_repo, "lfs locks", null);
            var rs = cmd.ReadToEnd();
            if (rs.IsSuccess)
            {
                var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
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

        public bool Lock(string file)
        {
            return new SubCmd(_repo, $"lfs lock \"{file}\"", null).Exec();
        }

        public bool Unlock(string file, bool force)
        {
            var opt = force ? "-f" : "";
            return new SubCmd(_repo, $"lfs unlock {opt} \"{file}\"", null).Exec();
        }

        public bool Unlock(long id, bool force)
        {
            var opt = force ? "-f" : "";
            return new SubCmd(_repo, $"lfs unlock {opt} --id={id}", null).Exec();
        }

        private readonly string _repo;
    }
}
