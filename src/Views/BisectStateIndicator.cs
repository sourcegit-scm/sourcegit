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
            switch (_flag)
            {
                case Models.BisectCommitFlag.Good:
                    RenderImpl(context, Brushes.Green, "Icons.Good");
                    break;
                case Models.BisectCommitFlag.Bad:
                    RenderImpl(context, Brushes.Red, "Icons.Bad");
                    break;
                case Models.BisectCommitFlag.Skipped:
                    RenderImpl(context, Brushes.Gray, "Icons.Skip");
                    break;
                default:
                    break;
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var flag = Models.BisectCommitFlag.None;

            if (Bisect is { } bisect && DataContext is Models.Commit commit)
            {
                var sha = commit.SHA;
                if (bisect.Bads.Contains(sha))
                    flag = Models.BisectCommitFlag.Bad;
                else if (bisect.Goods.Contains(sha))
                    flag = Models.BisectCommitFlag.Good;
                else if (bisect.Skipped.Contains(sha))
                    flag = Models.BisectCommitFlag.Skipped;
            }

            if (flag != _flag)
            {
                _flag = flag;
                InvalidateVisual();
            }

            if (flag == Models.BisectCommitFlag.None)
                return new Size(0, 0);

            return new Size(36, 16);
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

        private void RenderImpl(DrawingContext context, IBrush brush, string iconKey)
        {
            var prefix = LoadIcon("Icons.Bisect");
            var icon = LoadIcon(iconKey);
            var entireRect = new RoundedRect(new Rect(0, 0, 32, 16), new CornerRadius(2));
            var stateRect = new RoundedRect(new Rect(16, 0, 16, 16), new CornerRadius(0, 2, 2, 0));
            context.DrawRectangle(Background, new Pen(brush), entireRect);
            using (context.PushOpacity(.2))
                context.DrawRectangle(brush, null, stateRect);
            context.DrawLine(new Pen(brush), new Point(16, 0), new Point(16, 16));

            using (context.PushTransform(Matrix.CreateTranslation(3, 3)))
                context.DrawGeometry(Foreground, null, prefix);

            using (context.PushTransform(Matrix.CreateTranslation(19, 3)))
                context.DrawGeometry(Foreground, null, icon);
        }

        private Models.BisectCommitFlag _flag = Models.BisectCommitFlag.None;
    }
}
