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
    public class SubmoduleTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.SubmoduleTreeNode { IsFolder: true } node)
            {
                var view = this.FindAncestorOfType<SubmodulesView>();
                view?.ToggleNodeIsExpanded(node);
            }

            e.Handled = true;
        }
    }

    public class SubmoduleTreeNodeIcon : UserControl
    {
        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<SubmoduleTreeNodeIcon, bool>(nameof(IsExpanded));

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsExpandedProperty)
                UpdateContent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            UpdateContent();
        }

        private void UpdateContent()
        {
            if (DataContext is not ViewModels.SubmoduleTreeNode node)
            {
                Content = null;
                return;
            }

            if (node.Module != null)
                CreateContent(new Thickness(0, 0, 0, 0), "Icons.Submodule");
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

    public partial class SubmodulesView : UserControl
    {
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

        public SubmodulesView()
        {
            InitializeComponent();
        }

        public void ToggleNodeIsExpanded(ViewModels.SubmoduleTreeNode node)
        {
            if (Content is ViewModels.SubmoduleCollectionAsTree tree)
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
                if (Content is ViewModels.SubmoduleCollectionAsTree tree)
                    Rows = tree.Rows.Count;
                else if (Content is ViewModels.SubmoduleCollectionAsList list)
                    Rows = list.Submodules.Count;
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
            if (sender is Control control && DataContext is ViewModels.Repository repo)
            {
                if (control.DataContext is ViewModels.SubmoduleTreeNode node)
                {
                    if (node.IsFolder)
                        ToggleNodeIsExpanded(node);
                    else if (node.Module.Status != Models.SubmoduleStatus.NotInited)
                        repo.OpenSubmodule(node.Module.Path);
                }
                else if (control.DataContext is Models.Submodule m && m.Status != Models.SubmoduleStatus.NotInited)
                {
                    repo.OpenSubmodule(m.Path);
                }
            }

            e.Handled = true;
        }

        private void OnItemContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Control control && DataContext is ViewModels.Repository repo)
            {
                var submodule = control.DataContext switch
                {
                    ViewModels.SubmoduleTreeNode node => node.Module,
                    Models.Submodule m => m,
                    _ => null,
                };

                if (submodule != null)
                {
                    var open = new MenuItem();
                    open.Header = App.Text("Submodule.Open");
                    open.Icon = App.CreateMenuIcon("Icons.Folder.Open");
                    open.IsEnabled = submodule.Status != Models.SubmoduleStatus.NotInited;
                    open.Click += (_, ev) =>
                    {
                        repo.OpenSubmodule(submodule.Path);
                        ev.Handled = true;
                    };

                    var update = new MenuItem();
                    update.Header = App.Text("Submodule.Update");
                    update.Icon = App.CreateMenuIcon("Icons.Loading");
                    update.Click += (_, ev) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.UpdateSubmodules(repo, submodule));
                        ev.Handled = true;
                    };

                    var move = new MenuItem();
                    move.Header = App.Text("Submodule.Move");
                    move.Icon = App.CreateMenuIcon("Icons.MoveTo");
                    move.Click += (_, ev) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.MoveSubmodule(repo, submodule));
                        ev.Handled = true;
                    };

                    var setURL = new MenuItem();
                    setURL.Header = App.Text("Submodule.SetURL");
                    setURL.Icon = App.CreateMenuIcon("Icons.Edit");
                    setURL.Click += (_, ev) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.ChangeSubmoduleUrl(repo, submodule));
                        ev.Handled = true;
                    };

                    var setBranch = new MenuItem();
                    setBranch.Header = App.Text("Submodule.SetBranch");
                    setBranch.Icon = App.CreateMenuIcon("Icons.Track");
                    setBranch.Click += (_, ev) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.SetSubmoduleBranch(repo, submodule));
                        ev.Handled = true;
                    };

                    var deinit = new MenuItem();
                    deinit.Header = App.Text("Submodule.Deinit");
                    deinit.Icon = App.CreateMenuIcon("Icons.Undo");
                    deinit.IsEnabled = submodule.Status != Models.SubmoduleStatus.NotInited;
                    deinit.Click += (_, ev) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.DeinitSubmodule(repo, submodule.Path));
                        ev.Handled = true;
                    };

                    var rm = new MenuItem();
                    rm.Header = App.Text("Submodule.Remove");
                    rm.Icon = App.CreateMenuIcon("Icons.Clear");
                    rm.Click += (_, ev) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.DeleteSubmodule(repo, submodule.Path));
                        ev.Handled = true;
                    };

                    var histories = new MenuItem();
                    histories.Header = App.Text("Submodule.Histories");
                    histories.Icon = App.CreateMenuIcon("Icons.Histories");
                    histories.Click += (_, ev) =>
                    {
                        App.ShowWindow(new ViewModels.FileHistories(repo, submodule.Path));
                        ev.Handled = true;
                    };

                    var copySHA = new MenuItem();
                    copySHA.Header = App.Text("CommitDetail.Info.SHA");
                    copySHA.Icon = App.CreateMenuIcon("Icons.Hash");
                    copySHA.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(submodule.SHA);
                        ev.Handled = true;
                    };

                    var copyBranch = new MenuItem();
                    copyBranch.Header = App.Text("Submodule.CopyBranch");
                    copyBranch.Icon = App.CreateMenuIcon("Icons.Branch");
                    copyBranch.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(submodule.Branch);
                        ev.Handled = true;
                    };

                    var copyRelativePath = new MenuItem();
                    copyRelativePath.Header = App.Text("Submodule.CopyPath");
                    copyRelativePath.Icon = App.CreateMenuIcon("Icons.Folder");
                    copyRelativePath.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(submodule.Path);
                        ev.Handled = true;
                    };

                    var copyURL = new MenuItem();
                    copyURL.Header = App.Text("Submodule.URL");
                    copyURL.Icon = App.CreateMenuIcon("Icons.Link");
                    copyURL.Click += async (_, ev) =>
                    {
                        await App.CopyTextAsync(submodule.URL);
                        ev.Handled = true;
                    };

                    var copy = new MenuItem();
                    copy.Header = App.Text("Copy");
                    copy.Icon = App.CreateMenuIcon("Icons.Copy");
                    copy.Items.Add(copySHA);
                    copy.Items.Add(copyBranch);
                    copy.Items.Add(copyRelativePath);
                    copy.Items.Add(copyURL);

                    var menu = new ContextMenu();
                    menu.Items.Add(open);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(update);
                    menu.Items.Add(setURL);
                    menu.Items.Add(setBranch);
                    menu.Items.Add(move);
                    menu.Items.Add(deinit);
                    menu.Items.Add(rm);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(histories);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(copy);
                    menu.Open(control);
                }
            }

            e.Handled = true;
        }
    }
}
