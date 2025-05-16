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
                if (control.DataContext is ViewModels.SubmoduleTreeNode node && node.Module != null)
                {
                    var menu = repo.CreateContextMenuForSubmodule(node.Module);
                    menu?.Open(control);
                }
                else if (control.DataContext is Models.Submodule m)
                {
                    var menu = repo.CreateContextMenuForSubmodule(m);
                    menu?.Open(control);
                }
            }

            e.Handled = true;
        }
    }
}
