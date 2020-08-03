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
            public bool IsActive { get; set; }
            public Git.Repository Repo { get; set; }
            public object Page { get; set; }
        }

        /// <summary>
        ///     Alert data.
        /// </summary>
        public class Alert {
            public string Title { get; set; }
            public string Message { get; set; }
        }

        /// <summary>
        ///     Alerts.
        /// </summary>
        public ObservableCollection<Alert> Alerts { get; set; } = new ObservableCollection<Alert>();

        /// <summary>
        ///     Opened tabs.
        /// </summary>
        public ObservableCollection<Tab> Tabs { get; set; } = new ObservableCollection<Tab>();

        /// <summary>
        ///     Constructor
        /// </summary>
        public Launcher() {
            App.OnError = msg => {
                ShowAlert(new Alert() { Title = "ERROR", Message = msg });
            };

            Git.Repository.OnOpen = repo => {
                Dispatcher.Invoke(() => {
                    foreach (var item in openedTabs.Items) {
                        var opened = item as Tab;
                        if (opened != null && opened.Repo != null && repo.Path == opened.Repo.Path) {
                            openedTabs.SelectedItem = opened;
                            return;
                        }
                    }

                    var tab = new Tab() {
                        Title = repo.Parent == null ? repo.Name : $"{repo.Parent.Name} : {repo.Name}",
                        Repo = repo,
                        Page = new Dashboard(repo),
                    };

                    Tabs.Add(tab);
                    openedTabs.SelectedItem = tab;
                });
            };

            Tabs.Add(new Tab() {
                Title = "SOURCE GIT",
                Page = new Manager(),
            });

            InitializeComponent();
            openedTabs.SelectedItem = Tabs[0];
        }

        /// <summary>
        ///     Get popup manager from given active page.
        /// </summary>
        /// <param name="repo"></param>
        /// <returns></returns>
        public PopupManager GetPopupManager(Git.Repository repo) {
            if (repo == null) return (Tabs[0].Page as Manager).popupManager;

            foreach (var tab in Tabs) {
                if (tab.Repo != null && tab.Repo.Path == repo.Path) {
                    return (tab.Page as Dashboard).popupManager;
                }
            }

            return null;
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

            var popup = (tab.Page as Dashboard).popupManager;
            if (popup.IsLocked()) popup.Close(true);
            Tabs.Remove(tab);

            tab.Page = null;
            tab.Repo.Close();
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
        ///     Show alert.
        /// </summary>
        /// <param name="alert"></param>
        private void ShowAlert(Alert alert) {
            Dispatcher.Invoke(() => Alerts.Add(alert));
        }

        /// <summary>
        ///     Remove an alert.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveAlert(object sender, RoutedEventArgs e) {
            var alert = (sender as Button).DataContext as Alert;
            Alerts.Remove(alert);
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
    }
}
