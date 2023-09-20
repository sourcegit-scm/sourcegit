using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     新标签页
    /// </summary>
    public partial class Welcome : UserControl {

        public Welcome() {
            InitializeComponent();
            UpdateVisibles();

            Models.Theme.AddListener(this, UpdateVisibles);
            Models.Watcher.BookmarkChanged += (_, __) => { UpdateVisibles(); };
        }

        #region FUNC_EVENTS
        private void OnOpenClicked(object sender, RoutedEventArgs e) {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) CheckAndOpen(dialog.SelectedPath);
        }

        private void OnOpenTerminalClicked(object sender, RoutedEventArgs e) {
            if (MakeSureReady()) {
                var bash = Path.Combine(Models.Preference.Instance.Git.Path, "..", "bash.exe");
                if (!File.Exists(bash)) {
                    Models.Exception.Raise(App.Text("MissingBash"));
                    return;
                }

                Process.Start(new ProcessStartInfo {
                    FileName = bash,
                    UseShellExecute = true,
                });

                e.Handled = true;
            }
        }

        private void OnCloneClicked(object sender, RoutedEventArgs e) {
            if (MakeSureReady()) {
                var dialog = new Clone();
                dialog.Owner = App.Current.MainWindow;
                dialog.ShowDialog();
            }
        }

        private void FillSortMenu(ContextMenu menu, Models.Preference.SortMethod desired, string label) {
            var item = new MenuItem();
            item.Header = App.Text(label);
            item.Click += (s, ev) => {
                Models.Preference.Instance.General.SortBy = desired;
                UpdateVisibles();
            };

            if (Models.Preference.Instance.General.SortBy == desired) {
                var icon = new System.Windows.Shapes.Path();
                icon.Data = FindResource("Icon.Check") as Geometry;
                icon.Fill = FindResource("Brush.FG1") as Brush;
                icon.Width = 12;
                item.Icon = icon;
            }

            menu.Items.Add(item);
        }

        private void OnSortMethodClicked(object sender, RoutedEventArgs e) {
            var menu = new ContextMenu();
            menu.Placement = PlacementMode.Bottom;
            menu.PlacementTarget = sender as Button;
            menu.StaysOpen = false;
            menu.Focusable = true;

            FillSortMenu(menu, Models.Preference.SortMethod.ByNameASC, "Sort.NameAsc");
            FillSortMenu(menu, Models.Preference.SortMethod.ByNameDESC, "Sort.NameDesc");
            FillSortMenu(menu, Models.Preference.SortMethod.ByRecentlyOpened, "Sort.RecentlyOpened");
            FillSortMenu(menu, Models.Preference.SortMethod.ByBookmark, "Sort.Bookmark");

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void OnRemoveRepository(object sender, RoutedEventArgs e) {
            var repo = (sender as Control).DataContext as Models.Repository;
            if (repo == null) return;

            var confirmDialog = new ConfirmDialog(
                App.Text("Apply.Warn"),
                App.Text("ConfirmRemoveRepo", repo.Path),
                () => {
                    Models.Preference.Instance.RemoveRepository(repo.Path);
                    UpdateVisibles();
                });
            confirmDialog.ShowDialog();
            e.Handled = true;
        }

        private void OnDoubleClickRepository(object sender, MouseButtonEventArgs e) {
            OnOpenRepository(sender, e);
        }

        private void OnRepositoryContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var control = sender as Control;
            if (control == null) return;

            var repo = control.DataContext as Models.Repository;
            if (repo == null) return;

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.MousePoint;
            menu.PlacementTarget = control;
            menu.StaysOpen = false;
            menu.Focusable = true;

            var open = new MenuItem();
            open.Header = App.Text("RepoCM.Open");
            open.Click += OnOpenRepository;
            menu.Items.Add(open);
            menu.Items.Add(new Separator());

            var bookmark = new MenuItem();
            bookmark.Header = App.Text("PageTabBar.Tab.Bookmark");
            for (int i = 0; i < Converters.IntToBookmarkBrush.COLORS.Length; i++) {
                var mark = new MenuItem();
                mark.Icon = new Bookmark() { Color = i, Width = 14, Height = 14 };
                mark.Header = $"{i}";

                var refIdx = i;
                mark.Click += (o, ev) => {
                    repo.Bookmark = refIdx;
                    ev.Handled = true;
                };
                bookmark.Items.Add(mark);
            }
            menu.Items.Add(bookmark);
            menu.Items.Add(new Separator());

            var remove = new MenuItem();
            remove.Header = App.Text("Welcome.Delete");
            remove.Click += OnRemoveRepository;
            menu.Items.Add(remove);

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void OnOpenRepository(object sender, RoutedEventArgs e) {
            var repo = (sender as Control).DataContext as Models.Repository;
            if (repo == null) return;

            CheckAndOpen(repo.Path);
            e.Handled = true;
        }

        private void OnExploreRepository(object sender, RoutedEventArgs e) {
            var repo = (sender as Control).DataContext as Models.Repository;
            if (repo == null) return;

            Process.Start("explorer", repo.Path);
            e.Handled = true;
        }

        private void OnOpenRepositoryTerminal(object sender, RoutedEventArgs e) {
            var repo = (sender as Control).DataContext as Models.Repository;
            if (repo == null) return;

            var bash = Path.Combine(Models.Preference.Instance.Git.Path, "..", "bash.exe");
            if (!File.Exists(bash)) {
                Models.Exception.Raise(App.Text("MissingBash"));
                return;
            }

            Process.Start(new ProcessStartInfo {
                WorkingDirectory = repo.Path,
                FileName = bash,
                UseShellExecute = true,
            });
        }

        private void OnSearchFilterChanged(object sender, TextChangedEventArgs e) {
            UpdateVisibles();
        }

        private void OnPageDrop(object sender, DragEventArgs e) {
            bool rebuild = false;

            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                if (!MakeSureReady()) return;

                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                foreach (var path in paths) {
                    var dir = new Commands.QueryGitDir(path).Result();
                    if (dir != null) {
                        var root = new Commands.GetRepositoryRootPath(path).Result();
                        Models.Preference.Instance.AddRepository(root, dir);
                        rebuild = true;
                    }
                }
            }

            if (rebuild) UpdateVisibles();
        }
        #endregion

        #region DATA
        public void UpdateVisibles() {
            var visibles = new List<Models.Repository>();
            var curFilter = filter.Text.ToLower();

            if (string.IsNullOrEmpty(curFilter)) {
                visibles.AddRange(Models.Preference.Instance.Repositories);
            } else {
                foreach (var repo in Models.Preference.Instance.Repositories) {
                    if (repo.Name.ToLower().IndexOf(curFilter, StringComparison.Ordinal) >= 0 ||
                        repo.Path.ToLower().IndexOf(curFilter, StringComparison.Ordinal) >= 0) {
                        visibles.Add(repo);
                    }
                }
            }

            switch (Models.Preference.Instance.General.SortBy) {
            case Models.Preference.SortMethod.ByNameASC:
                visibles.Sort((l, r) => l.Name.CompareTo(r.Name));
                break;
            case Models.Preference.SortMethod.ByNameDESC:
                visibles.Sort((l, r) => r.Name.CompareTo(l.Name));
                break;
            case Models.Preference.SortMethod.ByRecentlyOpened:
                visibles.Sort((l, r) => r.LastOpenTime.CompareTo(l.LastOpenTime));
                break;
            default:
                visibles.Sort((l, r) => r.Bookmark - l.Bookmark);
                break;
            }

            mask.Visibility = visibles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            repoList.ItemsSource = visibles;
        }

        private bool MakeSureReady() {
            if (!Models.Preference.Instance.IsReady) {
                Models.Exception.Raise(App.Text("NotConfigured"));
                return false;
            }
            return true;
        }

        private void CheckAndOpen(string path) {
            if (!MakeSureReady()) return;

            if (!Directory.Exists(path)) {
                Models.Exception.Raise(App.Text("PathNotFound", path));
                return;
            }

            var root = new Commands.GetRepositoryRootPath(path).Result();
            if (root == null) {
                new Popups.Init(path).Show();
                return;
            }

            var gitDir = new Commands.QueryGitDir(root).Result();
            var repo = Models.Preference.Instance.AddRepository(root, gitDir);
            Models.Watcher.Open(repo);
            UpdateVisibles();
        }
        #endregion
    }
}
