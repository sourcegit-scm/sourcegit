using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class Fetch : Command
    {
        public Fetch(string repo, string remote, bool prune, bool noTags, Action<string> outputHandler)
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

            Args += "fetch --progress --verbose ";
            if (prune)
                Args += "--prune ";

            if (noTags)
                Args += "--no-tags ";
            else
                Args += "--force ";

            Args += remote;

            AutoFetch.MarkFetched(repo);
        }

        public Fetch(string repo, string remote, string localBranch, string remoteBranch, Action<string> outputHandler)
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

            Args += $"fetch --progress --verbose {remote} {remoteBranch}:{localBranch}";
        }

        protected override void OnReadline(string line)
        {
            _outputHandler?.Invoke(line);
        }

        private readonly Action<string> _outputHandler;
    }

    public class AutoFetch
    {
        public static bool IsEnabled
        {
            get;
            set;
        } = false;

        public static int Interval
        {
            get => _interval;
            set
            {
                if (value < 1)
                    return;
                _interval = value;
                lock (_lock)
                {
                    foreach (var job in _jobs)
                    {
                        job.Value.NextRunTimepoint = DateTime.Now.AddMinutes(Convert.ToDouble(_interval));
                    }
                }
            }
        }

        class Job
        {
            public Fetch Cmd = null;
            public DateTime NextRunTimepoint = DateTime.MinValue;
        }

        static AutoFetch()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    if (!IsEnabled)
                    {
                        Thread.Sleep(10000);
                        continue;
                    }

                    var now = DateTime.Now;
                    var uptodate = new List<Job>();
                    lock (_lock)
                    {
                        foreach (var job in _jobs)
                        {
                            if (job.Value.NextRunTimepoint.Subtract(now).TotalSeconds <= 0)
                            {
                                uptodate.Add(job.Value);
                            }
                        }
                    }

                    foreach (var job in uptodate)
                    {
                        job.Cmd.Exec();
                        job.NextRunTimepoint = DateTime.Now.AddMinutes(Convert.ToDouble(Interval));
                    }

                    Thread.Sleep(2000);
                }
            });
        }

        public static void AddRepository(string repo)
        {
            var job = new Job
            {
                Cmd = new Fetch(repo, "--all", true, false, null) { RaiseError = false },
                NextRunTimepoint = DateTime.Now.AddMinutes(Convert.ToDouble(Interval)),
            };

            lock (_lock)
            {
                if (_jobs.ContainsKey(repo))
                {
                    _jobs[repo] = job;
                }
                else
                {
                    _jobs.Add(repo, job);
                }
            }
        }

        public static void RemoveRepository(string repo)
        {
            lock (_lock)
            {
                _jobs.Remove(repo);
            }
        }

        public static void MarkFetched(string repo)
        {
            lock (_lock)
            {
                if (_jobs.TryGetValue(repo, out var value))
                {
                    value.NextRunTimepoint = DateTime.Now.AddMinutes(Convert.ToDouble(Interval));
                }
            }
        }

        private static readonly Dictionary<string, Job> _jobs = new Dictionary<string, Job>();
        private static readonly object _lock = new object();
        private static int _interval = 10;
    }
}
