using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SourceGit.Views
{
    public class CommitGraph : Control, Models.IAvatarHost
    {
        public static readonly DirectProperty<CommitGraph, Models.CommitGraph> GraphProperty =
            AvaloniaProperty.RegisterDirect<CommitGraph, Models.CommitGraph>(
                nameof(Graph),
                static o => o.Graph,
                static (o, v) => o.Graph = v);

        public Models.CommitGraph Graph
        {
            get => _graph;
            set => SetAndRaise(GraphProperty, ref _graph, value);
        }

        public static readonly DirectProperty<CommitGraph, Models.CommitGraphLayout> LayoutProperty =
            AvaloniaProperty.RegisterDirect<CommitGraph, Models.CommitGraphLayout>(
                nameof(Layout),
                static o => o.Layout,
                static (o, v) => o.Layout = v);

        public Models.CommitGraphLayout Layout
        {
            get => _layout;
            set => SetAndRaise(LayoutProperty, ref _layout, value);
        }

        public static readonly StyledProperty<IBrush> DotBrushProperty =
            AvaloniaProperty.Register<CommitGraph, IBrush>(nameof(DotBrush), Brushes.Transparent);

        public IBrush DotBrush
        {
            get => GetValue(DotBrushProperty);
            set => SetValue(DotBrushProperty, value);
        }

        public static readonly StyledProperty<Models.CommitGraphStyle> GraphStyleProperty =
            AvaloniaProperty.Register<CommitGraph, Models.CommitGraphStyle>(nameof(GraphStyle));

        public Models.CommitGraphStyle GraphStyle
        {
            get => GetValue(GraphStyleProperty);
            set => SetValue(GraphStyleProperty, value);
        }

        public static readonly StyledProperty<bool> ShowRefConnectorsProperty =
            AvaloniaProperty.Register<CommitGraph, bool>(nameof(ShowRefConnectors));

        public bool ShowRefConnectors
        {
            get => GetValue(ShowRefConnectorsProperty);
            set => SetValue(ShowRefConnectorsProperty, value);
        }

        public static readonly StyledProperty<bool> ShowTagsProperty =
            AvaloniaProperty.Register<CommitGraph, bool>(nameof(ShowTags), true);

        public bool ShowTags
        {
            get => GetValue(ShowTagsProperty);
            set => SetValue(ShowTagsProperty, value);
        }

        /// <summary>
        ///     Width required to draw the given graph without clipping in `Avatars` style.
        /// </summary>
        public static double GetAvatarsStyleWidth(Models.CommitGraph graph)
        {
            if (graph == null || graph.Width <= 0)
                return 0;

            // `Width` is the right edge of the last lane plus 8px padding (in classic units).
            return MapX(graph.Width - 8, AVATARS_LANE_SCALE) + AVATAR_RADIUS + 6;
        }

        /// <summary>
        ///     X position of a graph node (generated in classic units) in `Avatars` style.
        /// </summary>
        public static double GetAvatarsStyleX(double x)
        {
            return MapX(x, AVATARS_LANE_SCALE);
        }

        public static bool HasVisibleRefs(Models.Commit commit, bool showTags)
        {
            if (commit == null)
                return false;

            if (showTags)
                return commit.HasDecorators;

            foreach (var d in commit.Decorators)
            {
                if (d.Type != Models.DecoratorType.Tag)
                    return true;
            }

            return false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_graph == null || _layout == null)
                return;

            var startY = _layout.StartY;
            var offsetX = _layout.OffsetX;
            var clipWidth = _layout.ClipWidth;
            var clipHeight = Bounds.Height;
            var rowHeight = _layout.RowHeight;
            var endY = startY + clipHeight + 28;
            var isAvatars = GraphStyle == Models.CommitGraphStyle.Avatars;
            var scale = isAvatars ? AVATARS_LANE_SCALE : 1.0;

            using (context.PushClip(new Rect(offsetX, 0, clipWidth, clipHeight)))
            using (context.PushTransform(Matrix.CreateTranslation(offsetX, -startY)))
            {
                if (isAvatars)
                {
                    DrawRowDecorations(context, _graph, startY, endY, rowHeight, clipWidth);
                    DrawRoundedLines(context, _graph, startY, endY, rowHeight, scale);
                    DrawAvatarAnchors(context, _graph, startY, endY, rowHeight);
                }
                else
                {
                    DrawCurves(context, _graph, startY, endY, rowHeight, scale);
                    DrawAnchors(context, _graph, startY, endY, rowHeight);
                }
            }
        }

        public void OnAvatarResourceChanged(string email, Bitmap image)
        {
            if (_avatars.ContainsKey(email))
            {
                _avatars[email] = image;
                InvalidateVisual();
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            Models.AvatarManager.Instance.Subscribe(this);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            Models.AvatarManager.Instance.Unsubscribe(this);
            _avatars.Clear();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == GraphProperty ||
                change.Property == LayoutProperty ||
                change.Property == DotBrushProperty ||
                change.Property == GraphStyleProperty ||
                change.Property == ShowRefConnectorsProperty ||
                change.Property == ShowTagsProperty)
                InvalidateVisual();
        }

        private static double MapX(double x, double scale)
        {
            // Graph is generated with 12px lanes starting at x = 4. Stretch lanes horizontally for other styles.
            return scale == 1.0 ? x : 4 + (x - 4) * scale;
        }

        private void DrawCurves(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight, double scale)
        {
            var grayedPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), Models.CommitGraph.Pens[0].Thickness);

            foreach (var link in graph.Links)
            {
                var startY = link.Start.Y * rowHeight;
                var endY = link.End.Y * rowHeight;

                if (endY < top)
                    continue;
                if (startY > bottom)
                    break;

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(MapX(link.Start.X, scale), startY), false);
                    ctx.QuadraticBezierTo(new Point(MapX(link.Control.X, scale), link.Control.Y * rowHeight), new Point(MapX(link.End.X, scale), endY));
                }

                var pen = link.IsHighlighted ? Models.CommitGraph.Pens[link.Color] : grayedPen;
                context.DrawGeometry(null, pen, geo);
            }

            foreach (var line in graph.Paths)
            {
                var last = new Point(MapX(line.Points[0].X, scale), line.Points[0].Y * rowHeight);
                var size = line.Points.Count;
                var endY = line.Points[size - 1].Y * rowHeight;

                if (endY < top)
                    continue;
                if (last.Y > bottom)
                    break;

                var geo = new StreamGeometry();
                var pen = line.IsHighlighted ? Models.CommitGraph.Pens[line.Color] : grayedPen;

                using (var ctx = geo.Open())
                {
                    var started = false;
                    var ended = false;
                    for (int i = 1; i < size; i++)
                    {
                        var cur = new Point(MapX(line.Points[i].X, scale), line.Points[i].Y * rowHeight);
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

        /// <summary>
        ///     Same as `DrawCurves`, but uses horizontal/vertical lines with rounded corners instead of splines.
        /// </summary>
        private void DrawRoundedLines(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight, double scale)
        {
            var grayedPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), Models.CommitGraph.Pens[0].Thickness);
            var vertices = new List<Point>();

            foreach (var link in graph.Links)
            {
                var startY = link.Start.Y * rowHeight;
                var endY = link.End.Y * rowHeight;

                if (endY < top)
                    continue;
                if (startY > bottom)
                    break;

                // Leave the merge commit horizontally, then turn down into the parent's lane.
                var start = new Point(MapX(link.Start.X, scale), startY);
                var end = new Point(MapX(link.End.X, scale), endY);
                vertices.Clear();
                vertices.Add(start);
                vertices.Add(new Point(end.X, start.Y));
                vertices.Add(end);

                var pen = link.IsHighlighted ? Models.CommitGraph.Pens[link.Color] : grayedPen;
                DrawRoundedPolyline(context, pen, vertices);
            }

            foreach (var line in graph.Paths)
            {
                var last = new Point(MapX(line.Points[0].X, scale), line.Points[0].Y * rowHeight);
                var size = line.Points.Count;
                var endY = line.Points[size - 1].Y * rowHeight;

                if (endY < top)
                    continue;
                if (last.Y > bottom)
                    break;

                vertices.Clear();
                for (int i = 1; i < size; i++)
                {
                    var cur = new Point(MapX(line.Points[i].X, scale), line.Points[i].Y * rowHeight);
                    if (cur.Y < top)
                    {
                        last = cur;
                        continue;
                    }

                    if (vertices.Count == 0)
                        vertices.Add(last);

                    if (cur.X > last.X)
                    {
                        vertices.Add(new Point(cur.X, last.Y));
                    }
                    else if (cur.X < last.X)
                    {
                        if (i < size - 1)
                        {
                            var midY = (last.Y + cur.Y) / 2;
                            vertices.Add(new Point(last.X, midY));
                            vertices.Add(new Point(cur.X, midY));
                        }
                        else
                        {
                            vertices.Add(new Point(last.X, cur.Y));
                        }
                    }

                    vertices.Add(cur);

                    if (cur.Y > bottom)
                        break;
                    last = cur;
                }

                var pen = line.IsHighlighted ? Models.CommitGraph.Pens[line.Color] : grayedPen;
                DrawRoundedPolyline(context, pen, vertices);
            }
        }

        private static void DrawRoundedPolyline(DrawingContext context, Pen pen, List<Point> vertices)
        {
            // Drop duplicated and collinear vertices, so only real corners remain.
            var points = new List<Point>(vertices.Count);
            foreach (var v in vertices)
            {
                if (points.Count > 0 && Math.Abs(points[^1].X - v.X) < 0.01 && Math.Abs(points[^1].Y - v.Y) < 0.01)
                    continue;

                if (points.Count > 1)
                {
                    var a = points[^2];
                    var b = points[^1];
                    var cross = (b.X - a.X) * (v.Y - b.Y) - (b.Y - a.Y) * (v.X - b.X);
                    if (Math.Abs(cross) < 0.01)
                        points.RemoveAt(points.Count - 1);
                }

                points.Add(v);
            }

            if (points.Count < 2)
                return;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(points[0], false);

                var lastIdx = points.Count - 1;
                for (var i = 1; i < lastIdx; i++)
                {
                    var prev = points[i - 1];
                    var corner = points[i];
                    var next = points[i + 1];

                    var inLen = Math.Sqrt((corner.X - prev.X) * (corner.X - prev.X) + (corner.Y - prev.Y) * (corner.Y - prev.Y));
                    var outLen = Math.Sqrt((next.X - corner.X) * (next.X - corner.X) + (next.Y - corner.Y) * (next.Y - corner.Y));

                    // Segments between two corners are shared, so each corner may only use half of it.
                    var inMax = i == 1 ? inLen : inLen * 0.5;
                    var outMax = i + 1 == lastIdx ? outLen : outLen * 0.5;
                    var radius = Math.Min(CORNER_RADIUS, Math.Min(inMax, outMax));

                    var from = new Point(corner.X + (prev.X - corner.X) / inLen * radius, corner.Y + (prev.Y - corner.Y) / inLen * radius);
                    var to = new Point(corner.X + (next.X - corner.X) / outLen * radius, corner.Y + (next.Y - corner.Y) / outLen * radius);
                    ctx.LineTo(from);
                    ctx.QuadraticBezierTo(corner, to);
                }

                ctx.LineTo(points[lastIdx]);
            }

            context.DrawGeometry(null, pen, geo);
        }

        private void DrawAnchors(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight)
        {
            var dotFill = DotBrush;
            var dotFillPen = new Pen(dotFill, 2);
            var grayedPen = new Pen(Brushes.Gray, Models.CommitGraph.Pens[0].Thickness);

            foreach (var dot in graph.Dots)
            {
                var center = new Point(dot.Center.X, dot.Center.Y * rowHeight);

                if (center.Y < top)
                    continue;
                if (center.Y > bottom)
                    break;

                var pen = dot.IsHighlighted ? Models.CommitGraph.Pens[dot.Color] : grayedPen;
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

        private void DrawRowDecorations(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight, double clipWidth)
        {
            var showConnectors = ShowRefConnectors;
            var showTags = ShowTags;
            var grayedPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), Models.CommitGraph.Pens[0].Thickness);
            var bandHeight = Math.Max(rowHeight - 6, 4);

            foreach (var dot in graph.Dots)
            {
                var x = MapX(dot.Center.X, AVATARS_LANE_SCALE);
                var y = dot.Center.Y * rowHeight;

                if (y < top)
                    continue;
                if (y > bottom)
                    break;

                var pen = dot.IsHighlighted ? Models.CommitGraph.Pens[dot.Color] : grayedPen;

                // Translucent band from the node to the commit message column.
                if (clipWidth > x)
                {
                    using (context.PushOpacity(dot.IsHighlighted ? 0.12 : 0.05))
                        context.FillRectangle(pen.Brush, new Rect(x, y - bandHeight * 0.5, clipWidth - x, bandHeight));
                }

                // Connector from the BRANCH / TAG column to the node.
                if (showConnectors && HasVisibleRefs(dot.Commit, showTags))
                    context.DrawLine(pen, new Point(0, y), new Point(x, y));
            }
        }

        private void DrawAvatarAnchors(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight)
        {
            var dotFill = DotBrush;
            var grayedBrush = Brushes.Gray;

            foreach (var dot in graph.Dots)
            {
                var center = new Point(MapX(dot.Center.X, AVATARS_LANE_SCALE), dot.Center.Y * rowHeight);

                if (center.Y < top)
                    continue;
                if (center.Y > bottom)
                    break;

                var brush = dot.IsHighlighted ? Models.CommitGraph.Pens[dot.Color].Brush : grayedBrush;

                // Merge commits do not have an avatar.
                if (dot.Type == Models.CommitGraph.DotType.Merge)
                {
                    context.DrawEllipse(brush, null, center, MERGE_DOT_RADIUS, MERGE_DOT_RADIUS);
                    continue;
                }

                if (dot.Type == Models.CommitGraph.DotType.Head)
                    context.DrawEllipse(dotFill, new Pen(brush, 1.5), center, AVATAR_RADIUS + 2.5, AVATAR_RADIUS + 2.5);

                context.DrawEllipse(brush, null, center, AVATAR_RADIUS, AVATAR_RADIUS);

                var inner = AVATAR_RADIUS - 2;
                var rect = new Rect(center.X - inner, center.Y - inner, inner * 2, inner * 2);
                var author = dot.Commit?.Author;
                using (context.PushOpacity(dot.IsHighlighted ? 1.0 : 0.4))
                using (context.PushGeometryClip(new EllipseGeometry(rect)))
                {
                    var img = author != null ? GetAvatar(author.Email) : null;
                    if (img != null)
                        context.DrawImage(img, rect);
                    else
                        Avatar.DrawFallback(context, author?.Name, rect, inner);
                }
            }
        }

        private Bitmap GetAvatar(string email)
        {
            if (string.IsNullOrEmpty(email))
                return null;

            // Only ask `AvatarManager` once per email. Updates are pushed back via `OnAvatarResourceChanged`.
            if (!_avatars.TryGetValue(email, out var img))
            {
                img = Models.AvatarManager.Instance.Request(email, false);
                _avatars.Add(email, img);
            }

            return img;
        }

        private const double AVATARS_LANE_SCALE = 2.0;
        private const double AVATAR_RADIUS = 10.0;
        private const double MERGE_DOT_RADIUS = 5.0;
        private const double CORNER_RADIUS = 8.0;

        private Models.CommitGraph _graph = null;
        private Models.CommitGraphLayout _layout = null;
        private Dictionary<string, Bitmap> _avatars = new Dictionary<string, Bitmap>();
    }
}
