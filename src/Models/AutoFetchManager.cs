using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    public class AutoFetchManager
    {
        public static AutoFetchManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new AutoFetchManager();

                return _instance;
            }
        }

        public class Job
        {
            public Commands.Fetch Cmd = null;
            public DateTime NextRunTimepoint = DateTime.MinValue;
        }

        public bool IsEnabled
        {
            get;
            set;
        } = false;

        public int Interval
        {
            get => _interval;
            set
            {
                _interval = Math.Max(1, value);

                lock (_lock)
                {
                    foreach (var job in _jobs)
                        job.Value.NextRunTimepoint = DateTime.Now.AddMinutes(_interval * 1.0);
                }
            }
        }

        private static AutoFetchManager _instance = null;
        private Dictionary<string, Job> _jobs = new Dictionary<string, Job>();
        private object _lock = new object();
        private int _interval = 10;

        public void Start()
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
                                uptodate.Add(job.Value);
                        }
                    }

                    foreach (var job in uptodate)
                    {
                        job.Cmd.Exec();
                        job.NextRunTimepoint = DateTime.Now.AddMinutes(Convert.ToDouble(Interval));
                    }

                    Thread.Sleep(2000);
                }

                // ReSharper disable once FunctionNeverReturns
            });
        }

        public void AddRepository(string repo)
        {
            var job = new Job
            {
                Cmd = new Commands.Fetch(repo, "--all", true, false, null) { RaiseError = false },
                NextRunTimepoint = DateTime.Now.AddMinutes(Convert.ToDouble(Interval)),
            };

            lock (_lock)
            {
                _jobs[repo] = job;
            }
        }

        public void RemoveRepository(string repo)
        {
            lock (_lock)
            {
                _jobs.Remove(repo);
            }
        }

        public void MarkFetched(string repo)
        {
            lock (_lock)
            {
                if (_jobs.TryGetValue(repo, out var value))
                    value.NextRunTimepoint = DateTime.Now.AddMinutes(Interval * 1.0);
            }
        }
    }
}
