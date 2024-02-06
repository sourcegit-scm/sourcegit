using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;

namespace SourceGit.Views {
    public class LayoutableGrid : Grid {
        public static readonly StyledProperty<bool> UseHorizontalProperty =
            AvaloniaProperty.Register<LayoutableGrid, bool>(nameof(UseHorizontal), false);

        public bool UseHorizontal {
            get => GetValue(UseHorizontalProperty);
            set => SetValue(UseHorizontalProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(Grid);

        static LayoutableGrid() {
            UseHorizontalProperty.Changed.AddClassHandler<LayoutableGrid>((o, _) => o.RefreshLayout());
        }

        public override void ApplyTemplate() {
            base.ApplyTemplate();
            RefreshLayout();
        }

        private void RefreshLayout() {
            if (UseHorizontal) {
                var rowSpan = RowDefinitions.Count;
                for (int i = 0; i < Children.Count; i++) {
                    var child = Children[i];
                    child.SetValue(RowProperty, 0);
                    child.SetValue(RowSpanProperty, rowSpan);
                    child.SetValue(ColumnProperty, i);
                    child.SetValue(ColumnSpanProperty, 1);
                }
            } else {
                var colSpan = ColumnDefinitions.Count;
                for (int i = 0; i < Children.Count; i++) {
                    var child = Children[i];
                    child.SetValue(RowProperty, i);
                    child.SetValue(RowSpanProperty, 1);
                    child.SetValue(ColumnProperty, 0);
                    child.SetValue(ColumnSpanProperty, colSpan);
                }
            }
        }
    }

    public class CommitGraph : Control {
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

        public Models.CommitGraph Graph {
            get => GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        public static readonly StyledProperty<DataGrid> BindingDataGridProperty =
            AvaloniaProperty.Register<CommitGraph, DataGrid>(nameof(BindingDataGrid));

        public DataGrid BindingDataGrid {
            get => GetValue(BindingDataGridProperty);
            set => SetValue(BindingDataGridProperty, value);
        }

        static CommitGraph() {
            AffectsMeasure<CommitGraph>(BindingDataGridProperty, GraphProperty);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property.Name == "ActualThemeVariant") {
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context) {
            base.Render(context);

            if (Graph == null || BindingDataGrid == null) return;

            // Apply scroll offset.
            var offset = BindingDataGrid.GetDisplayOffset();
            context.PushClip(new Rect(Bounds.Left, Bounds.Top, BindingDataGrid.Columns[0].ActualWidth, Bounds.Height));
            context.PushTransform(Matrix.CreateTranslation(0, -offset.Y));

            // Calculate bounds.
            var top = offset.Y;
            var bottom = offset.Y + BindingDataGrid.Bounds.Height + BindingDataGrid.RowHeight * 2;

            // Draw all curves
            DrawCurves(context, top, bottom);

            // Draw connect dots
            Brush dotFill = null;
            if (App.Current.TryGetResource("Brush.Contents", App.Current.ActualThemeVariant, out object res) && res is SolidColorBrush) {
                dotFill = res as SolidColorBrush;
            }
            foreach (var dot in Graph.Dots) {
                if (dot.Center.Y < top) continue;
                if (dot.Center.Y > bottom) break;

                context.DrawEllipse(dotFill, Pens[dot.Color], dot.Center, 3, 3);
            }
        }

        private void DrawCurves(DrawingContext context, double top, double bottom) {
            foreach (var line in Graph.Paths) {
                var last = line.Points[0];
                var size = line.Points.Count;

                if (line.Points[size - 1].Y < top) continue;
                if (last.Y > bottom) continue;

                var geo = new StreamGeometry();
                var pen = Pens[line.Color];
                using (var ctx = geo.Open()) {
                    var started = false;
                    var ended = false;
                    for (int i = 1; i < size; i++) {
                        var cur = line.Points[i];
                        if (cur.Y < top) {
                            last = cur;
                            continue;
                        }

                        if (!started) {
                            ctx.BeginFigure(last, false);
                            started = true;
                        }

                        if (cur.Y > bottom) {
                            cur = new Point(cur.X, bottom);
                            ended = true;
                        }

                        if (cur.X > last.X) {
                            ctx.QuadraticBezierTo(new Point(cur.X, last.Y), cur);
                        } else if (cur.X < last.X) {
                            if (i < size - 1) {
                                var midY = (last.Y + cur.Y) / 2;
                                var midX = (last.X + cur.X) / 2;
                                ctx.QuadraticBezierTo(new Point(last.X, midY), new Point(midX, midY));
                                ctx.QuadraticBezierTo(new Point(cur.X, midY), cur);
                            } else {
                                ctx.QuadraticBezierTo(new Point(last.X, cur.Y), cur);
                            }
                        } else {
                            ctx.LineTo(cur);
                        }

                        if (ended) break;
                        last = cur;
                    }
                }

                context.DrawGeometry(null, pen, geo);
            }

            foreach (var link in Graph.Links) {
                if (link.End.Y < top) continue;
                if (link.Start.Y > bottom) break;

                var geo = new StreamGeometry();
                using (var ctx = geo.Open()) {
                    ctx.BeginFigure(link.Start, false);
                    ctx.QuadraticBezierTo(link.Control, link.End);
                }

                context.DrawGeometry(null, Pens[link.Color], geo);
            }
        }
    }

    public partial class Histories : UserControl {
        public Histories() {
            InitializeComponent();    
        }

        protected override void OnUnloaded(RoutedEventArgs e) {
            base.OnUnloaded(e);
            GC.Collect();
        }

        private void OnCommitDataGridDisplayRegionChanged(object sender, RoutedEventArgs e) {
            commitGraph.InvalidateMeasure();
        }

        private void OnCommitDataGridSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (DataContext is ViewModels.Histories histories) {
                histories.Select(commitDataGrid.SelectedItems);

                if (histories.DetailContext is ViewModels.CommitDetail detail) {
                    commitDataGrid.ScrollIntoView(detail.Commit, null);
                }
            }
            e.Handled = true;
        }

        private void OnCommitDataGridContextRequested(object sender, ContextRequestedEventArgs e) {
            if (DataContext is ViewModels.Histories histories) {
                var menu = histories.MakeContextMenu();
                menu?.Open(sender as Control);
            }
            e.Handled = true;
        }
    }
}
