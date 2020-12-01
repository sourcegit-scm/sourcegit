using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SourceGit.UI {

    /// <summary>
    ///     Main window for this app.
    /// </summary>
    public partial class Launcher : Window {

        /// <summary>
        ///     Tab data.
        /// </summary>
        public class Tab {
            public string Title { get; set; }
            public string Tooltip { get; set; }
            public bool AllowDragDrop { get; set; }
            public bool IsActive { get; set; }
            public Git.Repository Repo { get; set; }
            public object Page { get; set; }
        }

        /// <summary>
        ///     Alerts.
        /// </summary>
        public ObservableCollection<string> Errors { get; set; } = new ObservableCollection<string>();

        /// <summary>
        ///     Opened tabs.
        /// </summary>
        public ObservableCollection<Tab> Tabs { get; set; } = new ObservableCollection<Tab>();

        /// <summary>
        ///     Constructor
        /// </summary>
        public Launcher() {
            Tabs.Add(new Tab() {
                Title = "HOME",
                Tooltip = "Repositories Manager",
                AllowDragDrop = false,
                Page = new Manager(),
            });

            InitializeComponent();
            openedTabs.SelectedItem = Tabs[0];

            if (App.Preference.CheckUpdate) Task.Run(CheckUpdate);
        }

        /// <summary>
        ///     Open repository
        /// </summary>
        /// <param name="repo"></param>
        public void Open(Git.Repository repo) {
            for (int i = 1; i < Tabs.Count; i++) {
                var opened = Tabs[i];
                if (opened.Repo.Path == repo.Path) {
                    openedTabs.SelectedItem = opened;
                    return;
                }
            }

            repo.Open();

            var page = new Dashboard(repo);
            var tab = new Tab() {
                Title = repo.Parent == null ? repo.Name : $"{repo.Parent.Name} : {repo.Name}",
                Tooltip = repo.Path,
                AllowDragDrop = true,
                Repo = repo,
                Page = page,
            };

            repo.SetPopupManager(page.popupManager);
            Tabs.Add(tab);
            openedTabs.SelectedItem = tab;
        }

        /// <summary>
        ///     Checking for update.
        /// </summary>
        public void CheckUpdate() {
            try {
                var web = new WebClient();
                var raw = web.DownloadString("https://gitee.com/api/v5/repos/sourcegit/SourceGit/releases/latest");
                var ver = JsonSerializer.Deserialize<Git.Version>(raw);
                var cur = Assembly.GetExecutingAssembly().GetName().Version;

                var matches = Regex.Match(ver.TagName, @"^v(\d+)\.(\d+).*");
                if (!matches.Success) return;

                var major = int.Parse(matches.Groups[1].Value);
                var minor = int.Parse(matches.Groups[2].Value);
                if (major > cur.Major || (major == cur.Major && minor > cur.Minor)) {
                    Dispatcher.Invoke(() => {
                        var dialog = new UpdateAvailable(ver);
                        dialog.Owner = this;
                        dialog.Show();
                    });
                }
            } catch {
                // IGNORE
            }
        }

        #region LAYOUT_CONTENT
        /// <summary>
        ///     Context menu for tab items.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TabsContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var tab = (sender as TabItem).DataContext as Tab;
            if (tab == null) {
                ev.Handled = true;
                return;
            }

            var repo = tab.Repo;
            if (repo == null) {
                ev.Handled = true;
                return;
            }

            var close = new MenuItem();
            close.Header = "Close";
            close.Click += (o, e) => {
                Tabs.Remove(tab);

                tab.Page = null;
                tab.Repo.RemovePopup();
                tab.Repo.Close();
                tab.Repo = null;
            };

            var copyPath = new MenuItem();
            copyPath.Header = "Copy Path";
            copyPath.Click += (o, e) => {
                Clipboard.SetText(repo.Path);
                e.Handled = true;
            };

            var refresh = new MenuItem();
            refresh.Header = "Refresh";
            refresh.Click += (o, e) => {
                repo.AssertCommand(null);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(close);
            menu.Items.Add(new Separator());
            menu.Items.Add(copyPath);
            menu.Items.Add(refresh);
            menu.IsOpen = true;

            ev.Handled = true;
        }

        /// <summary>
        ///     Open preference dialog.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowPreference(object sender, RoutedEventArgs e) {
            var dialog = new Preference();
            dialog.Owner = this;
            dialog.ShowDialog();
        }

        /// <summary>
        ///     Open about dialog.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowAbout(object sender, RoutedEventArgs e) {
            var about = new About();
            about.Owner = this;
            about.ShowDialog();
        }

        /// <summary>
        ///     Remove an alert.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveError(object sender, RoutedEventArgs e) {
            var alert = (sender as Button).DataContext as string;
            Errors.Remove(alert);
        }
        #endregion

        #region WINDOW_COMMANDS
        /// <summary>
        ///     Minimize
        /// </summary>
        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        /// <summary>
        ///     Maximize/Restore
        /// </summary>
        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
        }

        /// <summary>
        ///     Quit
        /// </summary>
        private void Quit(object sender, RoutedEventArgs e) {
            App.Current.Shutdown();
        }
        #endregion

        #region DRAG_DROP
        private void TabsMouseMove(object sender, MouseEventArgs e) {
            var tab = e.Source as TabItem;
            if (tab == null || (tab.DataContext as Tab).Repo == null) return;

            if (Mouse.LeftButton == MouseButtonState.Pressed) {
                DragDrop.DoDragDrop(tab, tab, DragDropEffects.All);
                e.Handled = true;
            }
        }

        private void TabsDrop(object sender, DragEventArgs e) {
            var tabItemSrc = e.Data.GetData(typeof(TabItem)) as TabItem;
            var tabItemDst = e.Source as TabItem;
            if (tabItemSrc.Equals(tabItemDst)) return;

            var tabSrc = tabItemSrc.DataContext as Tab;
            var tabDst = tabItemDst.DataContext as Tab;
            if (tabDst.Repo == null) {
                Tabs.Remove(tabSrc);
                Tabs.Insert(1, tabSrc);
            } else {
                int dstIdx = Tabs.IndexOf(tabDst);

                Tabs.Remove(tabSrc);
                Tabs.Insert(dstIdx, tabSrc);
            }
        }
        #endregion

        #region TAB_SCROLL
        private void OpenedTabsSizeChanged(object sender, SizeChangedEventArgs e) {
            if (openedTabs.ActualWidth > openedTabsColumn.ActualWidth) {
                openedTabsOpts.Visibility = Visibility.Visible;
            } else {
                openedTabsOpts.Visibility = Visibility.Collapsed;
            }
        }

        private void ScrollToLeft(object sender, RoutedEventArgs e) {
            openedTabsScroller.LineLeft();
        }

        private void ScrollToRight(object sender, RoutedEventArgs e) {
            openedTabsScroller.LineRight();
        }
        #endregion
    }
}
