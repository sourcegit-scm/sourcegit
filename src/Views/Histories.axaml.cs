using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class LayoutableGrid : Grid
    {
        public static readonly StyledProperty<bool> UseHorizontalProperty =
            AvaloniaProperty.Register<LayoutableGrid, bool>(nameof(UseHorizontal), false);

        public bool UseHorizontal
        {
            get => GetValue(UseHorizontalProperty);
            set => SetValue(UseHorizontalProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(Grid);

        static LayoutableGrid()
        {
            UseHorizontalProperty.Changed.AddClassHandler<LayoutableGrid>((o, _) => o.RefreshLayout());
        }

        public override void ApplyTemplate()
        {
            base.ApplyTemplate();
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (UseHorizontal)
            {
                var rowSpan = RowDefinitions.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.SetValue(RowProperty, 0);
                    child.SetValue(RowSpanProperty, rowSpan);
                    child.SetValue(ColumnProperty, i);
                    child.SetValue(ColumnSpanProperty, 1);

                    if (child is GridSplitter splitter)
                        splitter.BorderThickness = new Thickness(1, 0, 0, 0);
                }
            }
            else
            {
                var colSpan = ColumnDefinitions.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.SetValue(RowProperty, i);
                    child.SetValue(RowSpanProperty, 1);
                    child.SetValue(ColumnProperty, 0);
                    child.SetValue(ColumnSpanProperty, colSpan);

                    if (child is GridSplitter splitter)
                        splitter.BorderThickness = new Thickness(0, 1, 0, 0);
                }
            }
        }
    }

    public class CommitGraph : Control
    {
        public static readonly Pen[] Pens = [
            new Pen(Brushes.Orange, 2),
            new Pen(Brushes.ForestGreen, 2),
            new Pen(Brushes.Gold, 2),
            new Pen(Brushes.Magenta, 2),
            new Pen(Brushes.Red, 2),
            new Pen(Brushes.Gray, 2),
            new Pen(Brushes.Turquoise, 2),
            new Pen(Brushes.Olive, 2),
        ];

        public static readonly StyledProperty<Models.CommitGraph> GraphProperty =
            AvaloniaProperty.Register<CommitGraph, Models.CommitGraph>(nameof(Graph));

        public Models.CommitGraph Graph
        {
            get => GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        public static readonly StyledProperty<IBrush> DotBrushProperty =
            AvaloniaProperty.Register<CommitGraph, IBrush>(nameof(DotBrush), Brushes.Transparent);

        public IBrush DotBrush
        {
            get => GetValue(DotBrushProperty);
            set => SetValue(DotBrushProperty, value);
        }

        static CommitGraph()
        {
            AffectsRender<CommitGraph>(GraphProperty, DotBrushProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var parent = this.FindAncestorOfType<Histories>();
            var graph = Graph;
            var grid = parent.commitDataGrid;
            if (graph == null || grid == null)
                return;

            var rowsPresenter = grid.FindDescendantOfType<DataGridRowsPresenter>();
            if (rowsPresenter == null)
                return;

            // Find the content display offset Y of binding DataGrid.
            double rowHeight = grid.RowHeight;
            double startY = 0;
            foreach (var child in rowsPresenter.Children)
            {
                var row = child as DataGridRow;
                if (row.IsVisible && row.Bounds.Top <= 0 && row.Bounds.Top > -rowHeight)
                {
                    var test = rowHeight * row.GetIndex() - row.Bounds.Top;
                    if (startY < test)
                        startY = test;
                }
            }

            // Apply scroll offset.
            context.PushClip(new Rect(Bounds.Left, Bounds.Top, grid.Columns[0].ActualWidth, Bounds.Height));
            context.PushTransform(Matrix.CreateTranslation(0, -startY));

            // Calculate bounds.
            var top = startY;
            var bottom = startY + grid.Bounds.Height + rowHeight * 2;

            // Draw all curves
            DrawCurves(context, top, bottom);

            // Draw connect dots
            IBrush dotFill = DotBrush;
            foreach (var dot in graph.Dots)
            {
                if (dot.Center.Y < top)
                    continue;
                if (dot.Center.Y > bottom)
                    break;

                context.DrawEllipse(dotFill, Pens[dot.Color], dot.Center, 3, 3);
            }
        }

        private void DrawCurves(DrawingContext context, double top, double bottom)
        {
            foreach (var line in Graph.Paths)
            {
                var last = line.Points[0];
                var size = line.Points.Count;

                if (line.Points[size - 1].Y < top)
                    continue;
                if (last.Y > bottom)
                    continue;

                var geo = new StreamGeometry();
                var pen = Pens[line.Color];
                using (var ctx = geo.Open())
                {
                    var started = false;
                    var ended = false;
                    for (int i = 1; i < size; i++)
                    {
                        var cur = line.Points[i];
                        if (cur.Y < top)
                        {
                            last = cur;
                            continue;
                        }

                        if (!started)
                        {
                            ctx.BeginFigure(last, false);
                            started = true;
                        }

                        if (cur.Y > bottom)
                        {
                            cur = new Point(cur.X, bottom);
                            ended = true;
                        }

                        if (cur.X > last.X)
                        {
                            ctx.QuadraticBezierTo(new Point(cur.X, last.Y), cur);
                        }
                        else if (cur.X < last.X)
                        {
                            if (i < size - 1)
                            {
                                var midY = (last.Y + cur.Y) / 2;
                                ctx.CubicBezierTo(new Point(last.X, midY + 2), new Point(cur.X, midY - 2), cur);
                            }
                            else
                            {
                                ctx.QuadraticBezierTo(new Point(last.X, cur.Y), cur);
                            }
                        }
                        else
                        {
                            ctx.LineTo(cur);
                        }

                        if (ended)
                            break;
                        last = cur;
                    }
                }

                context.DrawGeometry(null, pen, geo);
            }

            foreach (var link in Graph.Links)
            {
                if (link.End.Y < top)
                    continue;
                if (link.Start.Y > bottom)
                    break;

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(link.Start, false);
                    ctx.QuadraticBezierTo(link.Control, link.End);
                }

                context.DrawGeometry(null, Pens[link.Color], geo);
            }
        }
    }

    public partial class Histories : UserControl
    {
        public static readonly StyledProperty<long> NavigationIdProperty =
            AvaloniaProperty.Register<Histories, long>(nameof(NavigationId), 0);

        public long NavigationId
        {
            get => GetValue(NavigationIdProperty);
            set => SetValue(NavigationIdProperty, value);
        }

        static Histories()
        {
            NavigationIdProperty.Changed.AddClassHandler<Histories>((h, _) =>
            {
                // Force scroll selected item (current head) into view. see issue #58
                var datagrid = h.commitDataGrid;
                if (datagrid != null && datagrid.SelectedItems.Count == 1)
                    datagrid.ScrollIntoView(datagrid.SelectedItems[0], null);
            });
        }

        public Histories()
        {
            InitializeComponent();
        }

        private void OnCommitDataGridLayoutUpdated(object sender, EventArgs e)
        {
            commitGraph.InvalidateVisual();
        }

        private void OnCommitDataGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories)
            {
                histories.Select(commitDataGrid.SelectedItems);
            }
            e.Handled = true;
        }

        private void OnCommitDataGridContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories && sender is DataGrid datagrid)
            {
                var menu = histories.MakeContextMenu(datagrid);
                datagrid.OpenContextMenu(menu);
            }
            e.Handled = true;
        }
    }
}
