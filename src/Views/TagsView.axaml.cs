using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class TagTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.TagTreeNode { IsFolder: true } node)
            {
                var view = this.FindAncestorOfType<TagsView>();
                view?.ToggleNodeIsExpanded(node);
            }

            e.Handled = true;
        }
    }

    public class TagTreeNodeIcon : UserControl
    {
        public static readonly StyledProperty<ViewModels.TagTreeNode> NodeProperty =
            AvaloniaProperty.Register<TagTreeNodeIcon, ViewModels.TagTreeNode>(nameof(Node));

        public ViewModels.TagTreeNode Node
        {
            get => GetValue(NodeProperty);
            set => SetValue(NodeProperty, value);
        }

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<TagTreeNodeIcon, bool>(nameof(IsExpanded));

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        static TagTreeNodeIcon()
        {
            NodeProperty.Changed.AddClassHandler<TagTreeNodeIcon>((icon, _) => icon.UpdateContent());
            IsExpandedProperty.Changed.AddClassHandler<TagTreeNodeIcon>((icon, _) => icon.UpdateContent());
        }

        private void UpdateContent()
        {
            var node = Node;
            if (node == null)
            {
                Content = null;
                return;
            }

            if (node.Tag != null)
                CreateContent(new Thickness(0, 0, 0, 0), "Icons.Tag");
            else if (node.IsExpanded)
                CreateContent(new Thickness(0, 2, 0, 0), "Icons.Folder.Open");
            else
                CreateContent(new Thickness(0, 2, 0, 0), "Icons.Folder");
        }

        private void CreateContent(Thickness margin, string iconKey)
        {
            var geo = this.FindResource(iconKey) as StreamGeometry;
            if (geo == null)
                return;

            Content = new Avalonia.Controls.Shapes.Path()
            {
                Width = 12,
                Height = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin,
                Data = geo,
            };
        }
    }

    public partial class TagsView : UserControl
    {
        public static readonly StyledProperty<bool> ShowTagsAsTreeProperty =
            AvaloniaProperty.Register<TagsView, bool>(nameof(ShowTagsAsTree));

        public bool ShowTagsAsTree
        {
            get => GetValue(ShowTagsAsTreeProperty);
            set => SetValue(ShowTagsAsTreeProperty, value);
        }

        public static readonly StyledProperty<List<Models.Tag>> TagsProperty =
            AvaloniaProperty.Register<TagsView, List<Models.Tag>>(nameof(Tags));

        public List<Models.Tag> Tags
        {
            get => GetValue(TagsProperty);
            set => SetValue(TagsProperty, value);
        }

        public static readonly RoutedEvent<RoutedEventArgs> SelectionChangedEvent =
            RoutedEvent.Register<TagsView, RoutedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> SelectionChanged
        {
            add { AddHandler(SelectionChangedEvent, value); }
            remove { RemoveHandler(SelectionChangedEvent, value); }
        }

        public static readonly RoutedEvent<RoutedEventArgs> RowsChangedEvent =
            RoutedEvent.Register<TagsView, RoutedEventArgs>(nameof(RowsChanged), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> RowsChanged
        {
            add { AddHandler(RowsChangedEvent, value); }
            remove { RemoveHandler(RowsChangedEvent, value); }
        }

        public int Rows
        {
            get;
            private set;
        }

        public TagsView()
        {
            InitializeComponent();
        }

        public void UnselectAll()
        {
            var list = this.FindDescendantOfType<ListBox>();
            if (list != null)
                list.SelectedItem = null;
        }

        public void ToggleNodeIsExpanded(ViewModels.TagTreeNode node)
        {
            if (Content is ViewModels.TagCollectionAsTree tree)
            {
                node.IsExpanded = !node.IsExpanded;

                var depth = node.Depth;
                var idx = tree.Rows.IndexOf(node);
                if (idx == -1)
                    return;

                if (node.IsExpanded)
                {
                    var subrows = new List<ViewModels.TagTreeNode>();
                    MakeTreeRows(subrows, node.Children);
                    tree.Rows.InsertRange(idx + 1, subrows);
                }
                else
                {
                    var removeCount = 0;
                    for (int i = idx + 1; i < tree.Rows.Count; i++)
                    {
                        var row = tree.Rows[i];
                        if (row.Depth <= depth)
                            break;

                        removeCount++;
                    }
                    tree.Rows.RemoveRange(idx + 1, removeCount);
                }

                Rows = tree.Rows.Count;
                RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ShowTagsAsTreeProperty || change.Property == TagsProperty)
            {
                UpdateDataSource();
                RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            }
            else if (change.Property == IsVisibleProperty)
            {
                RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            }
        }

        private void OnDoubleTappedNode(object sender, TappedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.TagTreeNode { IsFolder: true } node })
                ToggleNodeIsExpanded(node);

            e.Handled = true;
        }

        private void OnRowPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var p = e.GetCurrentPoint(this);
            if (!p.Properties.IsLeftButtonPressed)
                return;

            if (DataContext is not ViewModels.Repository repo)
                return;

            if (sender is Control { DataContext: Models.Tag tag })
                repo.NavigateToCommit(tag.SHA);
            else if (sender is Control { DataContext: ViewModels.TagTreeNode { Tag: { } nodeTag } })
                repo.NavigateToCommit(nodeTag.SHA);
        }

        private void OnRowContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var control = sender as Control;
            if (control == null)
                return;

            Models.Tag selected;
            if (control.DataContext is ViewModels.TagTreeNode node)
                selected = node.Tag;
            else if (control.DataContext is Models.Tag tag)
                selected = tag;
            else
                selected = null;

            if (selected != null && DataContext is ViewModels.Repository repo)
            {
                var menu = repo.CreateContextMenuForTag(selected);
                menu?.Open(control);
            }

            e.Handled = true;
        }

        private void OnRowSelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            var selected = (sender as ListBox)?.SelectedItem;
            var selectedTag = null as Models.Tag;
            if (selected is ViewModels.TagTreeNode node)
                selectedTag = node.Tag;
            else if (selected is Models.Tag tag)
                selectedTag = tag;

            if (selectedTag != null)
                RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }

        private void MakeTreeRows(List<ViewModels.TagTreeNode> rows, List<ViewModels.TagTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                rows.Add(node);

                if (!node.IsExpanded || !node.IsFolder)
                    continue;

                MakeTreeRows(rows, node.Children);
            }
        }

        private void UpdateDataSource()
        {
            var tags = Tags;
            if (tags == null || tags.Count == 0)
            {
                Rows = 0;
                Content = null;
                return;
            }

            if (ShowTagsAsTree)
            {
                var oldExpanded = new HashSet<string>();
                if (Content is ViewModels.TagCollectionAsTree oldTree)
                {
                    foreach (var row in oldTree.Rows)
                    {
                        if (row.IsFolder && row.IsExpanded)
                            oldExpanded.Add(row.FullPath);
                    }
                }

                var tree = new ViewModels.TagCollectionAsTree();
                tree.Tree = ViewModels.TagTreeNode.Build(tags, oldExpanded);

                var rows = new List<ViewModels.TagTreeNode>();
                MakeTreeRows(rows, tree.Tree);
                tree.Rows.AddRange(rows);

                Content = tree;
                Rows = rows.Count;
            }
            else
            {
                var list = new ViewModels.TagCollectionAsList();
                list.Tags.AddRange(tags);

                Content = list;
                Rows = tags.Count;
            }

            RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
        }
    }
}

