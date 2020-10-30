using System.Collections.ObjectModel;
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
            Git.Repository.OnOpen = repo => {
                Dispatcher.Invoke(() => {
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
                });
            };

            Tabs.Add(new Tab() {
                Title = "SOURCE GIT",
                Tooltip = "Welcome Page",
                AllowDragDrop = false,
                Page = new Manager(),
            });

            InitializeComponent();
            openedTabs.SelectedItem = Tabs[0];
        }

        #region LAYOUT_CONTENT
        /// <summary>
        ///     Close repository tab.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseRepo(object sender, RoutedEventArgs e) {
            var tab = (sender as Button).DataContext as Tab;
            if (tab == null || tab.Repo == null) return;

            Tabs.Remove(tab);
            tab.Page = null;
            tab.Repo.RemovePopup();
            tab.Repo.Close();
            tab.Repo = null;
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

    /// <summary>
    ///     Extension methods for repository.
    /// </summary>
    public static class RepositoryTabBindings {

        /// <summary>
        ///     Bring up tab of repository if it was opened before.
        /// </summary>
        /// <param name="repo"></param>
        /// <returns></returns>
        public static bool BringUpTab(this Git.Repository repo) {
            var main = App.Current.MainWindow as Launcher;

            for (int i = 1; i < main.Tabs.Count; i++) {
                var opened = main.Tabs[i];
                if (opened.Repo.Path == repo.Path) {
                    main.openedTabs.SelectedItem = opened;
                    return true;
                }
            }

            return false;
        }
    }
}
