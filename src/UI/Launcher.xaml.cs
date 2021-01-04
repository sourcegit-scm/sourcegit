using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Main window for this app.
    /// </summary>
    public partial class Launcher : Window {
        public static readonly Thickness MAXIMIZE_MARGIN = new Thickness(
            (SystemParameters.MaximizedPrimaryScreenWidth - SystemParameters.WorkArea.Width) / 2);

        /// <summary>
        ///     Tab data.
        /// </summary>
        public class Tab : INotifyPropertyChanged {
            private bool isActive = false;
            private Git.Repository repo = null;
            private object page = null;

            public Git.Repository Repo {
                get { return repo; }
                set {
                    if (repo != value) {
                        repo = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Repo"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Title"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Tooltip"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Color"));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsRepo"));
                    }
                }
            }
            
            public object Page {
                get { return page; }
                set {
                    if (page != value) {
                        page = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Page"));
                    }
                }
            }

            public int Color {
                get { return Repo == null ? 0 : Repo.Color; }
                set {
                    if (Repo == null || Repo.Color == value) return;
                    Repo.Color = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Color"));
                }
            }

            public bool IsActive {
                get { return isActive; }
                set {
                    if (isActive == value) return;
                    isActive = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsActive"));
                }
            }

            public string Title {
                get {
                    if (Repo == null) return "New Page";
                    return Repo.Parent == null ? Repo.Name : $"{Repo.Parent.Name} : {Repo.Name}";
                }
            }

            public string Tooltip {
                get { return Repo == null ? "Repository Manager" : Repo.Path; }
            }

            public bool IsRepo {
                get { return Repo != null; }
            }

            public event PropertyChangedEventHandler PropertyChanged;
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
            InitializeComponent();
            NewTab(null, null);
            if (App.Setting.CheckUpdate) Task.Run(CheckUpdate);
        }

        /// <summary>
        ///     Open repository
        /// </summary>
        /// <param name="repo"></param>
        public void Open(Git.Repository repo) {
            foreach (var opened in Tabs) {
                if (opened.IsRepo && opened.Repo.Path == repo.Path) {
                    openedTabs.SelectedItem = opened;
                    return;
                }
            }

            repo.Open();
            var page = new Dashboard(repo);
            repo.SetPopupManager(page.popupManager);

            var selected = openedTabs.SelectedItem as Tab;
            if (selected != null && !selected.IsRepo) {
                selected.Repo = repo;
                selected.Page = page;
            } else {
                var tab = new Tab() { Repo = repo, Page = page };
                Tabs.Add(tab);
                openedTabs.SelectedItem = tab;
            }
        }

        /// <summary>
        ///     Checking for update.
        /// </summary>
        public void CheckUpdate() {
            try {
                var web = new WebClient() { Encoding = Encoding.UTF8 };
                var raw = web.DownloadString("https://gitee.com/api/v5/repos/sourcegit/SourceGit/releases/latest");
                var ver = JsonConvert.DeserializeObject<Git.Version>(raw);
                var cur = Assembly.GetExecutingAssembly().GetName().Version;

                var matches = Regex.Match(ver.TagName, @"^v(\d+)\.(\d+).*");
                if (!matches.Success) return;

                var major = int.Parse(matches.Groups[1].Value);
                var minor = int.Parse(matches.Groups[2].Value);
                if (major > cur.Major || (major == cur.Major && minor > cur.Minor)) {
                    Dispatcher.Invoke(() => {
                        var dialog = new UpdateAvailable(ver);
                        dialog.Owner = this;
                        dialog.ShowDialog();
                    });
                }
            } catch {
                // IGNORE
            }
        }

        #region LAYOUT_CONTENT
        /// <summary>
        ///     Add new tab.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewTab(object sender, RoutedEventArgs e) {
            var tab = new Tab() { Page = new NewPage() };
            Tabs.Add(tab);
            openedTabs.SelectedItem = tab;
        }

        /// <summary>
        ///     Close tab.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CloseTab(object sender, RoutedEventArgs e) {
            var tab = (sender as Button).DataContext as Tab;
            if (tab == null) return;
            
            if (Tabs.Count == 1) {
                App.Current.Shutdown();
                return;
            }

            tab.Page = null;
            if (tab.IsRepo) {
                tab.Repo.RemovePopup();
                tab.Repo.Close();
                tab.Repo = null;
            }

            Tabs.Remove(tab);            
        }

        /// <summary>
        ///     Context menu for tab items.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TabsContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var tab = (sender as TabItem).DataContext as Tab;
            if (tab == null || tab.Repo == null) {
                ev.Handled = true;
                return;
            }

            var repo = tab.Repo;

            var refresh = new MenuItem();
            refresh.Header = "Refresh";
            refresh.Click += (o, e) => {
                repo.AssertCommand(null);
                e.Handled = true;
            };

            var iconBookmark = FindResource("Icon.Bookmark") as Geometry;
            var bookmark = new MenuItem();
            bookmark.Header = "Bookmark";
            for (int i = 0; i < Converters.IntToRepoColor.Colors.Length; i++) {
                var icon = new System.Windows.Shapes.Path();
                icon.Style = FindResource("Style.Icon") as Style;
                icon.Data = iconBookmark;
                icon.Fill = Converters.IntToRepoColor.Colors[i];
                icon.Width = 8;

                var mark = new MenuItem();
                mark.Icon = icon;
                mark.Header = $"{i}";

                var refIdx = i;
                mark.Click += (o, e) => {
                    tab.Color = refIdx;
                    e.Handled = true;
                };

                bookmark.Items.Add(mark);
            }

            var copyPath = new MenuItem();
            copyPath.Header = "Copy path";
            copyPath.Click += (o, e) => {
                Clipboard.SetText(repo.Path);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(refresh);
            menu.Items.Add(bookmark);
            menu.Items.Add(copyPath);
            menu.IsOpen = true;

            ev.Handled = true;
        }

        /// <summary>
        ///     Open preference dialog.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowPreference(object sender, RoutedEventArgs e) {
            var dialog = new SettingDialog();
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
            var item = e.Source as TabItem;
            if (item == null) return;

            var tab = item.DataContext as Tab;
            if (tab == null) return;

            if (Mouse.LeftButton == MouseButtonState.Pressed) {
                DragDrop.DoDragDrop(item, item, DragDropEffects.All);
                e.Handled = true;
            }
        }

        private void TabsDrop(object sender, DragEventArgs e) {
            var tabItemSrc = e.Data.GetData(typeof(TabItem)) as TabItem;
            var tabItemDst = e.Source as TabItem;
            if (tabItemSrc.Equals(tabItemDst)) return;

            var tabSrc = tabItemSrc.DataContext as Tab;
            var tabDst = tabItemDst.DataContext as Tab;
            int dstIdx = Tabs.IndexOf(tabDst);

            Tabs.Remove(tabSrc);
            Tabs.Insert(dstIdx, tabSrc);
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
