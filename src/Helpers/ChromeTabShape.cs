using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SourceGit.Helpers {

    /// <summary>
    ///     Chrome like tab shape
    /// </summary>
    public class ChromeTabShape : Shape {
        protected override Geometry DefiningGeometry => MakeGeometry();

        public ChromeTabShape() {
            Stretch = Stretch.None;
            StrokeThickness = 0;
        }

        private Geometry MakeGeometry() {
            var geo = new StreamGeometry();
            var cornerSize = new Size(4, 4);
            var cornerAngle = Math.PI / 2; 
            using (var ctx = geo.Open()) {
                ctx.BeginFigure(new Point(-5.1, ActualHeight), true, true);
                ctx.ArcTo(new Point(-1.1, ActualHeight - 4), cornerSize, cornerAngle, false, SweepDirection.Counterclockwise, false, true);
                ctx.LineTo(new Point(-1.1, 4), false, true);
                ctx.ArcTo(new Point(2.9, 0), cornerSize, cornerAngle, false, SweepDirection.Clockwise, false, true);
                ctx.LineTo(new Point(ActualWidth - 2.9, 0), false, true);
                ctx.ArcTo(new Point(ActualWidth + 1.1, 4), cornerSize, cornerAngle, false, SweepDirection.Clockwise, false, true);
                ctx.LineTo(new Point(ActualWidth + 1.1, ActualHeight - 4), false, true);
                ctx.ArcTo(new Point(ActualWidth + 5.1, ActualHeight), cornerSize, cornerAngle, false, SweepDirection.Counterclockwise, false, true);
            }

            geo.Freeze();
            return geo;
        }
    }
}
