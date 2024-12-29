using System;
using System.Collections.Generic;
using System.Globalization;

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
            AvaloniaProperty.Register<BranchTreeNodeIcon, ViewModels.BranchTreeNode>(nameof(Node));

        public ViewModels.BranchTreeNode Node
        {
            get => GetValue(NodeProperty);
            set => SetValue(NodeProperty, value);
        }

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<BranchTreeNodeIcon, bool>(nameof(IsExpanded));

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        static BranchTreeNodeIcon()
        {
            NodeProperty.Changed.AddClassHandler<BranchTreeNodeIcon>((icon, _) => icon.UpdateContent());
            IsExpandedProperty.Changed.AddClassHandler<BranchTreeNodeIcon>((icon, _) => icon.UpdateContent());
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
                CreateContent(new Thickness(0, 0, 0, 0), "Icons.Remote");
            }
            else if (node.Backend is Models.Branch branch)
            {
                if (branch.IsCurrent)
                    CreateContent(new Thickness(0, 2, 0, 0), "Icons.Check");
                else
                    CreateContent(new Thickness(2, 0, 0, 0), "Icons.Branch");
            }
            else
            {
                if (node.IsExpanded)
                    CreateContent(new Thickness(0, 2, 0, 0), "Icons.Folder.Open");
                else
                    CreateContent(new Thickness(0, 2, 0, 0), "Icons.Folder");
            }
        }

        private void CreateContent(Thickness margin, string iconKey)
        {
            var geo = this.FindResource(iconKey) as StreamGeometry;
            if (geo == null)
                return;

            Content = new Path()
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

    public class BranchTreeNodeToggleButton : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                DataContext is ViewModels.BranchTreeNode { IsBranch: false } node)
            {
                var tree = this.FindAncestorOfType<BranchTree>();
                tree?.ToggleNodeIsExpanded(node);
            }

            e.Handled = true;
        }
    }

    public class BranchTreeNodeTrackStatusPresenter : Control
    {
        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<BranchTreeNodeTrackStatusPresenter>();

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           TextBlock.FontSizeProperty.AddOwner<BranchTreeNodeTrackStatusPresenter>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<BranchTreeNodeTrackStatusPresenter, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<BranchTreeNodeTrackStatusPresenter, IBrush>(nameof(Background), Brushes.White);

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        static BranchTreeNodeTrackStatusPresenter()
        {
            AffectsMeasure<BranchTreeNodeTrackStatusPresenter>(
                FontSizeProperty,
                FontFamilyProperty,
                ForegroundProperty);

            AffectsRender<BranchTreeNodeTrackStatusPresenter>(
                ForegroundProperty,
                BackgroundProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_label != null)
            {
                context.DrawRectangle(Background, null, new RoundedRect(new Rect(8, 0, _label.Width + 18, 18), new CornerRadius(9)));
                context.DrawText(_label, new Point(17, 9 - _label.Height * 0.5));
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateMeasure();
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _label = null;

            if (DataContext is ViewModels.BranchTreeNode { Backend: Models.Branch branch })
            {
                var status = branch.TrackStatus.ToString();
                if (!string.IsNullOrEmpty(status))
                {
                    _label = new FormattedText(
                        status,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(FontFamily),
                        FontSize,
                        Foreground);
                }
            }

            return _label != null ? new Size(_label.Width + 18 /* Padding */ + 16 /* Margin */, 18) : new Size(0, 0);
        }

        private FormattedText _label = null;
    }

    public partial class BranchTree : UserControl
    {
        public static readonly StyledProperty<List<ViewModels.BranchTreeNode>> NodesProperty =
            AvaloniaProperty.Register<BranchTree, List<ViewModels.BranchTreeNode>>(nameof(Nodes));

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

        public static readonly RoutedEvent<RoutedEventArgs> RowsChangedEvent =
            RoutedEvent.Register<BranchTree, RoutedEventArgs>(nameof(RowsChanged), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<RoutedEventArgs> RowsChanged
        {
            add { AddHandler(RowsChangedEvent, value); }
            remove { RemoveHandler(RowsChangedEvent, value); }
        }

        public BranchTree()
        {
            InitializeComponent();
        }

        public void UnselectAll()
        {
            BranchesPresenter.SelectedItem = null;
        }

        public void ToggleNodeIsExpanded(ViewModels.BranchTreeNode node)
        {
            _disableSelectionChangingEvent = true;
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

                    row.IsSelected = false;
                    removeCount++;
                }
                rows.RemoveRange(idx + 1, removeCount);
            }

            var repo = DataContext as ViewModels.Repository;
            repo?.UpdateBranchNodeIsExpanded(node);

            RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            _disableSelectionChangingEvent = false;
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            if (Bounds.Height >= 23.0)
                BranchesPresenter.Height = Bounds.Height;
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

                RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            }
            else if (change.Property == IsVisibleProperty)
            {
                RaiseEvent(new RoutedEventArgs(RowsChangedEvent));
            }
        }

        private void OnNodesSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            if (_disableSelectionChangingEvent)
                return;

            var repo = DataContext as ViewModels.Repository;
            if (repo?.Settings == null)
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

            var selected = BranchesPresenter.SelectedItems;
            if (selected == null || selected.Count == 0)
                return;

            if (selected.Count == 1 && selected[0] is ViewModels.BranchTreeNode { Backend: Models.Branch branch })
                repo.NavigateToCommit(branch.Head);

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

        private void OnTreeContextRequested(object _1, ContextRequestedEventArgs _2)
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
                menu?.Open(this);
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
                menu?.Open(this);
            }
            else if (branches.Find(x => x.IsCurrent) == null)
            {
                var menu = new ContextMenu();

                var mergeMulti = new MenuItem();
                mergeMulti.Header = App.Text("BranchCM.MergeMultiBranches", branches.Count);
                mergeMulti.Icon = App.CreateMenuIcon("Icons.Merge");
                mergeMulti.Click += (_, ev) =>
                {
                    repo.MergeMultipleBranches(branches);
                    ev.Handled = true;
                };
                menu.Items.Add(mergeMulti);
                menu.Items.Add(new MenuItem() { Header = "-" });

                var deleteMulti = new MenuItem();
                deleteMulti.Header = App.Text("BranchCM.DeleteMultiBranches", branches.Count);
                deleteMulti.Icon = App.CreateMenuIcon("Icons.Clear");
                deleteMulti.Click += (_, ev) =>
                {
                    repo.DeleteMultipleBranches(branches, branches[0].IsLocal);
                    ev.Handled = true;
                };
                menu.Items.Add(deleteMulti);

                menu?.Open(this);
            }
        }

        private void OnDoubleTappedBranchNode(object sender, TappedEventArgs _)
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
                    ToggleNodeIsExpanded(node);
                }
            }
        }

        private void MakeRows(List<ViewModels.BranchTreeNode> rows, List<ViewModels.BranchTreeNode> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                node.Depth = depth;
                node.IsSelected = false;
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

        private bool _disableSelectionChangingEvent = false;
    }
}

