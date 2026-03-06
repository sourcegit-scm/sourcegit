using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class WorktreeDepthIcon : Control
    {
        public static readonly StyledProperty<IBrush> BrushProperty =
            AvaloniaProperty.Register<WorktreeDepthIcon, IBrush>(nameof(Brush), Brushes.Transparent);

        public IBrush Brush
        {
            get => GetValue(BrushProperty);
            set => SetValue(BrushProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            if (DataContext is ViewModels.Worktree wt)
            {
                if (wt.IsMain)
                    return;

                var pen = new Pen(Brush);
                var h = Bounds.Height;
                var halfH = h * 0.5;

                if (wt.IsLast)
                    context.DrawLine(pen, new Point(12.5, 0), new Point(12.5, halfH));
                else
                    context.DrawLine(pen, new Point(12.5, 0), new Point(12.5, h));

                context.DrawLine(pen, new Point(12.5, halfH), new Point(18, halfH));
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (DataContext is ViewModels.Worktree wt)
            {
                if (wt.IsMain)
                    return new Size(0, 0);

                return new Size(18, availableSize.Height);
            }

            return new Size(0, 0);
        }
    }
}
