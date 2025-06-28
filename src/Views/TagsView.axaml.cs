using System;

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
        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<TagTreeNodeIcon, bool>(nameof(IsExpanded));

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            UpdateContent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsExpandedProperty)
                UpdateContent();
        }

        private void UpdateContent()
        {
            if (DataContext is not ViewModels.TagTreeNode node)
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
                tree.ToggleExpand(node);
                Rows = tree.Rows.Count;
                RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ContentProperty)
            {
                if (Content is ViewModels.TagCollectionAsTree tree)
                    Rows = tree.Rows.Count;
                else if (Content is ViewModels.TagCollectionAsList list)
                    Rows = list.Tags.Count;
                else
                    Rows = 0;

                RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            }
            else if (change.Property == IsVisibleProperty)
            {
                RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            }
        }

        private void OnItemDoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.TagTreeNode { IsFolder: true } node })
                ToggleNodeIsExpanded(node);

            e.Handled = true;
        }

        private void OnItemPointerPressed(object sender, PointerPressedEventArgs e)
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

        private void OnItemContextRequested(object sender, ContextRequestedEventArgs e)
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

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            var selected = (sender as ListBox)?.SelectedItem;
            Models.Tag selectedTag = null;
            if (selected is ViewModels.TagTreeNode node)
                selectedTag = node.Tag;
            else if (selected is Models.Tag tag)
                selectedTag = tag;

            if (selectedTag != null)
                RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not (Key.Delete or Key.Back))
                return;

            if (DataContext is not ViewModels.Repository repo)
                return;

            var selected = (sender as ListBox)?.SelectedItem;
            if (selected is ViewModels.TagTreeNode { Tag: { } tagInNode })
                repo.DeleteTag(tagInNode);
            else if (selected is Models.Tag tag)
                repo.DeleteTag(tag);

            e.Handled = true;
        }
    }
}
