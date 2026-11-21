using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class ProjectTree : UserControl
    {
        public ProjectTree()
        {
            InitializeComponent();

            // Without this the bindings below resolve against the inherited DataContext
            // (Launcher's view model), so the tree renders empty even when Projects is full.
            DataContext = ViewModels.Projects.Instance;

            Tree.PropertyChanged += OnTreePropertyChanged;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ViewModels.Projects.Instance.StartRestore();
                TryHookLauncher();
            });

            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        /// <summary>
        ///     The active tab is owned by the launcher, so the tree needs a reference to it
        ///     to follow tab switches. App.GetLauncher() can still be null while the launcher
        ///     window is being built, so this is retried when the control enters the visual tree.
        /// </summary>
        private void TryHookLauncher()
        {
            HookWindow();

            if (_launcher != null)
                return;

            _launcher = App.GetLauncher();
            if (_launcher == null)
                return;

            _launcher.PropertyChanged += OnLauncherPropertyChanged;
            ViewModels.Projects.Instance.HookLauncher();
            SyncSelection();
        }

        /// <summary>
        ///     Watches the host window: when the user returns to the app, files may have been
        ///     changed outside SourceGit, so the tree statuses are re-queried.
        /// </summary>
        private void HookWindow()
        {
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window == null || ReferenceEquals(_window, window))
                return;

            if (_window != null)
                _window.Activated -= OnWindowActivated;

            _window = window;
            _window.Activated += OnWindowActivated;
        }

        private void OnWindowActivated(object sender, EventArgs e)
        {
            // Throttled: alt-tabbing or closing a child window fires this repeatedly.
            var now = DateTime.UtcNow;
            if (now - _lastStatusRefresh < TimeSpan.FromSeconds(3))
                return;

            _lastStatusRefresh = now;
            _ = ViewModels.Projects.Instance.RefreshStatusAsync();
        }

        private void OnAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            TryHookLauncher();
        }

        private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            if (_window != null)
            {
                _window.Activated -= OnWindowActivated;
                _window = null;
            }

            if (_launcher == null)
                return;

            _launcher.PropertyChanged -= OnLauncherPropertyChanged;
            _launcher = null;
        }

        private void OnLauncherPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.Launcher.ActivePage))
                SyncSelection();
        }

        /// <summary>
        ///     Follows the active tab: highlights the repository in the tree and expands the
        ///     directories on the way to it. A welcome tab (its node id is just a generated
        ///     id) or a repository that belongs to no project clears the selection instead.
        /// </summary>
        private void SyncSelection()
        {
            var node = App.GetLauncher()?.ActivePage?.Node;
            if (node == null)
                return;

            var target = ViewModels.Projects.Instance.FindRepository(node.Id);
            if (target == null)
            {
                Tree.SelectedItem = null;
                return;
            }

            ViewModels.Projects.Instance.RevealRepository(target);
            Tree.SelectedItem = target;
            ScrollTo(target);
        }

        /// <summary>
        ///     TreeView has no ScrollIntoView in Avalonia, so the container of the node is
        ///     looked up after the pending layout pass and asked to bring itself into view.
        /// </summary>
        private void ScrollTo(ViewModels.ProjectTreeNode target)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var item in Tree.GetVisualDescendants().OfType<TreeViewItem>())
                {
                    if (!ReferenceEquals(item.DataContext, target))
                        continue;

                    item.BringIntoView();
                    break;
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        private async void OnAddProject(object sender, RoutedEventArgs e)
        {
            var dirs = await PickDirectoriesAsync("选择工程目录");
            if (dirs.Count == 0)
                return;

            var node = ViewModels.Projects.Instance.CreateProject(dirs);
            if (node == null)
                return;

            Tree.SelectedItem = node;
            await ViewModels.Projects.Instance.RebuildAsync(node);
        }

        private async void OnAddDirectory(object sender, RoutedEventArgs e)
        {
            var projectNode = FindProjectNode(Tree.SelectedItem as ViewModels.ProjectTreeNode);
            if (projectNode == null)
            {
                ViewModels.Projects.Instance.StatusText = "请先选中一个工程，再添加目录";
                return;
            }

            await AddDirectoryToAsync(projectNode);
        }

        private async void OnRescan(object sender, RoutedEventArgs e)
        {
            var projectNode = FindProjectNode(Tree.SelectedItem as ViewModels.ProjectTreeNode);
            if (projectNode == null)
            {
                ViewModels.Projects.Instance.StatusText = "请先选中一个工程，再重新扫描";
                return;
            }

            await ViewModels.Projects.Instance.RebuildAsync(projectNode);
        }

        /// <summary>
        ///     Removes exactly what is selected: the project itself, the configured directory
        ///     under it, or one discovered node below that. Nothing on disk is touched.
        /// </summary>
        private void OnRemoveSelection(object sender, RoutedEventArgs e)
        {
            var node = Tree.SelectedItem as ViewModels.ProjectTreeNode;
            if (node == null)
            {
                ViewModels.Projects.Instance.StatusText = "请先选中要移除的工程、目录或仓库";
                return;
            }

            if (node.IsProject)
            {
                ViewModels.Projects.Instance.RemoveProject(node);
                return;
            }

            if (node.Parent?.IsProject == true)
            {
                ViewModels.Projects.Instance.RemoveDirectory(node.Parent, node);
                return;
            }

            ViewModels.Projects.Instance.RemoveNode(node);
        }

        private void OnTreePropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TreeView.SelectedItemProperty)
                UpdateRemoveButton();
        }

        /// <summary>
        ///     Keeps the toolbar button honest about what it is going to remove, so the same
        ///     button no longer silently deletes a whole project while a repository is selected.
        /// </summary>
        private void UpdateRemoveButton()
        {
            var node = Tree.SelectedItem as ViewModels.ProjectTreeNode;
            if (node == null)
                RemoveButton.Content = "移除";
            else if (node.IsProject)
                RemoveButton.Content = "移除工程";
            else if (node.Parent?.IsProject == true)
                RemoveButton.Content = "移除目录";
            else
                RemoveButton.Content = node.IsRepository ? "移除仓库" : "移除目录";
        }

        private void OnItemDoubleTapped(object sender, TappedEventArgs e)
        {
            if (Tree.SelectedItem is ViewModels.ProjectTreeNode node && node.IsRepository)
                node.Open();
        }

        private void OnContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var item = (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>();
            var node = item?.DataContext as ViewModels.ProjectTreeNode;
            if (node == null)
            {
                NodeMenu.Items.Clear();
                return;
            }

            Tree.SelectedItem = node;
            BuildContextMenu(node);
        }

        private void BuildContextMenu(ViewModels.ProjectTreeNode node)
        {
            NodeMenu.Items.Clear();

            var projectNode = FindProjectNode(node);

            if (node.IsRepository)
            {
                AddItem("打开", node.Open);
                AddItem("在文件管理器打开", () => Native.OS.OpenInFileManager(node.FullPath));
                AddItem("在终端打开", () => Native.OS.OpenTerminal(node.FullPath));
                AddTaskItem("复制路径", () => CopyPathAsync(node.FullPath));
                AddSeparator();
            }

            if (node.IsProject)
            {
                AddTaskItem("添加目录…", () => AddDirectoryToAsync(node));
                AddTaskItem("重命名…", () => RenameAsync(node));
                AddSeparator();
            }

            if (node.Parent != null && node.Parent.IsProject)
                AddItem("从工程中移除此目录", () => ViewModels.Projects.Instance.RemoveDirectory(projectNode, node));

            if (projectNode != null)
                AddTaskItem("重新扫描工程", () => ViewModels.Projects.Instance.RebuildAsync(projectNode));

            if (node.IsProject)
            {
                AddSeparator();
                AddItem("移除工程", () => ViewModels.Projects.Instance.RemoveProject(node));
            }
        }

        private void AddItem(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            NodeMenu.Items.Add(item);
        }

        private void AddTaskItem(string header, Func<System.Threading.Tasks.Task> action)
        {
            var item = new MenuItem { Header = header };
            item.Click += async (_, _) => await action();
            NodeMenu.Items.Add(item);
        }

        private void AddSeparator()
        {
            NodeMenu.Items.Add(new MenuItem { Header = "-", IsEnabled = false });
        }

        private async System.Threading.Tasks.Task AddDirectoryToAsync(ViewModels.ProjectTreeNode projectNode)
        {
            if (projectNode?.Project == null)
                return;

            var dirs = await PickDirectoriesAsync("选择要添加的目录");
            if (dirs.Count == 0)
                return;

            if (ViewModels.Projects.Instance.AddDirectories(projectNode, dirs))
                await ViewModels.Projects.Instance.RebuildAsync(projectNode);
        }

        private async System.Threading.Tasks.Task RenameAsync(ViewModels.ProjectTreeNode projectNode)
        {
            if (projectNode?.Project == null)
                return;

            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
                return;

            var dialog = new ProjectRename();
            dialog.SetName(projectNode.Name);

            var result = await dialog.ShowDialog<string>(owner);
            if (!string.IsNullOrEmpty(result))
                ViewModels.Projects.Instance.RenameProject(projectNode, result);
        }

        private async System.Threading.Tasks.Task CopyPathAsync(string path)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(path);
        }

        private static ViewModels.ProjectTreeNode FindProjectNode(ViewModels.ProjectTreeNode node)
        {
            var cursor = node;
            while (cursor != null && !cursor.IsProject)
                cursor = cursor.Parent;
            return cursor;
        }

        private async System.Threading.Tasks.Task<List<string>> PickDirectoriesAsync(string title)
        {
            var result = new List<string>();

            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider == null)
                return result;

            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = true,
            });

            foreach (var folder in folders)
            {
                var local = folder.TryGetLocalPath();
                if (!string.IsNullOrEmpty(local))
                    result.Add(local);
            }

            return result;
        }

        private ViewModels.Launcher _launcher = null;
        private Window _window = null;
        private DateTime _lastStatusRefresh = DateTime.MinValue;
    }
}
