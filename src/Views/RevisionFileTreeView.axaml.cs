using System;
using System.Collections.Generic;
using System.IO;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Views
{
    public class RevisionFileTreeNode : ObservableObject
    {
        public Models.Object Backend { get; set; } = null;
        public int Depth { get; set; } = 0;
        public List<RevisionFileTreeNode> Children { get; set; } = new List<RevisionFileTreeNode>();

        public string Name
        {
            get => Backend == null ? string.Empty : Path.GetFileName(Backend.Path);
        }

        public bool IsFolder
        {
            get => Backend != null && Backend.Type == Models.ObjectType.Tree;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isExpanded = false;
    }

    public class RevisionFileTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is RevisionFileTreeNode { IsFolder: true } node)
            {
                var tree = this.FindAncestorOfType<RevisionFileTreeView>();
                tree.ToggleNodeIsExpanded(node);
            }

            e.Handled = true;
        }
    }

    public class RevisionTreeNodeIcon : UserControl
    {
        public static readonly StyledProperty<RevisionFileTreeNode> NodeProperty =
            AvaloniaProperty.Register<RevisionTreeNodeIcon, RevisionFileTreeNode>(nameof(Node));

        public RevisionFileTreeNode Node
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
            if (node == null || node.Backend == null)
            {
                Content = null;
                return;
            }

            var obj = node.Backend;
            if (obj.Type == Models.ObjectType.Blob)
            {
                CreateContent(14, new Thickness(0, 0, 0, 0), "Icons.File");
            }
            else if (obj.Type == Models.ObjectType.Commit)
            {
                CreateContent(14, new Thickness(0, 0, 0, 0), "Icons.Submodule");
            }
            else
            {
                if (node.IsExpanded)
                    CreateContent(14, new Thickness(0, 2, 0, 0), "Icons.Folder.Open", Brushes.Goldenrod);
                else
                    CreateContent(14, new Thickness(0, 2, 0, 0), "Icons.Folder.Fill", Brushes.Goldenrod);
            }
        }

        private void CreateContent(double size, Thickness margin, string iconKey, IBrush fill = null)
        {
            var geo = this.FindResource(iconKey) as StreamGeometry;
            if (geo == null)
                return;

            var icon = new Avalonia.Controls.Shapes.Path()
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin,
                Data = geo,
            };

            if (fill != null)
                icon.Fill = fill;

            Content = icon;
        }
    }

    public partial class RevisionFileTreeView : UserControl
    {
        public static readonly StyledProperty<string> RevisionProperty =
            AvaloniaProperty.Register<RevisionFileTreeView, string>(nameof(Revision), null);

        public string Revision
        {
            get => GetValue(RevisionProperty);
            set => SetValue(RevisionProperty, value);
        }

        public AvaloniaList<RevisionFileTreeNode> Rows
        {
            get => _rows;
        }

        public RevisionFileTreeView()
        {
            InitializeComponent();
        }

        public void ToggleNodeIsExpanded(RevisionFileTreeNode node)
        {
            _disableSelectionChangingEvent = true;
            node.IsExpanded = !node.IsExpanded;

            var depth = node.Depth;
            var idx = _rows.IndexOf(node);
            if (idx == -1)
                return;

            if (node.IsExpanded)
            {
                var subtree = GetChildrenOfTreeNode(node);
                if (subtree != null && subtree.Count > 0)
                {
                    var subrows = new List<RevisionFileTreeNode>();
                    MakeRows(subrows, subtree, depth + 1);
                    _rows.InsertRange(idx + 1, subrows);
                }
            }
            else
            {
                var removeCount = 0;
                for (int i = idx + 1; i < _rows.Count; i++)
                {
                    var row = _rows[i];
                    if (row.Depth <= depth)
                        break;

                    removeCount++;
                }
                _rows.RemoveRange(idx + 1, removeCount);
            }

            _disableSelectionChangingEvent = false;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == RevisionProperty)
            {
                _tree.Clear();
                _rows.Clear();

                var vm = DataContext as ViewModels.CommitDetail;
                if (vm == null || vm.Commit == null)
                {
                    GC.Collect();
                    return;
                }

                var objects = vm.GetRevisionFilesUnderFolder(null);
                if (objects == null || objects.Count == 0)
                {
                    GC.Collect();
                    return;
                }

                foreach (var obj in objects)
                    _tree.Add(new RevisionFileTreeNode { Backend = obj });

                _tree.Sort((l, r) =>
                {
                    if (l.IsFolder == r.IsFolder)
                        return l.Name.CompareTo(r.Name);
                    return l.IsFolder ? -1 : 1;
                });

                var topTree = new List<RevisionFileTreeNode>();
                MakeRows(topTree, _tree, 0);
                _rows.AddRange(topTree);
                GC.Collect();
            }
        }

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.CommitDetail vm && sender is Grid { DataContext: RevisionFileTreeNode { Backend: Models.Object obj } } grid)
            {
                if (obj.Type != Models.ObjectType.Tree)
                {
                    var menu = vm.CreateRevisionFileContextMenu(obj);
                    grid.OpenContextMenu(menu);
                }
            }

            e.Handled = true;
        }

        private void OnTreeNodeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Grid { DataContext: RevisionFileTreeNode { IsFolder: true } node })
            {
                var posX = e.GetPosition(this).X;
                if (posX < node.Depth * 16 + 16)
                    return;

                ToggleNodeIsExpanded(node);
            }                
        }

        private void OnRowsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_disableSelectionChangingEvent)
                return;

            if (sender is ListBox list && DataContext is ViewModels.CommitDetail vm)
            {
                var node = list.SelectedItem as RevisionFileTreeNode;
                if (node != null && !node.IsFolder)
                    vm.ViewRevisionFile(node.Backend);
                else
                    vm.ViewRevisionFile(null);
            }
        }

        private List<RevisionFileTreeNode> GetChildrenOfTreeNode(RevisionFileTreeNode node)
        {
            if (!node.IsFolder)
                return null;

            if (node.Children.Count > 0)
                return node.Children;

            var vm = DataContext as ViewModels.CommitDetail;
            if (vm == null)
                return null;

            var objects = vm.GetRevisionFilesUnderFolder(node.Backend.Path + "/");
            if (objects == null || objects.Count == 0)
                return null;

            foreach (var obj in objects)
                node.Children.Add(new RevisionFileTreeNode() { Backend = obj });

            node.Children.Sort((l, r) =>
            {
                if (l.IsFolder == r.IsFolder)
                    return l.Name.CompareTo(r.Name);
                return l.IsFolder ? -1 : 1;
            });

            return node.Children;
        }

        private void MakeRows(List<RevisionFileTreeNode> rows, List<RevisionFileTreeNode> nodes, int depth)
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

        private List<RevisionFileTreeNode> _tree = new List<RevisionFileTreeNode>();
        private AvaloniaList<RevisionFileTreeNode> _rows = new AvaloniaList<RevisionFileTreeNode>();
        private bool _disableSelectionChangingEvent = false;
    }
}
