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
            public double EndY;
            public Path Path;

            public PathHelper(string next, bool isMerged, int color, Point start)
            {
                Next = next;
                IsMerged = isMerged;
                LastX = start.X;
                LastY = start.Y;
                EndY = LastY;

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
                EndY = LastY;

                Path = new Path();
                Path.Color = color;
                Path.Points.Add(start);
                Path.Points.Add(to);
            }

            public void Add(double x, double y, double halfHeight, bool isEnd = false)
            {
                if (x > LastX)
                {
                    Add(new Point(LastX, LastY));
                    Add(new Point(x, y - halfHeight));
                    if (isEnd)
                        Add(new Point(x, y));
                }
                else if (x < LastX)
                {
                    if (y > LastY + halfHeight)
                        Add(new Point(LastX, LastY + halfHeight));
                    Add(new Point(x, y));
                }
                else if (isEnd)
                {
                    Add(new Point(x, y));
                }

                LastX = x;
                LastY = y;
            }

            private void Add(Point p)
            {
                if (EndY < p.Y)
                {
                    Path.Points.Add(p);
                    EndY = p.Y;
                }
            }
        }

        public class Link
        {
            public Point Start;
            public Point Control;
            public Point End;
            public int Color;
        }

        public class Dot
        {
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

        public static void SetDefaultPens()
        {
            SetPenColors(_defaultPenColors);
        }

        public static void SetPenColors(List<Color> colors)
        {
            Pens.Clear();

            foreach (var c in colors)
                Pens.Add(new Pen(c.ToUInt32(), 2));

            _penCount = colors.Count;
        }

        public static CommitGraph Parse(List<Commit> commits)
        {
            double UNIT_WIDTH = 12;
            double HALF_WIDTH = 6;
            double UNIT_HEIGHT = 28;
            double HALF_HEIGHT = 14;

            var temp = new CommitGraph();
            var unsolved = new List<PathHelper>();
            var mapUnsolved = new Dictionary<string, PathHelper>();
            var ended = new List<PathHelper>();
            var offsetY = -HALF_HEIGHT;
            var colorIdx = 0;

            foreach (var commit in commits)
            {
                var major = null as PathHelper;
                var isMerged = commit.IsMerged;
                var oldCount = unsolved.Count;

                // Update current y offset
                offsetY += UNIT_HEIGHT;

                // Find first curves that links to this commit and marks others that links to this commit ended.
                double offsetX = -HALF_WIDTH;
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
                                if (!mapUnsolved.ContainsKey(major.Next))
                                    mapUnsolved.Add(major.Next, major);
                            }
                            else
                            {
                                major.Next = "ENDED";
                                ended.Add(l);
                            }

                            major.Add(offsetX, offsetY, HALF_HEIGHT);
                        }
                        else
                        {
                            ended.Add(l);
                        }

                        isMerged = isMerged || l.IsMerged;
                    }
                    else
                    {
                        if (!mapUnsolved.ContainsKey(l.Next))
                            mapUnsolved.Add(l.Next, l);
                        offsetX += UNIT_WIDTH;
                        l.Add(offsetX, offsetY, HALF_HEIGHT);
                    }
                }

                // Create new curve for branch head
                if (major == null && commit.Parents.Count > 0)
                {
                    offsetX += UNIT_WIDTH;
                    major = new PathHelper(commit.Parents[0], isMerged, colorIdx, new Point(offsetX, offsetY));
                    unsolved.Add(major);
                    temp.Paths.Add(major.Path);
                    colorIdx = (colorIdx + 1) % _penCount;
                }

                // Calculate link position of this commit.
                Point position = new Point(offsetX, offsetY);
                if (major != null)
                {
                    major.IsMerged = isMerged;
                    position = new Point(major.LastX, offsetY);
                    temp.Dots.Add(new Dot() { Center = position, Color = major.Path.Color });
                }
                else
                {
                    temp.Dots.Add(new Dot() { Center = position, Color = 0 });
                }

                // Deal with parents
                for (int j = 1; j < commit.Parents.Count; j++)
                {
                    var parent = commit.Parents[j];
                    if (mapUnsolved.TryGetValue(parent, out var value))
                    {
                        var l = value;
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
                        var l = new PathHelper(commit.Parents[j], isMerged, colorIdx, position, new Point(offsetX, position.Y + HALF_HEIGHT));
                        unsolved.Add(l);
                        temp.Paths.Add(l.Path);
                        colorIdx = (colorIdx + 1) % _penCount;
                    }
                }

                // Remove ended curves from unsolved
                foreach (var l in ended)
                {
                    l.Add(position.X, position.Y, HALF_HEIGHT, true);
                    unsolved.Remove(l);
                }

                // Margins & merge state (used by datagrid).
                commit.IsMerged = isMerged;
                commit.Margin = new Thickness(Math.Max(offsetX + HALF_WIDTH, oldCount * UNIT_WIDTH), 0, 0, 0);

                // Clean up
                ended.Clear();
                mapUnsolved.Clear();
            }

            // Deal with curves haven't ended yet.
            for (int i = 0; i < unsolved.Count; i++)
            {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * UNIT_HEIGHT;

                if (path.Path.Points.Count == 1 && path.Path.Points[0].Y == endY)
                    continue;
                path.Add((i + 0.5) * UNIT_WIDTH, endY + HALF_HEIGHT, HALF_HEIGHT, true);
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
