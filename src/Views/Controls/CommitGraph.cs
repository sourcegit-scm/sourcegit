using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     提交线路图
    /// </summary>
    public class CommitGraph : FrameworkElement {
        public static readonly Pen[] PENS = new Pen[] {
            new Pen(Brushes.Orange, 2),
            new Pen(Brushes.ForestGreen, 2),
            new Pen(Brushes.Gold, 2),
            new Pen(Brushes.Magenta, 2),
            new Pen(Brushes.Red, 2),
            new Pen(Brushes.Gray, 2),
            new Pen(Brushes.Turquoise, 2),
            new Pen(Brushes.Olive, 2),
        };

        public static readonly double UNIT_WIDTH = 12;
        public static readonly double HALF_WIDTH = 6;
        public static readonly double UNIT_HEIGHT = 24;
        public static readonly double HALF_HEIGHT = 12;

        public class Path {
            public List<Point> Points = new List<Point>();
            public int Color = 0;
        }

        public class PathHelper {
            public string Next;
            public bool IsMerged;
            public double LastX;
            public double LastY;
            public double EndY;
            public Path Path;

            public PathHelper(string next, bool isMerged, int color, Point start) {
                Next = next;
                IsMerged = isMerged;
                LastX = start.X;
                LastY = start.Y;
                EndY = LastY;

                Path = new Path();
                Path.Color = color % PENS.Length;
                Path.Points.Add(start);
            }

            public PathHelper(string next, bool isMerged, int color, Point start, Point to) {
                Next = next;
                IsMerged = isMerged;
                LastX = to.X;
                LastY = to.Y;
                EndY = LastY;

                Path = new Path();
                Path.Color = color % PENS.Length;
                Path.Points.Add(start);
                Path.Points.Add(to);
            }

            public void Add(double x, double y, bool isEnd = false) {
                if (x > LastX) {
                    Add(new Point(LastX, LastY));
                    Add(new Point(x, y - HALF_HEIGHT));
                    if (isEnd) Add(new Point(x, y));
                } else if (x < LastX) {
                    if (y > LastY + HALF_HEIGHT) Add(new Point(LastX, LastY + HALF_HEIGHT));
                    Add(new Point(x, y));
                } else if (isEnd) {
                    Add(new Point(x, y));
                }

                LastX = x;
                LastY = y;
            }

            private void Add(Point p) {
                if (EndY < p.Y) {
                    Path.Points.Add(p);
                    EndY = p.Y;
                }
            }
        }

        public class Link {
            public Point Start;
            public Point Control;
            public Point End;
            public int Color;
        }

        public class Dot {
            public Point Center;
            public int Color;
        }

        public class Data {
            public List<Path> Paths = new List<Path>();
            public List<Link> Links = new List<Link>();
            public List<Dot> Dots = new List<Dot>();
        }

        private Data data = null;
        private double startY = 0;

        public CommitGraph() {
            IsHitTestVisible = false;
            ClipToBounds = true;
        }

        public void SetOffset(double offset) {
            startY = offset;
            InvalidateVisual();
        }

        public void SetData(List<Models.Commit> commits, bool isSearchResult = false) {
            if (isSearchResult) {
                foreach (var c in commits) c.Margin = new Thickness(0);
                data = null;
                return;
            }

            var temp = new Data();
            var unsolved = new List<PathHelper>();
            var mapUnsolved = new Dictionary<string, PathHelper>();
            var ended = new List<PathHelper>();
            var offsetY = -HALF_HEIGHT;
            var colorIdx = 0;

            foreach (var commit in commits) {
                var major = null as PathHelper;
                var isMerged = commit.IsMerged;
                var oldCount = unsolved.Count;

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
                                if (!mapUnsolved.ContainsKey(major.Next)) mapUnsolved.Add(major.Next, major);
                            } else {
                                major.Next = "ENDED";
                                ended.Add(l);
                            }

                            major.Add(offsetX, offsetY);
                        } else {
                            ended.Add(l);
                        }

                        isMerged = isMerged || l.IsMerged;
                    } else {
                        if (!mapUnsolved.ContainsKey(l.Next)) mapUnsolved.Add(l.Next, l);
                        offsetX += UNIT_WIDTH;
                        l.Add(offsetX, offsetY);
                    }
                }

                // 处理本提交为非当前分支HEAD的情况（创建新依赖线路）
                if (major == null && commit.Parents.Count > 0) {
                    offsetX += UNIT_WIDTH;
                    major = new PathHelper(commit.Parents[0], isMerged, colorIdx, new Point(offsetX, offsetY));
                    unsolved.Add(major);
                    colorIdx++;
                }

                // 确定本提交的点的位置
                Point position = new Point(offsetX, offsetY);
                if (major != null) {
                    major.IsMerged = isMerged;
                    position.X = major.LastX;
                    position.Y = offsetY;
                    temp.Dots.Add(new Dot() { Center = position, Color = major.Path.Color });
                } else {
                    temp.Dots.Add(new Dot() { Center = position, Color = 0 });
                }

                // 处理本提交的其他依赖
                for (int j = 1; j < commit.Parents.Count; j++) {
                    var parent = commit.Parents[j];
                    if (mapUnsolved.ContainsKey(parent)) {
                        var l = mapUnsolved[parent];
                        var link = new Link();

                        link.Start = position;
                        link.End = new Point(l.LastX, offsetY + HALF_HEIGHT);
                        link.Control = new Point(link.End.X, link.Start.Y);
                        link.Color = l.Path.Color;
                        temp.Links.Add(link);
                    } else {
                        offsetX += UNIT_WIDTH;

                        // 防止有下一个提交有ended线时，新的分支线与旧线重合
                        unsolved.Add(new PathHelper(commit.Parents[j], isMerged, colorIdx, position, new Point(offsetX, position.Y + HALF_HEIGHT)));
                        colorIdx++;
                    }
                }

                // 处理已终止的线
                foreach (var l in ended) {
                    l.Add(position.X, position.Y, true);
                    temp.Paths.Add(l.Path);
                    unsolved.Remove(l);
                }

                // 加入本次提交
                commit.IsMerged = isMerged;
                commit.Margin = new Thickness(Math.Max(offsetX + HALF_WIDTH, oldCount * UNIT_WIDTH), 0, 0, 0);

                // 清理
                ended.Clear();
                mapUnsolved.Clear();
            }

            // 处理尚未终结的线
            for (int i = 0; i < unsolved.Count; i++) {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * UNIT_HEIGHT;

                if (path.Path.Points.Count == 1 && path.Path.Points[0].Y == endY) continue;

                path.Add((i + 0.5) * UNIT_WIDTH, endY, true);
                temp.Paths.Add(path.Path);
            }
            unsolved.Clear();

            // 排序
            temp.Paths.Sort((l, h) => l.Points[0].Y.CompareTo(h.Points[0].Y));

            Dispatcher.Invoke(() => {
                data = temp;
                InvalidateVisual();
            });
        }

        protected override void OnRender(DrawingContext dc) {
            if (data == null) return;

            dc.PushTransform(new TranslateTransform(0, -startY));

            // 计算边界
            var top = startY;
            var bottom = startY + ActualHeight;

            // 绘制线
            if (Models.Preference.Instance.Window.UsePolylineInGraph) {
                DrawPolyLines(dc, top, bottom);
            } else {
                DrawCurves(dc, top, bottom);
            }

            // 绘制点
            var dotFill = FindResource("Brush.Contents") as Brush;
            foreach (var dot in data.Dots) {
                if (dot.Center.Y < top) continue;
                if (dot.Center.Y > bottom) break;

                dc.DrawEllipse(dotFill, PENS[dot.Color], dot.Center, 3, 3);
            }
        }
    
        private void DrawCurves(DrawingContext dc, double top, double bottom) {
            foreach (var line in data.Paths) {
                var last = line.Points[0];
                var size = line.Points.Count;

                if (line.Points[size - 1].Y < top) continue;
                if (last.Y > bottom) continue;

                var geo = new StreamGeometry();
                var pen = PENS[line.Color];
                using (var ctx = geo.Open()) {
                    ctx.BeginFigure(last, false, false);

                    var ended = false;
                    for (int i = 1; i < size; i++) {
                        var cur = line.Points[i];
                        if (cur.Y > bottom) {
                            cur.Y = bottom;
                            ended = true;
                        }

                        if (cur.X > last.X) {
                            ctx.QuadraticBezierTo(new Point(cur.X, last.Y), cur, true, false);
                        } else if (cur.X < last.X) {
                            if (i < size - 1) {
                                var midY = (last.Y + cur.Y) / 2;
                                var midX = (last.X + cur.X) / 2;
                                ctx.PolyQuadraticBezierTo(new Point[] {
                                    new Point(last.X, midY),
                                    new Point(midX, midY),
                                    new Point(cur.X, midY),
                                    cur}, true, false);
                            } else {
                                ctx.QuadraticBezierTo(new Point(last.X, cur.Y), cur, true, false);
                            }
                        } else {
                            ctx.LineTo(cur, true, false);
                        }

                        if (ended) break;
                        last = cur;
                    }
                }

                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }

            foreach (var link in data.Links) {
                if (link.End.Y < top) continue;
                if (link.Start.Y > bottom) break;

                var geo = new StreamGeometry();
                using (var ctx = geo.Open()) {
                    ctx.BeginFigure(link.Start, false, false);
                    ctx.QuadraticBezierTo(link.Control, link.End, true, false);
                }

                geo.Freeze();
                dc.DrawGeometry(null, PENS[link.Color], geo);
            }
        }

        private void DrawPolyLines(DrawingContext dc, double top, double bottom) {
            foreach (var line in data.Paths) {
                var last = line.Points[0];
                var size = line.Points.Count;

                if (line.Points[size - 1].Y < top) continue;
                if (last.Y > bottom) continue;

                var geo = new StreamGeometry();
                var pen = PENS[line.Color];
                using (var ctx = geo.Open()) {
                    ctx.BeginFigure(last, false, false);

                    var ended = false;
                    for (int i = 1; i < size; i++) {
                        var cur = line.Points[i];
                        if (cur.Y > bottom) {
                            cur.Y = bottom;
                            ended = true;
                        }

                        ctx.LineTo(cur, true, false);
                        if (ended) break;
                        last = cur;
                    }
                }

                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }

            foreach (var link in data.Links) {
                if (link.End.Y < top) continue;
                if (link.Start.Y > bottom) break;
                dc.DrawLine(PENS[link.Color], link.Start, link.End);
            }
        }
    }
}
