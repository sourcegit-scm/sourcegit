using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class RevisionFileTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override async void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.RevisionFileTreeNode { IsFolder: true } node)
            {
                var tree = this.FindAncestorOfType<RevisionFileTreeView>();
                if (tree != null)
                    await tree.ToggleNodeIsExpandedAsync(node);
            }

            e.Handled = true;
        }
    }

    public class RevisionTreeNodeIcon : UserControl
    {
        public static readonly StyledProperty<ViewModels.RevisionFileTreeNode> NodeProperty =
            AvaloniaProperty.Register<RevisionTreeNodeIcon, ViewModels.RevisionFileTreeNode>(nameof(Node));

        public ViewModels.RevisionFileTreeNode Node
        {
            get => GetValue(NodeProperty);
            set => SetValue(NodeProperty, value);
        }

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<RevisionTreeNodeIcon, bool>(nameof(IsExpanded));

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        static RevisionTreeNodeIcon()
        {
            NodeProperty.Changed.AddClassHandler<RevisionTreeNodeIcon>((icon, _) => icon.UpdateContent());
            IsExpandedProperty.Changed.AddClassHandler<RevisionTreeNodeIcon>((icon, _) => icon.UpdateContent());
        }

        private void UpdateContent()
        {
            var node = Node;
            if (node?.Backend == null)
            {
                Content = null;
                return;
            }

            var obj = node.Backend;
            switch (obj.Type)
            {
                case Models.ObjectType.Blob:
                    CreateContent("Icons.File", new Thickness(0, 0, 0, 0));
                    break;
                case Models.ObjectType.Commit:
                    CreateContent("Icons.Submodule", new Thickness(0, 0, 0, 0));
                    break;
                default:
                    CreateContent(node.IsExpanded ? "Icons.Folder.Open" : "Icons.Folder", new Thickness(0, 2, 0, 0), Brushes.Goldenrod);
                    break;
            }
        }

        private void CreateContent(string iconKey, Thickness margin, IBrush fill = null)
        {
            if (this.FindResource(iconKey) is not StreamGeometry geo)
                return;

            var icon = new Avalonia.Controls.Shapes.Path()
            {
                Width = 14,
                Height = 14,
                Margin = margin,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Data = geo,
            };

            if (fill != null)
                icon.Fill = fill;

            Content = icon;
        }
    }

    public class RevisionFileRowsListBox : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F && e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                var panel = this.FindAncestorOfType<RevisionFileTreeView>();
                panel.RaiseEvent(new RoutedEventArgs(RevisionFileTreeView.SearchRequestedEvent));
                e.Handled = true;
                return;
            }

            if (SelectedItem is ViewModels.RevisionFileTreeNode node)
            {
                if (e is { Key: Key.Left, KeyModifiers: KeyModifiers.None })
                {
                    if (node.IsFolder && node.IsExpanded)
                    {
                        var tree = this.FindAncestorOfType<RevisionFileTreeView>();
                        if (tree != null)
                            await tree.ToggleNodeIsExpandedAsync(node);

                        e.Handled = true;
                    }
                    else if (FindParent(node) is { } parent)
                    {
                        Select(parent);
                        e.Handled = true;
                    }
                }
                else if (e is { Key: Key.Right, KeyModifiers: KeyModifiers.None })
                {
                    if (node.IsFolder && !node.IsExpanded)
                    {
                        var tree = this.FindAncestorOfType<RevisionFileTreeView>();
                        if (tree != null)
                            await tree.ToggleNodeIsExpandedAsync(node);

                        e.Handled = true;
                    }
                    else if (node.Children.Count > 0)
                    {
                        Select(node.Children[0]);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.C &&
                    e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
                {
                    var detailView = this.FindAncestorOfType<CommitDetail>();
                    if (detailView is { DataContext: ViewModels.CommitDetail detail })
                    {
                        var path = node.Backend?.Path ?? string.Empty;
                        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                            path = detail.GetAbsPath(path);

                        await this.CopyTextAsync(path);
                        e.Handled = true;
                    }
                }
                else if (node.Backend is { Type: Models.ObjectType.Blob } file &&
                    e.Key == Key.S &&
                    e.KeyModifiers == ((OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) | KeyModifiers.Shift))
                {
                    var detailView = this.FindAncestorOfType<CommitDetail>();
                    if (detailView is { DataContext: ViewModels.CommitDetail detail })
                    {
                        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                        if (storageProvider == null)
                            return;

                        var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                        try
                        {
                            var selected = await storageProvider.OpenFolderPickerAsync(options);
                            if (selected.Count == 1)
                            {
                                var folder = selected[0];
                                var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                                var saveTo = Path.Combine(folderPath, Path.GetFileName(file.Path)!);
                                await detail.SaveRevisionFileAsync(file, saveTo);
                            }
                        }
                        catch (Exception ex)
                        {
                            detail.Repository.SendNotification($"Failed to save file: {ex.Message}", true);
                        }

                        e.Handled = true;
                    }
                }
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }

        private void Select(object item)
        {
            SelectedItem = item;
            ScrollIntoView(item);
            ContainerFromItem(item)?.Focus();
        }

        private ViewModels.RevisionFileTreeNode FindParent(ViewModels.RevisionFileTreeNode item)
        {
            if (item.Depth == 0)
                return null;

            var idx = Items.IndexOf(item);
            if (idx < 1)
                return null;

            for (var i = idx - 1; i >= 0; i--)
            {
                if (Items[i] is ViewModels.RevisionFileTreeNode node && node.Depth < item.Depth)
                    return node;
            }

            return null;
        }
    }

    public partial class RevisionFileTreeView : UserControl
    {
        public static readonly StyledProperty<string> RevisionProperty =
            AvaloniaProperty.Register<RevisionFileTreeView, string>(nameof(Revision));

        public string Revision
        {
            get => GetValue(RevisionProperty);
            set => SetValue(RevisionProperty, value);
        }

        public AvaloniaList<ViewModels.RevisionFileTreeNode> Rows { get; } = [];

        public static readonly RoutedEvent<RoutedEventArgs> SearchRequestedEvent =
            RoutedEvent.Register<BranchTree, RoutedEventArgs>(nameof(SearchRequested), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> SearchRequested
        {
            add { AddHandler(SearchRequestedEvent, value); }
            remove { RemoveHandler(SearchRequestedEvent, value); }
        }

        public RevisionFileTreeView()
        {
            InitializeComponent();
        }

        public async Task SetSearchResultAsync(string file)
        {
            Rows.Clear();
            _searchResult.Clear();

            var rows = new List<ViewModels.RevisionFileTreeNode>();
            if (string.IsNullOrEmpty(file))
            {
                MakeRows(rows, _tree, 0);
            }
            else
            {
                var vm = DataContext as ViewModels.CommitDetail;
                if (vm?.Commit == null)
                    return;

                var objects = await vm.GetRevisionFilesUnderFolderAsync(file);
                if (objects is not { Count: 1 })
                    return;

                var routes = file.Split('/');
                if (routes.Length == 1)
                {
                    _searchResult.Add(new ViewModels.RevisionFileTreeNode
                    {
                        Backend = objects[0]
                    });
                }
                else
                {
                    var last = _searchResult;
                    var prefix = string.Empty;
                    for (var i = 0; i < routes.Length - 1; i++)
                    {
                        var folder = new ViewModels.RevisionFileTreeNode
                        {
                            Backend = new Models.Object
                            {
                                Type = Models.ObjectType.Tree,
                                Path = prefix + routes[i],
                            },
                            IsExpanded = true,
                        };

                        last.Add(folder);
                        last = folder.Children;
                        prefix = folder.Backend + "/";
                    }

                    last.Add(new ViewModels.RevisionFileTreeNode
                    {
                        Backend = objects[0]
                    });
                }

                MakeRows(rows, _searchResult, 0);
            }

            Rows.AddRange(rows);
            GC.Collect();
        }

        public async Task ToggleNodeIsExpandedAsync(ViewModels.RevisionFileTreeNode node)
        {
            _disableSelectionChangingEvent = true;
            node.IsExpanded = !node.IsExpanded;

            var depth = node.Depth;
            var idx = Rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                var subtree = await GetChildrenOfTreeNodeAsync(node);
                if (subtree is { Count: > 0 })
                {
                    var subrows = new List<ViewModels.RevisionFileTreeNode>();
                    MakeRows(subrows, subtree, depth + 1);
                    Rows.InsertRange(idx + 1, subrows);
                }
            }
            else
            {
                var removeCount = 0;
                for (int i = idx + 1; i < Rows.Count; i++)
                {
                    var row = Rows[i];
                    if (row.Depth <= depth)
                        break;

                    removeCount++;
                }
                Rows.RemoveRange(idx + 1, removeCount);
            }

            _disableSelectionChangingEvent = false;
        }

        protected override async void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == RevisionProperty)
            {
                _tree.Clear();
                _searchResult.Clear();

                if (DataContext is ViewModels.CommitDetail { ActiveTabIndex: 2 } vm)
                    await ReloadTreeData(vm);
                else
                    Rows.Clear();
            }
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (DataContext is ViewModels.CommitDetail vm && _tree.Count == 0)
                await ReloadTreeData(vm);
        }

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail { Repository: { } repo, Commit: { } commit } vm &&
                sender is Grid { DataContext: ViewModels.RevisionFileTreeNode { Backend: { } obj } } grid)
            {
                var menu = obj.Type switch
                {
                    Models.ObjectType.Tree => CreateRevisionFileContextMenuByFolder(repo, vm, commit, obj.Path),
                    _ => CreateRevisionFileContextMenu(repo, vm, commit, obj),
                };
                menu.Open(grid);
            }

            e.Handled = true;
        }

        private async void OnTreeNodeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RevisionFileTreeNode { IsFolder: true } node })
            {
                var posX = e.GetPosition(this).X;
                if (posX < node.Depth * 16 + 16)
                    return;

                await ToggleNodeIsExpandedAsync(node);
            }
        }

        private async void OnRowsSelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            if (_disableSelectionChangingEvent || DataContext is not ViewModels.CommitDetail vm)
                return;

            if (sender is ListBox { SelectedItem: ViewModels.RevisionFileTreeNode { IsFolder: false } node })
                await vm.ViewRevisionFileAsync(node.Backend);
            else
                await vm.ViewRevisionFileAsync(null);
        }

        private async Task<List<ViewModels.RevisionFileTreeNode>> GetChildrenOfTreeNodeAsync(ViewModels.RevisionFileTreeNode node)
        {
            if (!node.IsFolder)
                return null;

            if (node.Children.Count > 0)
                return node.Children;

            if (DataContext is not ViewModels.CommitDetail vm)
                return null;

            var objects = await vm.GetRevisionFilesUnderFolderAsync(node.Backend.Path + "/");
            if (objects == null || objects.Count == 0)
                return null;

            foreach (var obj in objects)
                node.Children.Add(new ViewModels.RevisionFileTreeNode() { Backend = obj });

            SortNodes(node.Children);
            return node.Children;
        }

        private void MakeRows(List<ViewModels.RevisionFileTreeNode> rows, List<ViewModels.RevisionFileTreeNode> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                node.Depth = depth;
                rows.Add(node);

                if (!node.IsExpanded || !node.IsFolder)
                    continue;

                MakeRows(rows, node.Children, depth + 1);
            }
        }

        private void SortNodes(List<ViewModels.RevisionFileTreeNode> nodes)
        {
            nodes.Sort((l, r) =>
            {
                if (l.IsFolder == r.IsFolder)
                    return Models.NumericSort.Compare(l.Name, r.Name);
                return l.IsFolder ? -1 : 1;
            });
        }

        private async Task ReloadTreeData(ViewModels.CommitDetail vm)
        {
            if (_isReloadingTreeData)
                return;

            _isReloadingTreeData = true;

            if (vm?.Commit == null)
            {
                Rows.Clear();
                _isReloadingTreeData = false;
                return;
            }

            var objects = await vm.GetRevisionFilesUnderFolderAsync(null);
            if (objects == null || objects.Count == 0)
            {
                Rows.Clear();
                _isReloadingTreeData = false;
                return;
            }

            foreach (var obj in objects)
                _tree.Add(new ViewModels.RevisionFileTreeNode { Backend = obj });

            SortNodes(_tree);

            var topTree = new List<ViewModels.RevisionFileTreeNode>();
            MakeRows(topTree, _tree, 0);

            Rows.Clear();
            Rows.AddRange(topTree);
            _isReloadingTreeData = false;
        }

        private ContextMenu CreateRevisionFileContextMenuByFolder(ViewModels.Repository repo, ViewModels.CommitDetail vm, Models.Commit commit, string path)
        {
            var fullPath = Native.OS.GetAbsPath(repo.FullPath, path);
            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = this.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = Directory.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath);
                ev.Handled = true;
            };

            var history = new MenuItem();
            history.Header = App.Text("DirHistories");
            history.Icon = this.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                this.ShowWindow(new ViewModels.DirHistories(repo, path, commit.SHA));
                ev.Handled = true;
            };

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(path);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await this.CopyTextAsync(fullPath);
                e.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(history);
            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);
            return menu;
        }

        private ContextMenu CreateRevisionFileContextMenu(ViewModels.Repository repo, ViewModels.CommitDetail vm, Models.Commit commit, Models.Object file)
        {
            var fullPath = Native.OS.GetAbsPath(repo.FullPath, file.Path);
            var menu = new ContextMenu();

            var openWith = new MenuItem();
            openWith.Header = App.Text("Open");
            openWith.Icon = this.CreateMenuIcon("Icons.OpenWith");
            openWith.IsEnabled = file.Type == Models.ObjectType.Blob;
            if (openWith.IsEnabled)
            {
                var defaultEditor = new MenuItem();
                defaultEditor.Header = App.Text("Open.SystemDefaultEditor");
                defaultEditor.Tag = OperatingSystem.IsMacOS() ? "⌘+O" : "Ctrl+O";
                defaultEditor.Click += async (_, ev) =>
                {
                    await vm.OpenRevisionFileAsync(file.Path, null);
                    ev.Handled = true;
                };

                openWith.Items.Add(defaultEditor);

                var tools = Native.OS.ExternalTools;
                if (tools.Count > 0)
                {
                    openWith.Items.Add(new MenuItem() { Header = "-" });

                    for (var i = 0; i < tools.Count; i++)
                    {
                        var tool = tools[i];
                        var item = new MenuItem();
                        item.Header = tool.Name;
                        item.Icon = new Image { Width = 16, Height = 16, Source = tool.IconImage };
                        item.Click += async (_, ev) =>
                        {
                            await vm.OpenRevisionFileAsync(file.Path, tool);
                            ev.Handled = true;
                        };

                        openWith.Items.Add(item);
                    }
                }
            }

            var saveAs = new MenuItem();
            saveAs.Header = App.Text("SaveAs");
            saveAs.Icon = this.CreateMenuIcon("Icons.Save");
            saveAs.IsEnabled = file.Type == Models.ObjectType.Blob;
            saveAs.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+S" : "Ctrl+Shift+S";
            saveAs.Click += async (_, ev) =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                    return;

                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                try
                {
                    var selected = await storageProvider.OpenFolderPickerAsync(options);
                    if (selected.Count == 1)
                    {
                        var folder = selected[0];
                        var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                        var saveTo = Path.Combine(folderPath, Path.GetFileName(file.Path)!);
                        await vm.SaveRevisionFileAsync(file, saveTo);
                    }
                }
                catch (Exception e)
                {
                    repo.SendNotification($"Failed to save file: {e.Message}", true);
                }

                ev.Handled = true;
            };

            var explore = new MenuItem();
            explore.Header = App.Text("RevealFile");
            explore.Icon = this.CreateMenuIcon("Icons.Explore");
            explore.IsEnabled = File.Exists(fullPath) || Directory.Exists(fullPath);
            explore.Click += (_, ev) =>
            {
                Native.OS.OpenInFileManager(fullPath);
                ev.Handled = true;
            };

            menu.Items.Add(openWith);
            menu.Items.Add(saveAs);
            menu.Items.Add(explore);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var history = new MenuItem();
            history.Header = App.Text("FileHistory");
            history.Icon = this.CreateMenuIcon("Icons.Histories");
            history.Click += (_, ev) =>
            {
                this.ShowWindow(new ViewModels.FileHistories(repo.FullPath, file.Path, commit.SHA));
                ev.Handled = true;
            };

            var blame = new MenuItem();
            blame.Header = App.Text("Blame");
            blame.Icon = this.CreateMenuIcon("Icons.Blame");
            blame.IsEnabled = file.Type == Models.ObjectType.Blob;
            blame.Click += (_, ev) =>
            {
                this.ShowWindow(new ViewModels.Blame(repo.FullPath, file.Path, commit));
                ev.Handled = true;
            };

            menu.Items.Add(history);
            menu.Items.Add(blame);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var resetToThisRevision = new MenuItem();
                resetToThisRevision.Header = App.Text("ChangeCM.CheckoutThisRevision");
                resetToThisRevision.Icon = this.CreateMenuIcon("Icons.File.Checkout");
                resetToThisRevision.Click += async (_, ev) =>
                {
                    await vm.ResetToThisRevisionAsync(file.Path);
                    ev.Handled = true;
                };

                menu.Items.Add(resetToThisRevision);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (repo.Remotes.Count > 0 && File.Exists(fullPath) && repo.IsLFSEnabled())
                {
                    var lfs = new MenuItem();
                    lfs.Header = App.Text("GitLFS");
                    lfs.Icon = this.CreateMenuIcon("Icons.LFS");

                    var lfsLock = new MenuItem();
                    lfsLock.Header = App.Text("GitLFS.Locks.Lock");
                    lfsLock.Icon = this.CreateMenuIcon("Icons.Lock");
                    if (repo.Remotes.Count == 1)
                    {
                        lfsLock.Click += async (_, e) =>
                        {
                            await repo.LockLFSFileAsync(repo.Remotes[0].Name, file.Path);
                            e.Handled = true;
                        };
                    }
                    else
                    {
                        foreach (var remote in repo.Remotes)
                        {
                            var remoteName = remote.Name;
                            var lockRemote = new MenuItem();
                            lockRemote.Header = remoteName;
                            lockRemote.Click += async (_, e) =>
                            {
                                await repo.LockLFSFileAsync(remoteName, file.Path);
                                e.Handled = true;
                            };
                            lfsLock.Items.Add(lockRemote);
                        }
                    }
                    lfs.Items.Add(lfsLock);

                    var lfsUnlock = new MenuItem();
                    lfsUnlock.Header = App.Text("GitLFS.Locks.Unlock");
                    lfsUnlock.Icon = this.CreateMenuIcon("Icons.Unlock");
                    if (repo.Remotes.Count == 1)
                    {
                        lfsUnlock.Click += async (_, e) =>
                        {
                            await repo.UnlockLFSFileAsync(repo.Remotes[0].Name, file.Path, false, true);
                            e.Handled = true;
                        };
                    }
                    else
                    {
                        foreach (var remote in repo.Remotes)
                        {
                            var remoteName = remote.Name;
                            var unlockRemote = new MenuItem();
                            unlockRemote.Header = remoteName;
                            unlockRemote.Click += async (_, e) =>
                            {
                                await repo.UnlockLFSFileAsync(remoteName, file.Path, false, true);
                                e.Handled = true;
                            };
                            lfsUnlock.Items.Add(unlockRemote);
                        }
                    }
                    lfs.Items.Add(lfsUnlock);

                    menu.Items.Add(lfs);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }
            }

            var actions = repo.GetCustomActions(Models.CustomActionScope.File);
            if (actions.Count > 0)
            {
                var target = new Models.CustomActionTargetFile(file.Path, vm.Commit);
                var custom = new MenuItem();
                custom.Header = App.Text("FileCM.CustomAction");
                custom.Icon = this.CreateMenuIcon("Icons.Action");

                foreach (var action in actions)
                {
                    var (dup, label) = action;
                    var item = new MenuItem();
                    item.Icon = this.CreateMenuIcon("Icons.Action");
                    item.Header = label;
                    item.Click += async (_, e) =>
                    {
                        await repo.ExecCustomActionAsync(dup, target);
                        e.Handled = true;
                    };

                    custom.Items.Add(item);
                }

                menu.Items.Add(custom);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var copyPath = new MenuItem();
            copyPath.Header = App.Text("CopyPath");
            copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
            copyPath.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyPath.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(file.Path);
                ev.Handled = true;
            };

            var copyFullPath = new MenuItem();
            copyFullPath.Header = App.Text("CopyFullPath");
            copyFullPath.Icon = this.CreateMenuIcon("Icons.Copy");
            copyFullPath.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+C" : "Ctrl+Shift+C";
            copyFullPath.Click += async (_, e) =>
            {
                await this.CopyTextAsync(fullPath);
                e.Handled = true;
            };

            menu.Items.Add(copyPath);
            menu.Items.Add(copyFullPath);
            return menu;
        }

        private List<ViewModels.RevisionFileTreeNode> _tree = [];
        private bool _disableSelectionChangingEvent = false;
        private List<ViewModels.RevisionFileTreeNode> _searchResult = [];
        private bool _isReloadingTreeData = false;
    }
}
