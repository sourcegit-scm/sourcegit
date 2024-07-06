using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class BranchTreeNodeIcon : UserControl
    {
        public static readonly StyledProperty<ViewModels.BranchTreeNode> NodeProperty =
            AvaloniaProperty.Register<BranchTreeNodeIcon, ViewModels.BranchTreeNode>(nameof(Node), null);

        public ViewModels.BranchTreeNode Node
        {
            get => GetValue(NodeProperty);
            set => SetValue(NodeProperty, value);
        }
        
        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<BranchTreeNodeIcon, bool>(nameof(IsExpanded), false);

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        static BranchTreeNodeIcon()
        {
            NodeProperty.Changed.AddClassHandler<BranchTreeNodeIcon>((icon, e) => icon.UpdateContent());
            IsExpandedProperty.Changed.AddClassHandler<BranchTreeNodeIcon>((icon, e) => icon.UpdateContent());
        }

        private void UpdateContent()
        {
            var node = Node;
            if (node == null)
            {
                Content = null;
                return;
            }

            if (node.Backend is Models.Remote)
            {
                CreateContent(12, new Thickness(0,2,0,0), "Icons.Remote");
            }
            else if (node.Backend is Models.Branch branch)
            {
                if (branch.IsCurrent)
                    CreateContent(12, new Thickness(0,2,0,0), "Icons.Check");
                else
                    CreateContent(12, new Thickness(2,0,0,0), "Icons.Branch");
            }
            else
            {
                if (node.IsExpanded)
                    CreateContent(10, new Thickness(0,2,0,0), "Icons.Folder.Open");
                else
                    CreateContent(10, new Thickness(0,2,0,0), "Icons.Folder.Fill");
            }
        }

        private void CreateContent(double size, Thickness margin, string iconKey)
        {
            var geo = this.FindResource(iconKey) as StreamGeometry;
            if (geo == null)
                return;
            
            Content = new Path()
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin,
                Data = geo,
            };
        }
    }
    
    public partial class BranchTree : UserControl
    {
        public static readonly StyledProperty<List<ViewModels.BranchTreeNode>> NodesProperty =
            AvaloniaProperty.Register<BranchTree, List<ViewModels.BranchTreeNode>>(nameof(Nodes), null);

        public List<ViewModels.BranchTreeNode> Nodes
        {
            get => GetValue(NodesProperty);
            set => SetValue(NodesProperty, value);
        }

        public AvaloniaList<ViewModels.BranchTreeNode> Rows
        {
            get;
            private set;
        } = new AvaloniaList<ViewModels.BranchTreeNode>();
        
        public static readonly RoutedEvent<RoutedEventArgs> SelectionChangedEvent =
            RoutedEvent.Register<BranchTree, RoutedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> SelectionChanged
        {
            add { AddHandler(SelectionChangedEvent, value); }
            remove { RemoveHandler(SelectionChangedEvent, value); }
        }

        public BranchTree()
        {
            InitializeComponent();
        }

        public void UnselectAll()
        {
            BranchesPresenter.SelectedItem = null;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == NodesProperty)
            {
                Rows.Clear();

                if (Nodes is { Count: > 0 })
                {
                    var rows = new List<ViewModels.BranchTreeNode>();
                    MakeRows(rows, Nodes, 0);
                    Rows.AddRange(rows);
                }

                var repo = this.FindAncestorOfType<Repository>();
                repo?.UpdateLeftSidebarLayout();
            }
            else if (change.Property == IsVisibleProperty)
            {
                var repo = this.FindAncestorOfType<Repository>();
                repo?.UpdateLeftSidebarLayout();
            }
        }

        private void OnNodesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = BranchesPresenter.SelectedItems;
            if (selected == null || selected.Count == 0)
                return;

            foreach (var item in e.AddedItems)
            {
                if (item is ViewModels.BranchTreeNode node)
                    node.IsSelected = true;
            }

            foreach (var item in e.RemovedItems)
            {
                if (item is ViewModels.BranchTreeNode node)
                    node.IsSelected = false;
            }

            var prev = null as ViewModels.BranchTreeNode;
            foreach (var row in Rows)
            {
                if (row.IsSelected)
                {
                    if (prev is { IsSelected: true })
                    {
                        var prevTop = prev.CornerRadius.TopLeft;
                        prev.CornerRadius = new CornerRadius(prevTop, 0);
                        row.CornerRadius = new CornerRadius(0, 4);
                    }
                    else
                    {
                        row.CornerRadius = new CornerRadius(4);
                    }
                }

                prev = row;
            }
            
            RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }
        
        private void OnTreeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var repo = DataContext as ViewModels.Repository;
            if (repo?.Settings == null)
                return;
            
            var selected = BranchesPresenter.SelectedItems;
            if (selected == null || selected.Count == 0)
                return;

            if (selected.Count == 1 && selected[0] is ViewModels.BranchTreeNode { Backend: Models.Remote remote })
            {
                var menu = repo.CreateContextMenuForRemote(remote);
                this.OpenContextMenu(menu);
                return;
            }

            var branches = new List<Models.Branch>();
            foreach (var item in selected)
            {
                if (item is ViewModels.BranchTreeNode node)
                    CollectBranchesInNode(branches, node);
            }

            if (branches.Count == 1)
            {
                var branch = branches[0];
                var menu = branch.IsLocal ?
                    repo.CreateContextMenuForLocalBranch(branch) :
                    repo.CreateContextMenuForRemoteBranch(branch);
                this.OpenContextMenu(menu);
            }
            else if (branches.Find(x => x.IsCurrent) == null)
            {
                var menu = new ContextMenu();
                var deleteMulti = new MenuItem();
                deleteMulti.Header = App.Text("BranchCM.DeleteMultiBranches", branches.Count);
                deleteMulti.Icon = App.CreateMenuIcon("Icons.Clear");
                deleteMulti.Click += (_, ev) =>
                {
                    repo.DeleteMultipleBranches(branches, branches[0].IsLocal);
                    ev.Handled = true;
                };
                menu.Items.Add(deleteMulti);
                this.OpenContextMenu(menu);
            }
        }

        private void OnDoubleTappedBranchNode(object sender, TappedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.BranchTreeNode node })
            {
                if (node.Backend is Models.Branch branch)
                {
                    if (branch.IsCurrent)
                        return;

                    if (DataContext is ViewModels.Repository { Settings: not null } repo)
                        repo.CheckoutBranch(branch);
                }
                else
                {
                    node.IsExpanded = !node.IsExpanded;

                    var rows = Rows;
                    var depth = node.Depth;
                    var idx = rows.IndexOf(node);
                    if (idx == -1)
                        return;
                
                    if (node.IsExpanded)
                    {
                        var subtree = new List<ViewModels.BranchTreeNode>();
                        MakeRows(subtree, node.Children, depth + 1);
                        rows.InsertRange(idx + 1, subtree);
                    }
                    else
                    {
                        var removeCount = 0;
                        for (int i = idx + 1; i < rows.Count; i++)
                        {
                            var row = rows[i];
                            if (row.Depth <= depth)
                                break;

                            removeCount++;
                        }
                        rows.RemoveRange(idx + 1, removeCount);
                    }

                    var repo = this.FindAncestorOfType<Repository>();
                    repo?.UpdateLeftSidebarLayout();
                }
            }
        }
        
        private void OnToggleFilter(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && DataContext is ViewModels.Repository repo)
            {
                if (toggle.DataContext is ViewModels.BranchTreeNode { Backend: Models.Branch branch })
                    repo.UpdateFilter(branch.FullName, toggle.IsChecked == true);
            }

            e.Handled = true;
        }
        
        private void MakeRows(List<ViewModels.BranchTreeNode> rows, List<ViewModels.BranchTreeNode> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                node.Depth = depth;
                rows.Add(node);

                if (!node.IsExpanded || node.Backend is Models.Branch)
                    continue;
                
                MakeRows(rows, node.Children, depth + 1);
            }
        }

        private void CollectBranchesInNode(List<Models.Branch> outs, ViewModels.BranchTreeNode node)
        {
            if (node.Backend is Models.Branch branch && !outs.Contains(branch))
            {
                outs.Add(branch);
                return;
            }
            
            foreach (var sub in node.Children)
                CollectBranchesInNode(outs, sub);
        }
    }
}

