using System;
using System.Collections.Generic;

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

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ViewModels.Projects.Instance.StartRestore();
            });
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

        private void OnRemoveProject(object sender, RoutedEventArgs e)
        {
            var projectNode = FindProjectNode(Tree.SelectedItem as ViewModels.ProjectTreeNode);
            if (projectNode == null)
                return;

            ViewModels.Projects.Instance.RemoveProject(projectNode);
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
    }
}
