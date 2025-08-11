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
            if (this.FindResource(iconKey) is not StreamGeometry geo)
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

        private async void OnItemDoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.TagTreeNode node })
            {
                if (node.IsFolder)
                    ToggleNodeIsExpanded(node);
                else if (DataContext is ViewModels.Repository repo)
                    await repo.CheckoutTagAsync(node.Tag);
            }
            else if (sender is Control { DataContext: Models.Tag tag })
            {
                if (DataContext is ViewModels.Repository repo)
                    await repo.CheckoutTagAsync(tag);
            }

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
            if (sender is not Control control)
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
                var createBranch = new MenuItem();
                createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
                createBranch.Header = App.Text("CreateBranch");
                createBranch.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.CreateBranch(repo, selected));
                    ev.Handled = true;
                };

                var pushTag = new MenuItem();
                pushTag.Header = App.Text("TagCM.Push", selected.Name);
                pushTag.Icon = App.CreateMenuIcon("Icons.Push");
                pushTag.IsEnabled = repo.Remotes.Count > 0;
                pushTag.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.PushTag(repo, selected));
                    ev.Handled = true;
                };

                var deleteTag = new MenuItem();
                deleteTag.Header = App.Text("TagCM.Delete", selected.Name);
                deleteTag.Icon = App.CreateMenuIcon("Icons.Clear");
                deleteTag.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.DeleteTag(repo, selected));
                    ev.Handled = true;
                };

                var archive = new MenuItem();
                archive.Icon = App.CreateMenuIcon("Icons.Archive");
                archive.Header = App.Text("Archive");
                archive.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Archive(repo, selected));
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(createBranch);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(pushTag);
                menu.Items.Add(deleteTag);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(archive);
                menu.Items.Add(new MenuItem() { Header = "-" });

                var actions = repo.GetCustomActions(Models.CustomActionScope.Tag);
                if (actions.Count > 0)
                {
                    var custom = new MenuItem();
                    custom.Header = App.Text("TagCM.CustomAction");
                    custom.Icon = App.CreateMenuIcon("Icons.Action");

                    foreach (var action in actions)
                    {
                        var (dup, label) = action;
                        var item = new MenuItem();
                        item.Icon = App.CreateMenuIcon("Icons.Action");
                        item.Header = label;
                        item.Click += (_, e) =>
                        {
                            repo.ExecCustomAction(dup, selected);
                            e.Handled = true;
                        };

                        custom.Items.Add(item);
                    }

                    menu.Items.Add(custom);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var copy = new MenuItem();
                copy.Header = App.Text("Copy");
                copy.Icon = App.CreateMenuIcon("Icons.Copy");

                var copyName = new MenuItem();
                copyName.Header = App.Text("TagCM.Copy.Name");
                copyName.Icon = App.CreateMenuIcon("Icons.Tag");
                copyName.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(selected.Name);
                    ev.Handled = true;
                };

                var copyMessage = new MenuItem();
                copyMessage.Header = App.Text("TagCM.Copy.Message");
                copyMessage.Icon = App.CreateMenuIcon("Icons.Info");
                copyMessage.IsEnabled = !string.IsNullOrEmpty(selected.Message);
                copyMessage.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(selected.Message);
                    ev.Handled = true;
                };

                copy.Items.Add(copyName);
                copy.Items.Add(copyMessage);

                if (selected.Creator is { Email: { Length: > 0 } })
                {
                    var copyCreator = new MenuItem();
                    copyCreator.Header = App.Text("TagCM.Copy.Tagger");
                    copyCreator.Icon = App.CreateMenuIcon("Icons.User");
                    copyCreator.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(selected.Creator.ToString());
                        ev.Handled = true;
                    };
                    copy.Items.Add(copyCreator);
                }

                menu.Items.Add(copy);
                menu.Open(control);
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
