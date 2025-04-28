using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class BisectStateIndicator : Control
    {
        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<BisectStateIndicator, IBrush>(nameof(Background), Brushes.Transparent);

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<BisectStateIndicator, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<Models.Bisect> BisectProperty =
            AvaloniaProperty.Register<BisectStateIndicator, Models.Bisect>(nameof(Bisect));

        public Models.Bisect Bisect
        {
            get => GetValue(BisectProperty);
            set => SetValue(BisectProperty, value);
        }

        static BisectStateIndicator()
        {
            AffectsMeasure<BisectStateIndicator>(BisectProperty);
            AffectsRender<BisectStateIndicator>(BackgroundProperty, ForegroundProperty);
        }

        public override void Render(DrawingContext context)
        {
            if (_flags == Models.BisectCommitFlag.None)
                return;

            if (_prefix == null)
            {
                _prefix = LoadIcon("Icons.Bisect");
                _good = LoadIcon("Icons.Check");
                _bad = LoadIcon("Icons.Bad");
            }

            var x = 0.0;

            if (_flags.HasFlag(Models.BisectCommitFlag.Good))
            {
                RenderImpl(context, Brushes.Green, _good, x);
                x += 36;
            }

            if (_flags.HasFlag(Models.BisectCommitFlag.Bad))
                RenderImpl(context, Brushes.Red, _bad, x);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var desiredFlags = Models.BisectCommitFlag.None;
            var desiredWidth = 0.0;
            if (Bisect is { } bisect && DataContext is Models.Commit commit)
            {
                var sha = commit.SHA;
                if (bisect.Goods.Contains(sha))
                {
                    desiredFlags |= Models.BisectCommitFlag.Good;
                    desiredWidth = 36;
                }

                if (bisect.Bads.Contains(sha))
                {
                    desiredFlags |= Models.BisectCommitFlag.Bad;
                    desiredWidth += 36;
                }
            }

            if (desiredFlags != _flags)
            {
                _flags = desiredFlags;
                InvalidateVisual();
            }

            return new Size(desiredWidth, desiredWidth > 0 ? 16 : 0);
        }

        private Geometry LoadIcon(string key)
        {
            var geo = this.FindResource(key) as StreamGeometry;
            var drawGeo = geo!.Clone();
            var iconBounds = drawGeo.Bounds;
            var translation = Matrix.CreateTranslation(-(Vector)iconBounds.Position);
            var scale = Math.Min(10.0 / iconBounds.Width, 10.0 / iconBounds.Height);
            var transform = translation * Matrix.CreateScale(scale, scale);
            if (drawGeo.Transform == null || drawGeo.Transform.Value == Matrix.Identity)
                drawGeo.Transform = new MatrixTransform(transform);
            else
                drawGeo.Transform = new MatrixTransform(drawGeo.Transform.Value * transform);

            return drawGeo;
        }

        private void RenderImpl(DrawingContext context, IBrush brush, Geometry icon, double x)
        {
            var entireRect = new RoundedRect(new Rect(x, 0, 32, 16), new CornerRadius(2));
            var stateRect = new RoundedRect(new Rect(x + 16, 0, 16, 16), new CornerRadius(0, 2, 2, 0));
            context.DrawRectangle(Background, new Pen(brush), entireRect);
            using (context.PushOpacity(.2))
                context.DrawRectangle(brush, null, stateRect);
            context.DrawLine(new Pen(brush), new Point(x + 16, 0), new Point(x + 16, 16));

            using (context.PushTransform(Matrix.CreateTranslation(x + 3, 3)))
                context.DrawGeometry(Foreground, null, _prefix);

            using (context.PushTransform(Matrix.CreateTranslation(x + 19, 3)))
                context.DrawGeometry(Foreground, null, icon);
        }

        private Geometry _prefix = null;
        private Geometry _good = null;
        private Geometry _bad = null;
        private Models.BisectCommitFlag _flags = Models.BisectCommitFlag.None;
    }
}
