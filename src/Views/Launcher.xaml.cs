using System;
using System.Threading.Tasks;
using System.Windows;

namespace SourceGit.Views {

    /// <summary>
    ///     主窗体
    /// </summary>
    public partial class Launcher : Window {

        public Launcher() {
            Models.Watcher.Opened += OpenRepository;
            InitializeComponent();
            OnTabAdding(null, null);
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
            var tab = new Widgets.PageTabItem(repo.Name, false, repo.Bookmark, repo.Path);
            container.Add(repo.Path, page);
            Controls.PopupWidget.RegisterContainer(repo.Path, page);

            var front = container.Get(tabs.Current);
            if (front == null || front is Widgets.Dashboard) {
                tabs.Add(repo.Path, tab);
            } else {
                tabs.Replace(tabs.Current, repo.Path, tab);
            }
        }
        #endregion

        #region RIGHT_COMMANDS
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

        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }
        #endregion

        #region TAB_OPERATION
        private void OnTabAdding(object sender, RoutedEventArgs e) {
            var id = Guid.NewGuid().ToString();
            var tab = new Widgets.PageTabItem(App.Text("PageSwitcher.Welcome.Title"), true, 0, App.Text("PageSwitcher.Welcome.Tip"));
            var page = new Widgets.Welcome();

            container.Add(id, page);
            tabs.Add(id, tab);
            Controls.PopupWidget.RegisterContainer(id, page);
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
    }
}
