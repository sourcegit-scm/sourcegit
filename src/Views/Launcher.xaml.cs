using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SourceGit.Views {

    /// <summary>
    ///     主窗体
    /// </summary>
    public partial class Launcher : Controls.Window {

        public Launcher() {
            Models.Watcher.Opened += OpenRepository;
            InitializeComponent();
            tabs.Add();

            tabs.OnTabEdited += (t) => {
                foreach (var tab in tabs.Tabs) {
                    if (!tab.IsWelcomePage) continue;
                    var page = container.Get(tab.Id) as Widgets.Welcome;
                    if (page != null) page.UpdateNodes(t.Id, t.Bookmark);
                }
            };
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

        #region OPERATIONS
        private void FillMenu(ContextMenu menu, string icon, string header, RoutedEventHandler onClick) {
            var iconMode = new Path();
            iconMode.Width = 12;
            iconMode.Height = 12;
            iconMode.Data = FindResource(icon) as Geometry;
            iconMode.SetResourceReference(Path.FillProperty, "Brush.FG2");

            var item = new MenuItem();
            item.Icon = iconMode;
            item.Header = App.Text(header);
            item.Click += onClick;

            menu.Items.Add(item);
        }

        private void ToggleMainMenu(object sender, RoutedEventArgs e) {
            var btn = (sender as Button);
            if (btn.ContextMenu != null) {
                btn.ContextMenu.IsOpen = true;
                e.Handled = true;
                return;
            }

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.Bottom;
            menu.PlacementTarget = btn;
            menu.StaysOpen = false;
            menu.Focusable = true;

            FillMenu(menu, "Icon.Preference", "Preference", (o, ev) => {
                var dialog = new Preference() { Owner = this };
                dialog.ShowDialog();
            });

            FillMenu(menu, "Icon.Help", "About", (o, ev) => {
                var dialog = new About() { Owner = this };
                dialog.ShowDialog();
            });

            btn.ContextMenu = menu;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
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
            page.OnNodeEdited += node => tabs.Update(node.Id, node.Bookmark, node.Name);
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

            if (Keyboard.IsKeyDown(Key.Escape)) {
                var page = container.Get(tabs.Current);

                var popup = null as Widgets.PopupPanel;
                if (page is Widgets.Dashboard) {
                    popup = (page as Widgets.Dashboard).popup;
                } else if (page is Widgets.Welcome) {
                    popup = (page as Widgets.Welcome).popup;
                }

                popup?.CancelDirectly();
            }
        }
        #endregion
    }
}
