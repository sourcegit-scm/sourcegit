using System;
using System.Collections.Generic;
using System.Threading;

namespace SourceGit.Commands {

    /// <summary>
    ///     拉取
    /// </summary>
    public class Fetch : Command {
        private Action<string> handler = null;

        public Fetch(string repo, string remote, bool prune, Action<string> outputHandler) {
            Cwd = repo;
            TraitErrorAsOutput = true;
            Args = "-c credential.helper=manager fetch --progress --verbose ";
            if (prune) Args += "--prune ";
            Args += remote;
            handler = outputHandler;
            AutoFetch.MarkFetched(repo);
        }

        public override void OnReadline(string line) {
            handler?.Invoke(line);
        }
    }

    /// <summary>
    ///     自动拉取（每隔10分钟）
    /// </summary>
    public class AutoFetch {
        private static Dictionary<string, AutoFetch> jobs = new Dictionary<string, AutoFetch>();

        private Fetch cmd = null;
        private long nextFetchPoint = 0;
        private Timer timer = null;

        public static void Start(string repo) {
            if (!Models.Preference.Instance.General.AutoFetchRemotes) return;

            // 只自动更新加入管理列表中的仓库（子模块等不自动更新）
            var exists = Models.Preference.Instance.FindRepository(repo);
            if (exists == null) return;

            var job = new AutoFetch(repo);
            jobs.Add(repo, job);
        }

        public static void MarkFetched(string repo) {
            if (!jobs.ContainsKey(repo)) return;
            jobs[repo].nextFetchPoint = DateTime.Now.AddMinutes(10).ToFileTime();
        }

        public static void Stop(string repo) {
            if (!jobs.ContainsKey(repo)) return;

            jobs[repo].timer.Dispose();
            jobs.Remove(repo);
        }

        public AutoFetch(string repo) {
            cmd = new Fetch(repo, "--all", true, null);
            nextFetchPoint = DateTime.Now.AddMinutes(10).ToFileTime();
            timer = new Timer(OnTick, null, 60000, 10000);
        }

        private void OnTick(object o) {
            var now = DateTime.Now.ToFileTime();
            if (nextFetchPoint > now) return;
            
            Models.Watcher.SetEnabled(cmd.Cwd, false);
            cmd.Exec();
            nextFetchPoint = DateTime.Now.AddMinutes(10).ToFileTime();
            Models.Watcher.SetEnabled(cmd.Cwd, true);
        }
    }
}
