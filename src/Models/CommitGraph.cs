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
        }

        public class Link
        {
            public Point Start;
            public Point Control;
            public Point End;
            public int Color;
            public bool IsMerged;
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

        public static CommitGraph Parse(List<Commit> commits, bool firstParentOnlyEnabled, bool alwaysShowCurrentHeadOnLeft)
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

            // 1. Pre-scan for the Grand Lineage (Trunk)
            var headPathSHAs = new HashSet<string>();
            string topTrunkSHA = null;
            if (alwaysShowCurrentHeadOnLeft)
            {
                var head = commits.Find(x => x.IsCurrentHead);
                if (head != null)
                {
                    headPathSHAs.Add(head.SHA);

                    // Trace DOWN (Ancestors) via first parent
                    string currentDown = head.SHA;
                    for (int i = commits.IndexOf(head); i < commits.Count; i++)
                    {
                        if (commits[i].SHA == currentDown)
                        {
                            headPathSHAs.Add(currentDown);
                            if (commits[i].Parents.Count > 0)
                                currentDown = commits[i].Parents[0];
                        }
                    }

                    // Trace UP (Descendants) via strict single line
                    string currentUp = head.SHA;
                    for (int i = commits.IndexOf(head) - 1; i >= 0; i--)
                    {
                        if (commits[i].Parents.Count > 0 && commits[i].Parents[0] == currentUp)
                        {
                            headPathSHAs.Add(commits[i].SHA);
                            currentUp = commits[i].SHA;
                        }
                    }

                    // Find the top-most trunk commit to anchor the Phantom Path
                    foreach (var c in commits)
                    {
                        if (headPathSHAs.Contains(c.SHA))
                        {
                            topTrunkSHA = c.SHA;
                            break;
                        }
                    }
                }
            }

            // 2. Inject Phantom Path for Trunk at Y=0.
            // This forces the Trunk to occupy Slot 0 (X=10) permanently, simulating an uncommitted node.
            if (topTrunkSHA != null)
            {
                var phantom = new PathHelper(topTrunkSHA, false, colorPicker.Next(), new Point(10.0, offsetY));
                phantom.IsTrunk = true;
                unsolved.Add(phantom);
                temp.Paths.Add(phantom.Path);
            }

            foreach (var commit in commits)
            {
                PathHelper major = null;
                var isMerged = commit.IsMerged;
                bool isCommitTrunk = alwaysShowCurrentHeadOnLeft && headPathSHAs.Contains(commit.SHA);

                // Update current y offset
                offsetY += unitHeight;

                // Find first curves that links to this commit and marks others that links to this commit ended.
                var maxOffsetOld = 0.0;
                for (int i = 0; i < unsolved.Count; i++)
                {
                    if (unsolved[i].LastX > maxOffsetOld)
                        maxOffsetOld = unsolved[i].LastX;
                }

                var currentOffsetX = 4 - halfWidth;
                foreach (var l in unsolved)
                {
                    currentOffsetX += unitWidth;

                    if (l.Next.Equals(commit.SHA, StringComparison.Ordinal))
                    {
                        // Only Trunk paths can claim major status for Trunk commits.
                        bool canBeMajor = !isCommitTrunk || l.IsTrunk;

                        if (major == null && canBeMajor)
                        {
                            major = l;

                            if (commit.Parents.Count > 0)
                            {
                                major.Next = commit.Parents[0];
                                major.Goto(currentOffsetX, offsetY, halfHeight);
                            }
                            else
                            {
                                major.End(currentOffsetX, offsetY, halfHeight);
                                ended.Add(l);
                            }
                        }
                        else
                        {
                            l.End(major?.LastX ?? currentOffsetX, offsetY, halfHeight);
                            ended.Add(l);
                        }

                        isMerged = isMerged || l.IsMerged;
                    }
                    else
                    {
                        l.Pass(currentOffsetX, offsetY, halfHeight);
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
                    if (commit.Parents.Count > 0)
                    {
                        currentOffsetX += unitWidth;
                        major = new PathHelper(commit.Parents[0], isMerged, colorPicker.Next(), new Point(currentOffsetX, offsetY));
                        unsolved.Add(major);
                        temp.Paths.Add(major.Path);
                    }
                }
                else if (isMerged && !major.IsMerged && commit.Parents.Count > 0)
                {
                    major.ReplaceMerged();
                    temp.Paths.Add(major.Path);
                }

                // Calculate link position of this commit.
                var position = new Point(major?.LastX ?? Math.Max(currentOffsetX, 10.0), offsetY);
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
                                parent.ReplaceMerged();
                                temp.Paths.Add(parent.Path);
                            }

                            temp.Links.Add(new Link
                            {
                                Start = position,
                                End = new Point(parent.LastX, offsetY + halfHeight),
                                Control = new Point(parent.LastX, position.Y),
                                Color = parent.Path.Color,
                                IsMerged = isMerged,
                            });
                        }
                        else
                        {
                            currentOffsetX += unitWidth;

                            // Create new curve for parent commit that not includes before
                            var l = new PathHelper(parentHash, isMerged, colorPicker.Next(), position, new Point(currentOffsetX, position.Y + halfHeight));
                            unsolved.Add(l);
                            temp.Paths.Add(l.Path);
                        }
                    }
                }

                // Margins & merge state (used by Views.Histories).
                commit.IsMerged = isMerged;
                commit.Color = dotColor;
                commit.LeftMargin = Math.Max(currentOffsetX, maxOffsetOld) + halfWidth + 2;
            }

            // Deal with curves haven't ended yet.
            for (var i = 0; i < unsolved.Count; i++)
            {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * unitHeight;

                if (path.Path.Points.Count == 1 && Math.Abs(path.Path.Points[0].Y - endY) < 0.0001)
                    continue;

                path.End((i + 0.5) * unitWidth + 4, endY + halfHeight, halfHeight);
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
            public bool IsTrunk { get; set; } = false;

            public bool IsMerged => Path.IsMerged;

            public PathHelper(string next, bool isMerged, int color, Point start)
            {
                Next = next;
                LastX = start.X;
                _lastY = start.Y;

                Path = new Path(color, isMerged);
                Path.Points.Add(start);
            }

            public PathHelper(string next, bool isMerged, int color, Point start, Point to)
            {
                Next = next;
                LastX = to.X;
                _lastY = to.Y;

                Path = new Path(color, isMerged);
                Path.Points.Add(start);
                Path.Points.Add(to);
            }

            /// <summary>
            ///     A path that just passed this row.
            /// </summary>
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
            public void ReplaceMerged()
            {
                var color = Path.Color;
                Add(LastX, _lastY);

                Path = new Path(color, true);
                Path.Points.Add(new Point(LastX, _lastY));
                _endY = 0;
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