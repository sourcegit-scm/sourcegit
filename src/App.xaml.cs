using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace SourceGit {

    /// <summary>
    ///     程序入口.
    /// </summary>
    public partial class App : Application {

        /// <summary>
        ///     读取本地化字串
        /// </summary>
        /// <param name="key">本地化字串的Key</param>
        /// <param name="args">可选格式化参数</param>
        /// <returns>本地化字串</returns>
        public static string Text(string key, params object[] args) {
            var data = Current.FindResource($"Text.{key}") as string;
            if (string.IsNullOrEmpty(data)) return $"Text.{key}";
            return string.Format(data, args);
        }

        /// <summary>
        ///     启动.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            // 崩溃上报
            AppDomain.CurrentDomain.UnhandledException += (_, ev) => Models.Issue.Create(ev.ExceptionObject as Exception);

            // 创建必要目录
            if (!Directory.Exists(Views.Controls.Avatar.CACHE_PATH)) {
                Directory.CreateDirectory(Views.Controls.Avatar.CACHE_PATH);
            }

            Models.Theme.Change();
            Models.Locale.Change();

            // 如果启动命令中指定了路径，打开指定目录的仓库
            var launcher = new Views.Launcher();
            if (e.Args.Length > 0) {
                var repo = Models.Preference.Instance.FindRepository(e.Args[0]);
                if (repo == null) {
                    var path = new Commands.GetRepositoryRootPath(e.Args[0]).Result();
                    if (path != null) {
                        var gitDir = new Commands.QueryGitDir(path).Result();
                        repo = Models.Preference.Instance.AddRepository(path, gitDir, "");
                    }
                }

                if (repo != null) Models.Watcher.Open(repo);
            } else {
                var restore = Models.Preference.Instance.Restore;
                var actived = null as Models.Repository;
                if (restore.IsEnabled && restore.Opened.Count > 0) {
                    foreach (var path in restore.Opened) {
                        if (!Directory.Exists(path)) continue;
                        var repo = Models.Preference.Instance.FindRepository(path);
                        if (repo != null) Models.Watcher.Open(repo);
                        if (path == restore.Actived) actived = repo;
                    }

                    if (actived != null) Models.Watcher.Open(actived);
                }
            }

            // 主界面显示
            MainWindow = launcher;
            MainWindow.Show();

            // 检测版本更新
            Models.Version.Check(ver => Dispatcher.Invoke(() => {
                var dialog = new Views.Upgrade(ver) { Owner = MainWindow };
                dialog.ShowDialog();
            }));
        }

        /// <summary>
        ///     后台运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected override void OnDeactivated(EventArgs e) {
            base.OnDeactivated(e);
            GC.Collect();
            Models.Preference.Save();
        }
    }
}
