using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Media;

namespace SourceGit.Models
{
    public record CommitGraphLayout(double StartY, double ClipWidth, double RowHeight);

    public class CommitGraph
    {
        public static List<Pen> Pens { get; } = [];

        public static void SetDefaultPens(double thickness = 2)
        {
            SetPens(s_defaultPenColors, thickness);
        }

        public static void SetPens(List<Color> colors, double thickness)
        {
            Pens.Clear();

            foreach (var c in colors)
                Pens.Add(new Pen(c.ToUInt32(), thickness));

            s_penCount = colors.Count;
        }

        public class Path(int color, bool isMerged)
        {
            public List<Point> Points { get; } = [];
            public int Color { get; } = color;
            public bool IsMerged { get; } = isMerged;
            public bool IsHoveredRelated { get; set; } = false;
            public int StartCommitIndex { get; set; } = -1;
            public int EndCommitIndex { get; set; } = -1;
        }

        public class Link
        {
            public Point Start;
            public Point Control;
            public Point End;
            public int Color;
            public bool IsMerged;
            public int StartCommitIndex;
            public int EndCommitIndex;
        }

        public enum DotType
        {
            Default,
            Head,
            Merge,
        }

        public class Dot
        {
            public DotType Type;
            public Point Center;
            public int Color;
            public bool IsMerged;
        }

        public List<Path> Paths { get; } = [];
        public List<Link> Links { get; } = [];
        public List<Dot> Dots { get; } = [];

        public static CommitGraph Parse(List<Commit> commits, bool firstParentOnlyEnabled)
        {
            const double unitWidth = 12;
            const double halfWidth = 6;
            const double unitHeight = 1;
            const double halfHeight = 0.5;

            var temp = new CommitGraph();
            var unsolved = new List<PathHelper>();
            var ended = new List<PathHelper>();
            var offsetY = -halfHeight;
            var colorPicker = new ColorPicker();

            var commitMap = new Dictionary<string, int>();
            for (int i = 0; i < commits.Count; i++)
            {
                commits[i].Index = i;
                commitMap[commits[i].SHA] = i;
            }

            foreach (var commit in commits)
            {
                PathHelper major = null;
                var isMerged = commit.IsMerged;

                // Update current y offset
                offsetY += unitHeight;

                // Find first curves that links to this commit and marks others that links to this commit ended.
                var offsetX = 4 - halfWidth;
                var maxOffsetOld = unsolved.Count > 0 ? unsolved[^1].LastX : offsetX + unitWidth;
                foreach (var l in unsolved)
                {
                    if (l.Next.Equals(commit.SHA, StringComparison.Ordinal))
                    {
                        if (major == null)
                        {
                            offsetX += unitWidth;
                            major = l;

                            if (commit.Parents.Count > 0)
                            {
                                major.Next = commit.Parents[0];
                                major.Goto(offsetX, offsetY, halfHeight);
                            }
                            else
                            {
                                major.End(offsetX, offsetY, halfHeight);
                                major.Path.EndCommitIndex = commit.Index;
                                ended.Add(l);
                            }
                        }
                        else
                        {
                            l.End(major.LastX, offsetY, halfHeight);
                            l.Path.EndCommitIndex = commit.Index;
                            ended.Add(l);
                        }

                        isMerged = isMerged || l.IsMerged;
                    }
                    else
                    {
                        offsetX += unitWidth;
                        l.Pass(offsetX, offsetY, halfHeight);
                    }
                }

                // Remove ended curves from unsolved
                foreach (var l in ended)
                {
                    colorPicker.Recycle(l.Path.Color);
                    unsolved.Remove(l);
                }
                ended.Clear();

                // If no path found, create new curve for branch head
                // Otherwise, create new curve for new merged commit
                if (major == null)
                {
                    offsetX += unitWidth;

                    if (commit.Parents.Count > 0)
                    {
                        major = new PathHelper(commit.Parents[0], isMerged, colorPicker.Next(), new Point(offsetX, offsetY), commit.Index);
                        unsolved.Add(major);
                        temp.Paths.Add(major.Path);
                    }
                }
                else if (isMerged && !major.IsMerged && commit.Parents.Count > 0)
                {
                    major.Path.EndCommitIndex = commit.Index;
                    major.ReplaceMerged(commit.Index);
                    temp.Paths.Add(major.Path);
                }
                else if (major != null && commit.Parents.Count > 0)
                {
                    // Break at every commit to ensure path-aware highlight is precise.
                    major.Path.EndCommitIndex = commit.Index;
                    major.Replace(major.Path.Color, major.Path.IsMerged, commit.Index);
                    temp.Paths.Add(major.Path);
                }

                if (major != null)
                    commit.PathIndex = temp.Paths.IndexOf(major.Path);

                // Calculate link position of this commit.
                var position = new Point(major?.LastX ?? offsetX, offsetY);
                var dotColor = major?.Path.Color ?? 0;
                var anchor = new Dot() { Center = position, Color = dotColor, IsMerged = isMerged };
                if (commit.IsCurrentHead)
                    anchor.Type = DotType.Head;
                else if (commit.Parents.Count > 1)
                    anchor.Type = DotType.Merge;
                else
                    anchor.Type = DotType.Default;
                temp.Dots.Add(anchor);

                // Deal with other parents (the first parent has been processed)
                if (!firstParentOnlyEnabled)
                {
                    for (int j = 1; j < commit.Parents.Count; j++)
                    {
                        var parentHash = commit.Parents[j];
                        var parent = unsolved.Find(x => x.Next.Equals(parentHash, StringComparison.Ordinal));
                        if (parent != null)
                        {
                            if (isMerged && !parent.IsMerged)
                            {
                                parent.Goto(parent.LastX, offsetY + halfHeight, halfHeight);
                                parent.Path.EndCommitIndex = commit.Index;
                                parent.ReplaceMerged(commit.Index);
                                temp.Paths.Add(parent.Path);
                            }

                            temp.Links.Add(new Link
                            {
                                Start = position,
                                End = new Point(parent.LastX, offsetY + halfHeight),
                                Control = new Point(parent.LastX, position.Y),
                                Color = parent.Path.Color,
                                IsMerged = isMerged,
                                StartCommitIndex = commit.Index,
                                EndCommitIndex = commitMap.GetValueOrDefault(parentHash, -1),
                            });
                        }
                        else
                        {
                            offsetX += unitWidth;

                            // Create new curve for parent commit that not includes before
                            var l = new PathHelper(parentHash, isMerged, colorPicker.Next(), position, new Point(offsetX, position.Y + halfHeight), commit.Index);
                            unsolved.Add(l);
                            temp.Paths.Add(l.Path);
                        }
                    }
                }

                // Margins & merge state (used by Views.Histories).
                commit.IsMerged = isMerged;
                commit.Color = dotColor;
                commit.LeftMargin = Math.Max(offsetX, maxOffsetOld) + halfWidth + 2;
            }

            // Deal with curves haven't ended yet.
            for (var i = 0; i < unsolved.Count; i++)
            {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * unitHeight;

                if (path.Path.Points.Count == 1 && Math.Abs(path.Path.Points[0].Y - endY) < 0.0001)
                    continue;

                path.End((i + 0.5) * unitWidth + 4, endY + halfHeight, halfHeight);
                path.Path.EndCommitIndex = commits.Count - 1;
            }
            unsolved.Clear();

            return temp;
        }

        private class ColorPicker
        {
            public int Next()
            {
                if (_colorsQueue.Count == 0)
                {
                    for (var i = 0; i < s_penCount; i++)
                        _colorsQueue.Enqueue(i);
                }

                return _colorsQueue.Dequeue();
            }

            public void Recycle(int idx)
            {
                if (!_colorsQueue.Contains(idx))
                    _colorsQueue.Enqueue(idx);
            }

            private Queue<int> _colorsQueue = new Queue<int>();
        }

        private class PathHelper
        {
            public Path Path { get; private set; }
            public string Next { get; set; }
            public double LastX { get; private set; }

            public bool IsMerged => Path.IsMerged;

            public PathHelper(string next, bool isMerged, int color, Point start, int startCommitIndex)
            {
                Next = next;
                LastX = start.X;
                _lastY = start.Y;

                Path = new Path(color, isMerged);
                Path.Points.Add(start);
                Path.StartCommitIndex = startCommitIndex;
            }

            public PathHelper(string next, bool isMerged, int color, Point start, Point to, int startCommitIndex)
            {
                Next = next;
                LastX = to.X;
                _lastY = to.Y;

                Path = new Path(color, isMerged);
                Path.Points.Add(start);
                Path.Points.Add(to);
                Path.StartCommitIndex = startCommitIndex;
            }

            /// <summary>
            ///     A path that just passed this row.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="halfHeight"></param>
            public void Pass(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    Add(LastX, y - halfHeight);
                    y += halfHeight;
                    Add(x, y);
                }

                LastX = x;
                _lastY = y;
            }

            /// <summary>
            ///     A path that has commit in this row but not ended
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="halfHeight"></param>
            public void Goto(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    var minY = y - halfHeight;
                    if (minY > _lastY)
                        minY -= halfHeight;

                    Add(LastX, minY);
                    Add(x, y);
                }

                LastX = x;
                _lastY = y;
            }

            /// <summary>
            ///     A path that has commit in this row and end.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="halfHeight"></param>
            public void End(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    Add(LastX, y - halfHeight);
                }

                Add(x, y);

                LastX = x;
                _lastY = y;
            }

            /// <summary>
            ///     End the current path and create a new from the end.
            /// </summary>
            public void Replace(int newColor, bool isMerged, int startCommitIndex)
            {
                Add(LastX, _lastY);

                Path = new Path(newColor, isMerged);
                Path.Points.Add(new Point(LastX, _lastY));
                Path.StartCommitIndex = startCommitIndex;
                _endY = 0;
            }

            public void ReplaceMerged(int startCommitIndex)
            {
                Replace(Path.Color, true, startCommitIndex);
            }

            private void Add(double x, double y)
            {
                if (_endY < y)
                {
                    Path.Points.Add(new Point(x, y));
                    _endY = y;
                }
            }

            private double _lastY = 0;
            private double _endY = 0;
        }

        private static int s_penCount = 0;
        private static readonly List<Color> s_defaultPenColors = [
            Colors.Orange,
            Colors.ForestGreen,
            Colors.Turquoise,
            Colors.Olive,
            Colors.Magenta,
            Colors.Red,
            Colors.Khaki,
            Colors.Lime,
            Colors.RoyalBlue,
            Colors.Teal,
        ];
    }
}
