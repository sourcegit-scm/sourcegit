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
                CreateContent(new Thickness(0, 0, 0, 0), "Icons.Tag", node.ToolTip is { IsAnnotated: false });
            else if (node.IsExpanded)
                CreateContent(new Thickness(0, 2, 0, 0), "Icons.Folder.Open", false);
            else
                CreateContent(new Thickness(0, 2, 0, 0), "Icons.Folder", false);
        }

        private void CreateContent(Thickness margin, string iconKey, bool stroke)
        {
            if (this.FindResource(iconKey) is not StreamGeometry geo)
                return;

            var path = new Avalonia.Controls.Shapes.Path()
            {
                Width = 12,
                Height = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin,
                Data = geo,
            };

            if (stroke)
            {
                path.Fill = Brushes.Transparent;
                path.Stroke = this.FindResource("Brush.FG1") as IBrush;
                path.StrokeThickness = 1;
            }

            Content = path;
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
                    Rows = list.TagItems.Count;
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
            else if (sender is Control { DataContext: ViewModels.TagListItem item })
            {
                if (DataContext is ViewModels.Repository repo)
                    await repo.CheckoutTagAsync(item.Tag);
            }

            e.Handled = true;
        }

        private void OnItemPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var ctrl = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (e.KeyModifiers.HasFlag(ctrl) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                return;

            var p = e.GetCurrentPoint(this);
            if (!p.Properties.IsLeftButtonPressed)
                return;

            if (DataContext is not ViewModels.Repository repo)
                return;

            if (sender is not Control control)
                return;

            if (control.DataContext is ViewModels.TagListItem { Tag: { } itemTag })
                repo.NavigateToCommit(itemTag.SHA);
            else if (control.DataContext is ViewModels.TagTreeNode { Tag: { } nodeTag })
                repo.NavigateToCommit(nodeTag.SHA);
        }

        private void OnTagsContextMenuRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is not ListBox { SelectedItems: { Count: > 0 } selectedItems } listBox)
                return;

            if (DataContext is not ViewModels.Repository repo)
                return;

            var selected = new List<Models.Tag>();
            foreach (var item in selectedItems)
            {
                if (item is ViewModels.TagListItem i)
                    selected.Add(i.Tag);
                else if (item is ViewModels.TagTreeNode n)
                    CollectTagsInNode(n, selected);
            }

            if (selected.Count == 1)
            {
                var tag = selected[0];

                var createBranch = new MenuItem();
                createBranch.Icon = App.CreateMenuIcon("Icons.Branch.Add");
                createBranch.Header = App.Text("CreateBranch");
                createBranch.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.CreateBranch(repo, tag));
                    ev.Handled = true;
                };

                var pushTag = new MenuItem();
                pushTag.Header = App.Text("TagCM.Push", tag.Name);
                pushTag.Icon = App.CreateMenuIcon("Icons.Push");
                pushTag.IsEnabled = repo.Remotes.Count > 0;
                pushTag.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.PushTag(repo, tag));
                    ev.Handled = true;
                };

                var deleteTag = new MenuItem();
                deleteTag.Header = App.Text("TagCM.Delete", tag.Name);
                deleteTag.Icon = App.CreateMenuIcon("Icons.Clear");
                deleteTag.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.DeleteTag(repo, tag));
                    ev.Handled = true;
                };

                var compareWithHead = new MenuItem();
                compareWithHead.Header = App.Text("TagCM.CompareWithHead");
                compareWithHead.Icon = App.CreateMenuIcon("Icons.Compare");
                compareWithHead.Click += (_, _) =>
                {
                    App.ShowWindow(new ViewModels.Compare(repo.FullPath, tag, repo.CurrentBranch));
                };

                var compareWith = new MenuItem();
                compareWith.Header = App.Text("TagCM.CompareWith");
                compareWith.Icon = App.CreateMenuIcon("Icons.Compare");
                compareWith.Click += (_, _) =>
                {
                    var launcher = App.GetLauncher();
                    if (launcher != null)
                        launcher.OpenCommandPalette(new ViewModels.CompareCommandPalette(launcher, repo, tag));
                };

                var archive = new MenuItem();
                archive.Icon = App.CreateMenuIcon("Icons.Archive");
                archive.Header = App.Text("Archive");
                archive.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Archive(repo, tag));
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Items.Add(createBranch);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(pushTag);
                menu.Items.Add(deleteTag);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(compareWithHead);
                menu.Items.Add(compareWith);
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
                        item.Click += async (_, ev) =>
                        {
                            await repo.ExecCustomActionAsync(dup, tag);
                            ev.Handled = true;
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
                    await App.CopyTextAsync(tag.Name);
                    ev.Handled = true;
                };

                var copyMessage = new MenuItem();
                copyMessage.Header = App.Text("TagCM.Copy.Message");
                copyMessage.Icon = App.CreateMenuIcon("Icons.Info");
                copyMessage.IsEnabled = !string.IsNullOrEmpty(tag.Message);
                copyMessage.Click += async (_, ev) =>
                {
                    await App.CopyTextAsync(tag.Message);
                    ev.Handled = true;
                };

                copy.Items.Add(copyName);
                copy.Items.Add(copyMessage);

                if (tag.Creator is { Email: { Length: > 0 } })
                {
                    var copyCreator = new MenuItem();
                    copyCreator.Header = App.Text("TagCM.Copy.Tagger");
                    copyCreator.Icon = App.CreateMenuIcon("Icons.User");
                    copyCreator.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(tag.Creator.ToString());
                        ev.Handled = true;
                    };
                    copy.Items.Add(copyCreator);
                }

                menu.Items.Add(copy);
                menu.Open(listBox);
            }
            else if (selected.Count > 0)
            {
                var menu = new ContextMenu();

                if (selected.Count == 2)
                {
                    var compare = new MenuItem();
                    compare.Header = App.Text("TagCM.CompareTwo");
                    compare.Icon = App.CreateMenuIcon("Icons.Compare");
                    compare.Click += (_, ev) =>
                    {
                        var (based, to) = (selected[0], selected[1]);
                        if (based.CreatorDate > to.CreatorDate)
                            (based, to) = (to, based);

                        App.ShowWindow(new ViewModels.Compare(repo.FullPath, based, to));
                        ev.Handled = true;
                    };
                    menu.Items.Add(compare);
                }

                var deleteMultiple = new MenuItem();
                deleteMultiple.Header = App.Text("TagCM.DeleteMultiple", selected.Count);
                deleteMultiple.Icon = App.CreateMenuIcon("Icons.Clear");
                deleteMultiple.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.DeleteMultipleTags(repo, selected));

                    ev.Handled = true;
                };

                menu.Items.Add(deleteMultiple);
                menu.Open(listBox);
            }

            e.Handled = true;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            if (sender is not ListBox listBox)
                return;

            if (listBox.SelectedItems is { Count: 0 })
            {
                if (Content is ViewModels.TagCollectionAsList list)
                    list.ClearSelection();
                else if (Content is ViewModels.TagCollectionAsTree tree)
                    tree.ClearSelection();
            }
            else if (listBox.SelectedItems is { Count: > 0 })
            {
                if (Content is ViewModels.TagCollectionAsList list)
                    list.UpdateSelection(listBox.SelectedItems);
                else if (Content is ViewModels.TagCollectionAsTree tree)
                    tree.UpdateSelection(listBox.SelectedItems);

                RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
            }
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
            else if (selected is ViewModels.TagListItem { Tag: { } tagInItem })
                repo.DeleteTag(tagInItem);

            e.Handled = true;
        }

        private void CollectTagsInNode(ViewModels.TagTreeNode node, List<Models.Tag> outs)
        {
            if (node.Tag is { } tag)
            {
                if (!outs.Contains(tag))
                    outs.Add(tag);

                return;
            }

            foreach (var child in node.Children)
                CollectTagsInNode(child, outs);
        }
    }
}
