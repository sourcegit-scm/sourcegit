using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class CommitGraph : Control
    {
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

        public static readonly StyledProperty<bool> OnlyHighlightCurrentBranchProperty =
            AvaloniaProperty.Register<CommitGraph, bool>(nameof(OnlyHighlightCurrentBranch), true);

        public bool OnlyHighlightCurrentBranch
        {
            get => GetValue(OnlyHighlightCurrentBranchProperty);
            set => SetValue(OnlyHighlightCurrentBranchProperty, value);
        }

        public static readonly StyledProperty<bool> HighlightSelectedLineageProperty =
            AvaloniaProperty.Register<CommitGraph, bool>(nameof(HighlightSelectedLineage), false);

        public bool HighlightSelectedLineage
        {
            get => GetValue(HighlightSelectedLineageProperty);
            set => SetValue(HighlightSelectedLineageProperty, value);
        }

        public static readonly StyledProperty<System.Collections.Generic.HashSet<int>> HoveredLineageCommitsProperty =
            AvaloniaProperty.Register<CommitGraph, System.Collections.Generic.HashSet<int>>(nameof(HoveredLineageCommits));

        public System.Collections.Generic.HashSet<int> HoveredLineageCommits
        {
            get => GetValue(HoveredLineageCommitsProperty);
            set => SetValue(HoveredLineageCommitsProperty, value);
        }

        public static readonly StyledProperty<long> HoveredCommitIndexProperty =
            AvaloniaProperty.Register<CommitGraph, long>(nameof(HoveredCommitIndex), -1);

        public long HoveredCommitIndex
        {
            get => GetValue(HoveredCommitIndexProperty);
            set => SetValue(HoveredCommitIndexProperty, value);
        }

        public static readonly StyledProperty<System.Collections.Generic.HashSet<int>> SelectedLineageCommitsProperty =
            AvaloniaProperty.Register<CommitGraph, System.Collections.Generic.HashSet<int>>(nameof(SelectedLineageCommits));

        public System.Collections.Generic.HashSet<int> SelectedLineageCommits
        {
            get => GetValue(SelectedLineageCommitsProperty);
            set => SetValue(SelectedLineageCommitsProperty, value);
        }

        public static readonly StyledProperty<System.Collections.Generic.HashSet<int>> SelectedLineagePathsProperty =
            AvaloniaProperty.Register<CommitGraph, System.Collections.Generic.HashSet<int>>(nameof(SelectedLineagePaths));

        public System.Collections.Generic.HashSet<int> SelectedLineagePaths
        {
            get => GetValue(SelectedLineagePathsProperty);
            set => SetValue(SelectedLineagePathsProperty, value);
        }

        public static readonly StyledProperty<Models.CommitGraphLayout> LayoutProperty =
            AvaloniaProperty.Register<CommitGraph, Models.CommitGraphLayout>(nameof(Layout));

        public Models.CommitGraphLayout Layout
        {
            get => GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }

        static CommitGraph()
        {
            AffectsRender<CommitGraph>(
                GraphProperty,
                DotBrushProperty,
                OnlyHighlightCurrentBranchProperty,
                HighlightSelectedLineageProperty,
                HoveredLineageCommitsProperty,
                HoveredCommitIndexProperty,
                SelectedLineageCommitsProperty,
                SelectedLineagePathsProperty,
                LayoutProperty);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == GraphProperty ||
                change.Property == HoveredCommitIndexProperty ||
                change.Property == HoveredLineageCommitsProperty ||
                change.Property == SelectedLineageCommitsProperty ||
                change.Property == SelectedLineagePathsProperty ||
                change.Property == HighlightSelectedLineageProperty)
            {
                UpdateHoveredRelated();
            }
        }

        private void UpdateHoveredRelated()
        {
            var graph = Graph;
            if (graph == null)
                return;

            foreach (var line in graph.Paths)
                line.IsHoveredRelated = false;

            var hoveredLineage = HoveredLineageCommits;
            if (hoveredLineage != null && hoveredLineage.Count > 0)
            {
                foreach (var line in graph.Paths)
                {
                    if (line.StartCommitIndex >= 0 && line.EndCommitIndex >= 0)
                    {
                        line.IsHoveredRelated = hoveredLineage.Contains(line.StartCommitIndex) &&
                                                hoveredLineage.Contains(line.EndCommitIndex);
                    }
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Graph is not { } graph || Layout is not { } layout)
                return;

            var startY = layout.StartY;
            var clipWidth = layout.ClipWidth;
            var clipHeight = Bounds.Height;
            var rowHeight = layout.RowHeight;
            var endY = startY + clipHeight + 28;

            using (context.PushClip(new Rect(0, 0, clipWidth, clipHeight)))
            using (context.PushTransform(Matrix.CreateTranslation(0, -startY)))
            {
                DrawCurves(context, graph, startY, endY, rowHeight);
                DrawAnchors(context, graph, startY, endY, rowHeight);
            }
        }

        private void DrawCurves(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight)
        {
            var hoverBold = 2.0;
            var grayedPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), Models.CommitGraph.Pens[0].Thickness);
            var onlyHighlightCurrentBranch = OnlyHighlightCurrentBranch;
            var highlightSelectedLineage = HighlightSelectedLineage;
            var selectedLineageCommits = SelectedLineageCommits;
            var hoveredLineage = HoveredLineageCommits;

            foreach (var link in graph.Links)
            {
                var startY = link.Start.Y * rowHeight;
                var endY = link.End.Y * rowHeight;

                if (endY < top)
                    continue;
                if (startY > bottom)
                    break;

                var isLinkInSelectedLineage =
                    highlightSelectedLineage &&
                    onlyHighlightCurrentBranch &&
                    selectedLineageCommits != null &&
                    link.StartCommitIndex >= 0 && link.EndCommitIndex >= 0 &&
                    selectedLineageCommits.Contains(link.StartCommitIndex) &&
                    selectedLineageCommits.Contains(link.EndCommitIndex);

                var isLinkInHoveredLineage = hoveredLineage != null &&
                    link.StartCommitIndex >= 0 && link.EndCommitIndex >= 0 &&
                    hoveredLineage.Contains(link.StartCommitIndex) &&
                    hoveredLineage.Contains(link.EndCommitIndex);

                var pen = link.Color < 0 ? grayedPen : Models.CommitGraph.Pens[link.Color];
                if (onlyHighlightCurrentBranch && !link.IsMerged && !isLinkInSelectedLineage)
                    pen = grayedPen;

                if (isLinkInHoveredLineage)
                    pen = new Pen(pen.Brush, pen.Thickness + hoverBold);

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(link.Start.X, startY), false);
                    ctx.QuadraticBezierTo(new Point(link.Control.X, link.Control.Y * rowHeight), new Point(link.End.X, endY));
                }

                context.DrawGeometry(null, pen, geo);
            }

            foreach (var line in graph.Paths)
            {
                var last = new Point(line.Points[0].X, line.Points[0].Y * rowHeight);
                var size = line.Points.Count;
                var endY = line.Points[size - 1].Y * rowHeight;

                if (endY < top)
                    continue;
                if (last.Y > bottom)
                    break;

                var isLineInSelectedLineage = highlightSelectedLineage && selectedLineageCommits != null &&
                    line.StartCommitIndex >= 0 && line.EndCommitIndex >= 0 &&
                    selectedLineageCommits.Contains(line.StartCommitIndex) &&
                    selectedLineageCommits.Contains(line.EndCommitIndex);

                var geo = new StreamGeometry();
                var pen = line.Color < 0 ? grayedPen : Models.CommitGraph.Pens[line.Color];
                if (onlyHighlightCurrentBranch && !line.IsMerged && !isLineInSelectedLineage)
                    pen = grayedPen;

                if (line.IsHoveredRelated)
                    pen = new Pen(pen.Brush, pen.Thickness + hoverBold);

                using (var ctx = geo.Open())
                {
                    var started = false;
                    var ended = false;
                    for (int i = 1; i < size; i++)
                    {
                        var cur = new Point(line.Points[i].X, line.Points[i].Y * rowHeight);
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
                                ctx.CubicBezierTo(new Point(last.X, midY + 4), new Point(cur.X, midY - 4), cur);
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
        }

        private void DrawAnchors(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight)
        {
            var dotFill = DotBrush;
            var dotFillPen = new Pen(dotFill, 2);
            var grayedPen = new Pen(Brushes.Gray, Models.CommitGraph.Pens[0].Thickness);
            var onlyHighlightCurrentBranch = OnlyHighlightCurrentBranch;
            var highlightSelectedLineage = HighlightSelectedLineage;
            var selectedLineageCommits = SelectedLineageCommits;

            if (DataContext is not ViewModels.Histories vm)
                return;

            for (int i = 0; i < graph.Dots.Count; i++)
            {
                var dot = graph.Dots[i];
                var center = new Point(dot.Center.X, dot.Center.Y * rowHeight);

                if (center.Y < top)
                    continue;
                if (center.Y > bottom)
                    break;

                bool isDotInSelectedLineage = highlightSelectedLineage && selectedLineageCommits != null && selectedLineageCommits.Contains(i);

                var pen = Models.CommitGraph.Pens[dot.Color];
                if (onlyHighlightCurrentBranch && !dot.IsMerged && !isDotInSelectedLineage)
                    pen = grayedPen;

                switch (dot.Type)
                {
                    case Models.CommitGraph.DotType.Head:
                        context.DrawEllipse(dotFill, pen, center, 6, 6);
                        context.DrawEllipse(pen.Brush, null, center, 3, 3);
                        break;
                    case Models.CommitGraph.DotType.Merge:
                        context.DrawEllipse(pen.Brush, null, center, 6, 6);
                        context.DrawLine(dotFillPen, new Point(center.X, center.Y - 3), new Point(center.X, center.Y + 3));
                        context.DrawLine(dotFillPen, new Point(center.X - 3, center.Y), new Point(center.X + 3, center.Y));
                        break;
                    default:
                        context.DrawEllipse(dotFill, pen, center, 3, 3);
                        break;
                }
            }
        }
    }
}
