using System;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class ChangeStatusIcon : Control
    {
        private static readonly IBrush[] BACKGROUNDS = [
            Brushes.Transparent,
            new LinearGradientBrush
            {
                GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(238, 160, 14), 0), new GradientStop(Color.FromRgb(228, 172, 67), 1) },
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            },
            new LinearGradientBrush
            {
                GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(47, 185, 47), 0), new GradientStop(Color.FromRgb(75, 189, 75), 1) },
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            },
            new LinearGradientBrush
            {
                GradientStops = new GradientStops() { new GradientStop(Colors.Tomato, 0), new GradientStop(Color.FromRgb(252, 165, 150), 1) },
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            },
            new LinearGradientBrush
            {
                GradientStops = new GradientStops() { new GradientStop(Colors.Orchid, 0), new GradientStop(Color.FromRgb(248, 161, 245), 1) },
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            },
            new LinearGradientBrush
            {
                GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(238, 160, 14), 0), new GradientStop(Color.FromRgb(228, 172, 67), 1) },
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            },
            new LinearGradientBrush
            {
                GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(238, 160, 14), 0), new GradientStop(Color.FromRgb(228, 172, 67), 1) },
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            },
            new LinearGradientBrush
            {
                GradientStops = new GradientStops() { new GradientStop(Color.FromRgb(47, 185, 47), 0), new GradientStop(Color.FromRgb(75, 189, 75), 1) },
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            },
        ];

        private static readonly string[] INDICATOR = ["?", "±", "+", "−", "➜", "❏", "U", "★"];

        public static readonly StyledProperty<bool> IsUnstagedChangeProperty =
            AvaloniaProperty.Register<ChangeStatusIcon, bool>(nameof(IsUnstagedChange));

        public bool IsUnstagedChange
        {
            get => GetValue(IsUnstagedChangeProperty);
            set => SetValue(IsUnstagedChangeProperty, value);
        }

        public static readonly StyledProperty<Models.Change> ChangeProperty =
            AvaloniaProperty.Register<ChangeStatusIcon, Models.Change>(nameof(Change));

        public Models.Change Change
        {
            get => GetValue(ChangeProperty);
            set => SetValue(ChangeProperty, value);
        }

        static ChangeStatusIcon()
        {
            AffectsRender<ChangeStatusIcon>(IsUnstagedChangeProperty, ChangeProperty);
        }

        public override void Render(DrawingContext context)
        {
            if (Change == null || Bounds.Width <= 0)
                return;

            var typeface = new Typeface("fonts:SourceGit#JetBrains Mono");

            IBrush background;
            string indicator;
            if (IsUnstagedChange)
            {
                if (Change.IsConflit)
                {
                    background = Brushes.OrangeRed;
                    indicator = "!";
                }
                else
                {
                    background = BACKGROUNDS[(int)Change.WorkTree];
                    indicator = INDICATOR[(int)Change.WorkTree];
                }
            }
            else
            {
                background = BACKGROUNDS[(int)Change.Index];
                indicator = INDICATOR[(int)Change.Index];
            }

            var txt = new FormattedText(
                indicator,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                Bounds.Width * 0.8,
                Brushes.White);

            float corner = (float)Math.Max(2, Bounds.Width / 16);
            Point textOrigin = new Point((Bounds.Width - txt.Width) * 0.5, (Bounds.Height - txt.Height) * 0.5);
            context.DrawRectangle(background, null, new Rect(0, 0, Bounds.Width, Bounds.Height), corner, corner);
            context.DrawText(txt, textOrigin);
        }
    }
}
