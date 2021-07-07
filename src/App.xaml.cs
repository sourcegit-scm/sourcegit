using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace SourceGit {

    /// <summary>
    ///     程序入口.
    /// </summary>
    public partial class App : Application {
        private static bool restart = false;

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
        ///     重启程序
        /// </summary>
        public static void Restart() {
            restart = true;
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            Current.Shutdown();
        }

        /// <summary>
        ///     启动.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAppStartup(object sender, StartupEventArgs e) {
            // 创建必要目录
            if (!Directory.Exists(Views.Controls.Avatar.CACHE_PATH)) {
                Directory.CreateDirectory(Views.Controls.Avatar.CACHE_PATH);
            }

            // 控制主题
            if (Models.Preference.Instance.General.UseDarkTheme) {
                foreach (var rs in Current.Resources.MergedDictionaries) {
                    if (rs.Source != null && rs.Source.OriginalString.StartsWith("pack://application:,,,/Resources/Themes/", StringComparison.Ordinal)) {
                        rs.Source = new Uri("pack://application:,,,/Resources/Themes/Dark.xaml", UriKind.Absolute);
                        break;
                    }
                }
            }

            // 控制显示语言
            var lang = Models.Preference.Instance.General.Locale;
            if (lang != "en_US") {
                foreach (var rs in Current.Resources.MergedDictionaries) {
                    if (rs.Source != null && rs.Source.OriginalString.StartsWith("pack://application:,,,/Resources/Locales/", StringComparison.Ordinal)) {
                        rs.Source = new Uri($"pack://application:,,,/Resources/Locales/{lang}.xaml", UriKind.Absolute);
                        break;
                    }
                }
            }

            // 主界面显示
            MainWindow = new Views.Launcher();

            // 如果启动命令中指定了路径，打开指定目录的仓库
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

            MainWindow.Show();

            // 检测更新
            if (Models.Preference.Instance.General.CheckForUpdate) {
                var curDayOfYear = DateTime.Now.DayOfYear;
                var lastDayOfYear = Models.Preference.Instance.General.LastCheckDay;
                if (lastDayOfYear != curDayOfYear) {
                    Models.Preference.Instance.General.LastCheckDay = curDayOfYear;
                    Task.Run(() => {
                        try {
                            var web = new WebClient() { Encoding = Encoding.UTF8 };
                            var raw = web.DownloadString("https://gitee.com/api/v5/repos/sourcegit/SourceGit/releases/latest");
                            var ver = Models.Version.Load(raw);
                            var cur = Assembly.GetExecutingAssembly().GetName().Version;

                            var matches = Regex.Match(ver.TagName, @"^v(\d+)\.(\d+).*");
                            if (!matches.Success) return;

                            var major = int.Parse(matches.Groups[1].Value);
                            var minor = int.Parse(matches.Groups[2].Value);
                            if (major > cur.Major || (major == cur.Major && minor > cur.Minor)) {
                                Dispatcher.Invoke(() => Views.Upgrade.Open(MainWindow, ver));
                            }
                        } catch {}
                    });
                }
            }
        }

        /// <summary>
        ///     后台运行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAppDeactivated(object sender, EventArgs e) {
            GC.Collect();
            if (!restart) Models.Preference.Save();
        }
    }
}
