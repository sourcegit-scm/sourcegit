using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class LFS
    {
        [GeneratedRegex(@"^(.+)\s+([\w.]+)\s+\w+:(\d+)$")]
        private static partial Regex REG_LOCK();

        class SubCmd : Command
        {
            public SubCmd(string repo, string args, Models.ICommandLog log)
            {
                WorkingDirectory = repo;
                Context = repo;
                Args = args;
                Log = log;
            }
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
            return new SubCmd(_repo, "lfs install --local", null).Exec();
        }

        public bool Track(string pattern, bool isFilenameMode, Models.ICommandLog log)
        {
            var opt = isFilenameMode ? "--filename" : "";
            return new SubCmd(_repo, $"lfs track {opt} \"{pattern}\"", log).Exec();
        }

        public void Fetch(string remote, Models.ICommandLog log)
        {
            new SubCmd(_repo, $"lfs fetch {remote}", log).Exec();
        }

        public void Pull(string remote, Models.ICommandLog log)
        {
            new SubCmd(_repo, $"lfs pull {remote}", log).Exec();
        }

        public void Push(string remote, Models.ICommandLog log)
        {
            new SubCmd(_repo, $"lfs push {remote}", log).Exec();
        }

        public void Prune(Models.ICommandLog log)
        {
            new SubCmd(_repo, "lfs prune", log).Exec();
        }

        public List<Models.LFSLock> Locks(string remote)
        {
            var locks = new List<Models.LFSLock>();
            var cmd = new SubCmd(_repo, $"lfs locks --remote={remote}", null);
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
