using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class ProjectTree : UserControl
    {
        public ProjectTree()
        {
            InitializeComponent();
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

            var dirs = await PickDirectoriesAsync("选择要添加的目录");
            if (dirs.Count == 0)
                return;

            if (ViewModels.Projects.Instance.AddDirectories(projectNode, dirs))
                await ViewModels.Projects.Instance.RebuildAsync(projectNode);
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
