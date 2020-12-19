using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SourceGit.Helpers {

    /// <summary>
    ///     Tools to parse commit graph.
    /// </summary>
    public class CommitGraphData {
        /// <summary>
        ///     Unit lengths for commit graph
        /// </summary>
        public static readonly double UNIT_WIDTH = 12;
        public static readonly double HALF_WIDTH = 6;
        public static readonly double UNIT_HEIGHT = 24;
        public static readonly double HALF_HEIGHT = 12;

        /// <summary>
        ///     Colors
        /// </summary>
        public static Brush[] Colors = new Brush[] {
            Brushes.Orange,
            Brushes.ForestGreen,
            Brushes.Gold,
            Brushes.Magenta,
            Brushes.Red,
            Brushes.Gray,
            Brushes.Turquoise,
            Brushes.Olive,
        };

        /// <summary>
        ///     Data to draw lines.
        /// </summary>
        public class Line {
            private double lastX = 0;
            private double lastY = 0;

            /// <summary>
            ///     Parent commit id.
            /// </summary>
            public string Next { get; set; }

            /// <summary>
            ///     Is merged into this tree.
            /// </summary>
            public bool IsMerged { get; set; }

            /// <summary>
            ///     Points in line
            /// </summary>
            public List<Point> Points { get; set; }

            /// <summary>
            ///     Brush to draw line
            /// </summary>
            public Brush Brush { get; set; }

            /// <summary>
            ///     Current horizontal offset.
            /// </summary>
            public double HorizontalOffset => lastX;

            /// <summary>
            ///     Constructor.
            /// </summary>
            /// <param name="nextCommitId">Parent commit id</param>
            /// <param name="isMerged">Is merged in tree</param>
            /// <param name="colorIdx">Color index</param>
            /// <param name="startPoint">Start point</param>
            public Line(string nextCommitId, bool isMerged, int colorIdx, Point startPoint) {
                Next = nextCommitId;
                IsMerged = isMerged;
                Points = new List<Point>() { startPoint };
                Brush = Colors[colorIdx % Colors.Length];

                lastX = startPoint.X;
                lastY = startPoint.Y;
            }

            /// <summary>
            ///     Line to.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="isEnd"></param>
            public void AddPoint(double x, double y, bool isEnd = false) {
                if (x > lastX) {
                    Points.Add(new Point(lastX, lastY));
                    Points.Add(new Point(x, y - HALF_HEIGHT));
                } else if (x < lastX) {
                    Points.Add(new Point(lastX, lastY + HALF_HEIGHT));
                    Points.Add(new Point(x, y));
                }

                lastX = x;
                lastY = y;

                if (isEnd) {
                    var last = Points.Last();
                    if (last.X != lastX || last.Y != lastY) Points.Add(new Point(lastX, lastY));
                }
            }
        }

        /// <summary>
        ///     Short link between two commits.
        /// </summary>
        public struct ShortLink {
            public Point Start;
            public Point Control;
            public Point End;
            public Brush Brush;
        }

        /// <summary>
        ///     Dot
        /// </summary>
        public struct Dot {
            public Point Center;
            public Brush Color;
        }

        /// <summary>
        ///     Independent lines in graph
        /// </summary>
        public List<Line> Lines { get; set; } = new List<Line>();

        /// <summary>
        ///     Short links.
        /// </summary>
        public List<ShortLink> Links { get; set; } = new List<ShortLink>();

        /// <summary>
        ///     All dots.
        /// </summary>
        public List<Dot> Dots { get; set; } = new List<Dot>();

        /// <summary>
        ///     Parse commits.
        /// </summary>
        /// <param name="commits"></param>
        /// <returns></returns>
        public static CommitGraphData Parse(List<Git.Commit> commits) {
            CommitGraphData data = new CommitGraphData();

            List<Line> unsolved = new List<Line>();
            List<Line> ended = new List<Line>();
            Dictionary<string, Line> currentMap = new Dictionary<string, Line>();
            double offsetY = -HALF_HEIGHT;
            int colorIdx = 0;

            for (int i = 0; i < commits.Count; i++) {
                Git.Commit commit = commits[i];
                Line major = null;
                bool isMerged = commit.IsHEAD || commit.IsMerged;
                int oldCount = unsolved.Count;

                // 更新Y坐标
                offsetY += UNIT_HEIGHT;

                // 找到第一个依赖于本提交的树，将其他依赖于本提交的树标记为终止，并对已存在的线路调整（防止线重合）
                double offsetX = -HALF_WIDTH;
                foreach (var l in unsolved) {
                    if (l.Next == commit.SHA) {
                        if (major == null) {
                            offsetX += UNIT_WIDTH;
                            major = l;

                            if (commit.Parents.Count > 0) {
                                major.Next = commit.Parents[0];
                                if (!currentMap.ContainsKey(major.Next)) currentMap.Add(major.Next, major);
                            } else {
                                major.Next = "ENDED";
                                ended.Add(l);
                            }

                            major.AddPoint(offsetX, offsetY);
                        } else {
                            ended.Add(l);
                        }

                        isMerged = isMerged || l.IsMerged;
                    } else {
                        if (!currentMap.ContainsKey(l.Next)) currentMap.Add(l.Next, l);
                        offsetX += UNIT_WIDTH;
                        l.AddPoint(offsetX, offsetY);
                    }
                }

                // 处理本提交为非当前分支HEAD的情况（创建新依赖线路）
                if (major == null && commit.Parents.Count > 0) {
                    offsetX += UNIT_WIDTH;
                    major = new Line(commit.Parents[0], isMerged, colorIdx, new Point(offsetX, offsetY));
                    unsolved.Add(major);
                    colorIdx++;
                }

                // 确定本提交的点的位置
                Point position = new Point(offsetX, offsetY);
                if (major != null) {
                    major.IsMerged = isMerged;
                    position.X = major.HorizontalOffset;
                    position.Y = offsetY;
                    data.Dots.Add(new Dot() { Center = position, Color = major.Brush });
                } else {
                    data.Dots.Add(new Dot() { Center = position, Color = Brushes.Orange });
                }

                // 处理本提交的其他依赖
                for (int j = 1; j < commit.Parents.Count; j++) {
                    var parent = commit.Parents[j];
                    if (currentMap.ContainsKey(parent)) {
                        var l = currentMap[parent];
                        var link = new ShortLink();

                        link.Start = position;
                        link.End = new Point(l.HorizontalOffset, offsetY + HALF_HEIGHT);
                        link.Control = new Point(link.End.X, link.Start.Y);
                        link.Brush = l.Brush;
                        data.Links.Add(link);
                    } else {
                        offsetX += UNIT_WIDTH;
                        unsolved.Add(new Line(commit.Parents[j], isMerged, colorIdx, position));
                        colorIdx++;
                    }         
                }

                // 处理已终止的线
                foreach (var l in ended) {
                    l.AddPoint(position.X, position.Y, true);
                    data.Lines.Add(l);
                    unsolved.Remove(l);
                }

                // 加入本次提交
                commit.IsMerged = isMerged;
                commit.GraphOffset = Math.Max(offsetX + HALF_WIDTH, oldCount * UNIT_WIDTH);

                // 清理临时数据
                ended.Clear();
                currentMap.Clear();
            }

            // 处理尚未终结的线
            for (int i = 0; i < unsolved.Count; i++) {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * UNIT_HEIGHT;

                if (path.Points.Count == 1 && path.Points[0].Y == endY) continue; 

                path.AddPoint((i + 0.5) * UNIT_WIDTH, endY, true);
                data.Lines.Add(path);
            }
            unsolved.Clear();

            data.Lines.Sort((l, h) => l.Points[0].Y.CompareTo(h.Points[0].Y));
            return data;
        }
    }

    /// <summary>
    ///     Visual element to render commit graph
    /// </summary>
    public class CommitGraph : FrameworkElement {
        private double offsetY;
        private CommitGraphData data;

        public CommitGraph() {
            Clear();
        }

        public void Clear() {
            offsetY = 0;
            data = null;
        }

        public void SetCommits(List<Git.Commit> commits) {
            data = CommitGraphData.Parse(commits);
            Dispatcher.Invoke(() => InvalidateVisual());            
        }

        public void SetOffset(double y) {
            offsetY = y * CommitGraphData.UNIT_HEIGHT;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc) {
            if (data == null) return;

            var startY = offsetY;
            var endY = offsetY + ActualHeight;

            dc.PushTransform(new TranslateTransform(0, -offsetY));

            // Draw all visible lines.
            foreach (var path in data.Lines) {
                var last = path.Points[0];
                var size = path.Points.Count;

                if (path.Points[size - 1].Y < startY) continue;
                if (last.Y > endY) break;

                var geo = new StreamGeometry();
                var pen = new Pen(path.Brush, 2);

                using (var geoCtx = geo.Open()) {
                    geoCtx.BeginFigure(last, false, false);

                    var ended = false;
                    for (int i = 1; i < size; i++) {
                        var cur = path.Points[i];

                        // Fix line NOT shown in graph if cur.Y is too large than current.
                        if (cur.Y > endY) {
                            cur.Y = endY;
                            ended = true;
                        }

                        if (cur.X > last.X) {
                            geoCtx.QuadraticBezierTo(new Point(cur.X, last.Y), cur, true, false);
                        } else if (cur.X < last.X) {
                            if (i < size - 1) {
                                cur.Y += CommitGraphData.HALF_HEIGHT;

                                var midY = (last.Y + cur.Y) / 2;
                                var midX = (last.X + cur.X) / 2;
                                geoCtx.PolyQuadraticBezierTo(new Point[] {
                                    new Point(last.X, midY),
                                    new Point(midX, midY),
                                    new Point(cur.X, midY),
                                    cur}, true, false);
                            } else {
                                geoCtx.QuadraticBezierTo(new Point(last.X, cur.Y), cur, true, false);
                            }
                        } else {
                            geoCtx.LineTo(cur, true, false);
                        }

                        if (ended) break;
                        last = cur;
                    }
                }

                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }

            // Draw short links
            foreach (var link in data.Links) {
                if (link.End.Y < startY) continue;
                if (link.Start.Y > endY) break;

                var geo = new StreamGeometry();
                var pen = new Pen(link.Brush, 2);

                using (var geoCtx = geo.Open()) {
                    geoCtx.BeginFigure(link.Start, false, false);
                    geoCtx.QuadraticBezierTo(link.Control, link.End, true, false);
                }

                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }

            // Draw visible points
            foreach (var dot in data.Dots) {
                if (dot.Center.Y < startY) continue;
                if (dot.Center.Y > endY) break;

                dc.DrawEllipse(dot.Color, null, dot.Center, 3, 3);
            }
        }
    }
}
