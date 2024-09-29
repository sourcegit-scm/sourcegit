using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Media;

namespace SourceGit.Models
{
    public class CommitGraph
    {
        public class Path
        {
            public List<Point> Points = new List<Point>();
            public int Color = 0;
        }

        public class PathHelper
        {
            public string Next;
            public bool IsMerged;
            public double LastX;
            public double LastY;
            public Path Path;

            public PathHelper(string next, bool isMerged, int color, Point start)
            {
                Next = next;
                IsMerged = isMerged;
                LastX = start.X;
                LastY = start.Y;

                Path = new Path();
                Path.Color = color;
                Path.Points.Add(start);
            }

            public PathHelper(string next, bool isMerged, int color, Point start, Point to)
            {
                Next = next;
                IsMerged = isMerged;
                LastX = to.X;
                LastY = to.Y;

                Path = new Path();
                Path.Color = color;
                Path.Points.Add(start);
                Path.Points.Add(to);
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
                    Add(LastX, LastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    Add(LastX, y - halfHeight);
                    y += halfHeight;
                    Add(x, y);
                }

                LastX = x;
                LastY = y;
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
                    Add(LastX, LastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    var minY = y - halfHeight;
                    if (minY > LastY)
                        minY -= halfHeight;

                    Add(LastX, minY);
                    Add(x, y);
                }

                LastX = x;
                LastY = y;
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
                    Add(LastX, LastY);
                    Add(x, y - halfHeight);
                }                    
                else if (x < LastX)
                {
                    Add(LastX, y - halfHeight);
                }                    

                Add(x, y);

                LastX = x;
                LastY = y;
            }

            private void Add(double x, double y)
            {
                if (_endY < y)
                {
                    Path.Points.Add(new Point(x, y));
                    _endY = y;
                }
            }

            private double _endY = 0;
        }

        public class Link
        {
            public Point Start;
            public Point Control;
            public Point End;
            public int Color;
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
        }

        public List<Path> Paths { get; set; } = new List<Path>();
        public List<Link> Links { get; set; } = new List<Link>();
        public List<Dot> Dots { get; set; } = new List<Dot>();

        public static List<Pen> Pens
        {
            get;
            private set;
        } = new List<Pen>();

        public static void SetDefaultPens(double thickness = 2)
        {
            SetPens(_defaultPenColors, thickness);
        }

        public static void SetPens(List<Color> colors, double thickness)
        {
            Pens.Clear();

            foreach (var c in colors)
                Pens.Add(new Pen(c.ToUInt32(), thickness));

            _penCount = colors.Count;
        }

        public static CommitGraph Parse(List<Commit> commits, bool firstParentOnlyEnabled)
        {
            double UNIT_WIDTH = 12;
            double HALF_WIDTH = 6;
            double UNIT_HEIGHT = 28;
            double HALF_HEIGHT = 14;
            double H_MARGIN = 2;

            var temp = new CommitGraph();
            var unsolved = new List<PathHelper>();
            var ended = new List<PathHelper>();
            var offsetY = -HALF_HEIGHT;
            var colorIdx = 0;

            foreach (var commit in commits)
            {
                var major = null as PathHelper;
                var isMerged = commit.IsMerged;

                // Update current y offset
                offsetY += UNIT_HEIGHT;

                // Find first curves that links to this commit and marks others that links to this commit ended.
                var offsetX = 4 - HALF_WIDTH;
                var maxOffsetOld = unsolved.Count > 0 ? unsolved[^1].LastX : offsetX + UNIT_WIDTH;
                foreach (var l in unsolved)
                {
                    if (l.Next == commit.SHA)
                    {
                        if (major == null)
                        {
                            offsetX += UNIT_WIDTH;
                            major = l;

                            if (commit.Parents.Count > 0)
                            {
                                major.Next = commit.Parents[0];
                                major.Goto(offsetX, offsetY, HALF_HEIGHT);
                            }
                            else
                            {
                                major.End(offsetX, offsetY, HALF_HEIGHT);
                                ended.Add(l);
                            }
                        }
                        else
                        {
                            l.End(major.LastX, offsetY, HALF_HEIGHT);
                            ended.Add(l);
                        }

                        isMerged = isMerged || l.IsMerged;
                        major.IsMerged = isMerged;
                    }
                    else
                    {
                        offsetX += UNIT_WIDTH;
                        l.Pass(offsetX, offsetY, HALF_HEIGHT);
                    }
                }

                // Remove ended curves from unsolved
                foreach (var l in ended)
                    unsolved.Remove(l);
                ended.Clear();

                // Create new curve for branch head
                if (major == null)
                {
                    offsetX += UNIT_WIDTH;

                    if (commit.Parents.Count > 0)
                    {
                        major = new PathHelper(commit.Parents[0], isMerged, colorIdx, new Point(offsetX, offsetY));
                        unsolved.Add(major);
                        temp.Paths.Add(major.Path);
                    }

                    colorIdx = (colorIdx + 1) % _penCount;
                }

                // Calculate link position of this commit.
                Point position = new Point(major?.LastX ?? offsetX, offsetY);
                int dotColor = major?.Path.Color ?? 0;
                Dot anchor = new Dot() { Center = position, Color = dotColor };
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
                        var parentSHA = commit.Parents[j];
                        var parent = unsolved.Find(x => x.Next.Equals(parentSHA, StringComparison.Ordinal));
                        if (parent != null)
                        {
                            // Try to change the merge state of linked graph
                            var l = parent;
                            if (isMerged)
                                l.IsMerged = true;

                            var link = new Link();
                            link.Start = position;
                            link.End = new Point(l.LastX, offsetY + HALF_HEIGHT);
                            link.Control = new Point(link.End.X, link.Start.Y);
                            link.Color = l.Path.Color;

                            temp.Links.Add(link);
                        }
                        else
                        {
                            offsetX += UNIT_WIDTH;

                            // Create new curve for parent commit that not includes before
                            var l = new PathHelper(parentSHA, isMerged, colorIdx, position, new Point(offsetX, position.Y + HALF_HEIGHT));
                            unsolved.Add(l);
                            temp.Paths.Add(l.Path);
                            colorIdx = (colorIdx + 1) % _penCount;
                        }
                    }
                }

                // Margins & merge state (used by Views.Histories).
                commit.IsMerged = isMerged;
                commit.Margin = new Thickness(Math.Max(offsetX, maxOffsetOld) + HALF_WIDTH + H_MARGIN, 0, 0, 0);
            }

            // Deal with curves haven't ended yet.
            for (int i = 0; i < unsolved.Count; i++)
            {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * UNIT_HEIGHT;

                if (path.Path.Points.Count == 1 && Math.Abs(path.Path.Points[0].Y - endY) < 0.0001)
                    continue;

                path.End((i + 0.5) * UNIT_WIDTH + 4, endY + HALF_HEIGHT, HALF_HEIGHT);
            }
            unsolved.Clear();

            return temp;
        }

        private static int _penCount = 0;
        private static readonly List<Color> _defaultPenColors = [
            Colors.Orange,
            Colors.ForestGreen,
            Colors.Gold,
            Colors.Magenta,
            Colors.Red,
            Colors.Gray,
            Colors.Turquoise,
            Colors.Olive,
            Colors.Khaki,
            Colors.Lime,
        ];
    }
}
