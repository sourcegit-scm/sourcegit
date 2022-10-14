using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     新标签页
    /// </summary>
    public partial class Welcome : UserControl, Controls.IPopupContainer {

        /// <summary>
        ///     修改仓库标签颜色的回调
        /// </summary>
        public event Action<Models.Repository> OnBookmarkChanged;

        public Welcome() {
            InitializeComponent();
            UpdateVisibles();
        }

        #region POPUP_CONTAINER
        public void Show(Controls.PopupWidget widget) {
            popup.Show(widget);
        }

        public void ShowAndStart(Controls.PopupWidget widget) {
            popup.ShowAndStart(widget);
        }

        public void UpdateProgress(string message) {
            popup.UpdateProgress(message);
        }
        #endregion

        #region FUNC_EVENTS
        private void OnOpenClicked(object sender, RoutedEventArgs e) {
            var dialog = new Controls.FolderDialog();
            if (dialog.ShowDialog() == true) CheckAndOpen(dialog.SelectedPath);
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
            if (MakeSureReady()) new Popups.Clone().Show();
        }

        private void OnRemoveRepository(object sender, RoutedEventArgs e) {
            var repo = (sender as Button).DataContext as Models.Repository;
            Models.Preference.Instance.RemoveRepository(repo.Path);
            UpdateVisibles();
            e.Handled = true;
        }

        private void OnDoubleClickRepository(object sender, MouseButtonEventArgs e) {
            OnOpenRepository(sender, e);
        }

        private void OnOpenRepository(object sender, RoutedEventArgs e) {
            var repo = (sender as Control).DataContext as Models.Repository;
            CheckAndOpen(repo.Path);
            e.Handled = true;
        }

        private void OnExploreRepository(object sender, RoutedEventArgs e) {
            var repo = (sender as Control).DataContext as Models.Repository;
            Process.Start("explorer", repo.Path);
            e.Handled = true;
        }

        private void OnChangeRepositoryBookmark(object sender, RoutedEventArgs e) {
            var btn = (sender as Button);
            var repo = btn.DataContext as Models.Repository;

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.Bottom;
            menu.PlacementTarget = btn;
            menu.StaysOpen = false;
            menu.Focusable = true;

            for (int i = 0; i < Converters.IntToBookmarkBrush.COLORS.Length; i++) {
                var icon = new System.Windows.Shapes.Path();
                icon.Data = new EllipseGeometry(new Point(0, 0), 8, 8);
                icon.Fill = i == 0 ? (FindResource("Brush.FG1") as Brush) : Converters.IntToBookmarkBrush.COLORS[i];
                icon.Width = 12;

                var mark = new MenuItem();
                mark.Icon = icon;
                mark.Header = $"{i}"; 

                var refIdx = i;
                mark.Click += (o, ev) => {
                    if (repo != null) {
                        repo.Bookmark = refIdx;
                        UpdateVisibles();
                        OnBookmarkChanged?.Invoke(repo);
                    }
                    ev.Handled = true;
                };

                menu.Items.Add(mark);
            }

            btn.ContextMenu = menu;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void OnOpenRepositoryTerminal(object sender, RoutedEventArgs e) {
            var repo = (sender as Button).DataContext as Models.Repository;
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
        #endregion

        #region DRAG_DROP
        private void OnPageDragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                dropArea.Visibility = Visibility.Visible;
            }
        }

        private void OnPageDragLeave(object sender, DragEventArgs e) {
            dropArea.Visibility = Visibility.Hidden;
        }

        private void OnPageDrop(object sender, DragEventArgs e) {
            dropArea.Visibility = Visibility.Hidden;
        }

        private void OnDropFolder(object sender, DragEventArgs e) {
            bool rebuild = false;
            dropArea.Visibility = Visibility.Hidden;

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
                    if (repo.Name.ToLower().Contains(curFilter, StringComparison.Ordinal) ||
                        repo.Path.ToLower().Contains(curFilter, StringComparison.Ordinal)) {
                        visibles.Add(repo);
                    }
                }
            }

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
