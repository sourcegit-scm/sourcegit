using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public record CommitBranchConnection(Point Start, Point End, IBrush Brush, bool IsActive, double TextGapWidth = 0);

    public class CommitBranchConnections : Control
    {
        public IReadOnlyList<CommitBranchConnection> Connections => _connections;

        public void UpdateConnections(List<CommitBranchConnection> connections)
        {
            if (_connections.Count == connections.Count)
            {
                var unchanged = true;
                for (var i = 0; i < connections.Count; i++)
                    unchanged &= _connections[i] == connections[i];
                if (unchanged)
                    return;
            }

            _connections = connections;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            using (context.PushClip(new Rect(Bounds.Size)))
            {
                foreach (var connection in _connections)
                {
                    using (context.PushOpacity(connection.IsActive ? 1 : 0.7))
                    {
                        var pen = new Pen(connection.Brush, 1);
                        if (connection.TextGapWidth > 0)
                        {
                            // Leave the plain-text branch count readable between the badge and the line.
                            context.DrawLine(pen, connection.Start, new Point(System.Math.Min(connection.Start.X + 2, connection.End.X), connection.Start.Y));
                            var resumeX = connection.Start.X + connection.TextGapWidth;
                            if (resumeX < connection.End.X)
                                context.DrawLine(pen, new Point(resumeX, connection.Start.Y), connection.End);
                        }
                        else
                        {
                            context.DrawLine(pen, connection.Start, connection.End);
                        }
                    }
                }
            }
        }

        private List<CommitBranchConnection> _connections = [];
    }
}
