using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SourceGit.Views {

    /// <summary>
    ///     主窗体
    /// </summary>
    public partial class Launcher : Controls.Window {

        public Launcher() {
            Models.Watcher.Opened += OpenRepository;
            InitializeComponent();
            tabs.Add();
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            var restore = Models.Preference.Instance.Restore;
            if (!restore.IsEnabled) return;

            restore.Opened.Clear();
            restore.Actived = null;

            foreach (var tab in tabs.Tabs) {
                if (tab.IsWelcomePage) continue;

                // 仅支持恢复加入管理的仓库页，Submodules等未加入管理的不支持
                var repo = Models.Preference.Instance.FindRepository(tab.Id);
                if (repo != null) restore.Opened.Add(tab.Id);
            }

            if (restore.Opened.Count > 0) {
                if (restore.Opened.IndexOf(tabs.Current) >= 0) {
                    restore.Actived = tabs.Current;
                } else {
                    restore.Actived = restore.Opened[0];
                }
            }

            Models.Preference.Save();
        }

        #region OPEN_REPO
        private void OpenRepository(Models.Repository repo) {
            if (tabs.Goto(repo.Path)) return;

            Task.Run(() => {
                var cmd = new Commands.Config(repo.Path);
                repo.GitFlow.Feature = cmd.Get("gitflow.prefix.feature");
                repo.GitFlow.Release = cmd.Get("gitflow.prefix.release");
                repo.GitFlow.Hotfix = cmd.Get("gitflow.prefix.hotfix");
            });

            Commands.AutoFetch.Start(repo.Path);

            var page = new Widgets.Dashboard(repo);
            container.Add(repo.Path, page);
            Controls.PopupWidget.RegisterContainer(repo.Path, page);

            var front = container.Get(tabs.Current);
            if (front == null || front is Widgets.Dashboard) {
                tabs.Add(repo.Name, repo.Path, repo.Bookmark);
            } else {
                tabs.Replace(tabs.Current, repo.Name, repo.Path, repo.Bookmark);
            }
        }
        #endregion

        #region RIGHT_COMMANDS
        private void ChangeTheme(object sender, RoutedEventArgs e) {
            Models.Theme.Change();
        }

        private void OpenPreference(object sender, RoutedEventArgs e) {
            var dialog = new Preference() { Owner = this };
            dialog.ShowDialog();
        }

        private void OpenAbout(object sender, RoutedEventArgs e) {
            var dialog = new About() { Owner = this };
            dialog.ShowDialog();
        }

        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }
        #endregion

        #region TAB_OPERATION
        private void OnTabAdding(object sender, Widgets.PageTabBar.TabEventArgs e) {
            var page = new Widgets.Welcome();
            container.Add(e.TabId, page);
            Controls.PopupWidget.RegisterContainer(e.TabId, page);
        }

        private void OnTabSelected(object sender, Widgets.PageTabBar.TabEventArgs e) {
            container.Goto(e.TabId);
            Controls.PopupWidget.SetCurrentContainer(e.TabId);
        }

        private void OnTabClosed(object sender, Widgets.PageTabBar.TabEventArgs e) {
            Controls.PopupWidget.UnregisterContainer(e.TabId);
            Models.Watcher.Close(e.TabId);
            Commands.AutoFetch.Stop(e.TabId);
            container.Remove(e.TabId);
            GC.Collect();
        }
        #endregion

        #region HOTKEYS
        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
                if (Keyboard.IsKeyDown(Key.Tab)) {
                    tabs.Next();
                    e.Handled = true;
                    return;
                }

                if (Keyboard.IsKeyDown(Key.W)) {
                    tabs.CloseCurrent();
                    e.Handled = true;
                    return;
                }

                if (Keyboard.IsKeyDown(Key.T)) {
                    tabs.Add();
                    e.Handled = true;
                    return;
                }

                if (Keyboard.IsKeyDown(Key.F)) {
                    var dashboard = container.Get(tabs.Current) as Widgets.Dashboard;
                    if (dashboard != null) {
                        dashboard.OpenSearch(null, null);
                        e.Handled = true;
                        return;
                    }
                }

                for (int i = 0; i < 9; i++) {
                    if (Keyboard.IsKeyDown(Key.D1 + i) || Keyboard.IsKeyDown(Key.NumPad1 + i)) {
                        if (tabs.Tabs.Count > i) {
                            tabs.Goto(tabs.Tabs[i].Id);
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }

            if (Keyboard.IsKeyDown(Key.F5)) {
                var dashboard = container.Get(tabs.Current) as Widgets.Dashboard;
                if (dashboard != null) dashboard.Refresh();
                e.Handled = true;
                return;
            }
        }
        #endregion
    }
}
