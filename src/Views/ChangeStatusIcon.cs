using System;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace SourceGit.Views
{
    public class ChangeStatusIcon : Control
    {
        private static readonly string[] INDICATOR = ["?", "M", "T", "+", "−", "➜", "❏", "?", "!"];
        private static readonly Color[] COLOR =
        [
            Colors.Transparent,
            Colors.Goldenrod,
            Colors.Goldenrod,
            Colors.LimeGreen,
            Colors.Tomato,
            Colors.Orchid,
            Colors.Goldenrod,
            Colors.SkyBlue,
            Colors.OrangeRed,
        ];

        public static readonly DirectProperty<ChangeStatusIcon, bool> IsUnstagedChangeProperty =
            AvaloniaProperty.RegisterDirect<ChangeStatusIcon, bool>(
                nameof(IsUnstagedChange),
                static o => o.IsUnstagedChange,
                static (o, v) => o.IsUnstagedChange = v);

        public bool IsUnstagedChange
        {
            get => _isUnstagedChange;
            set => SetAndRaise(IsUnstagedChangeProperty, ref _isUnstagedChange, value);
        }

        public static readonly DirectProperty<ChangeStatusIcon, Models.Change> ChangeProperty =
            AvaloniaProperty.RegisterDirect<ChangeStatusIcon, Models.Change>(
                nameof(Change),
                static o => o.Change,
                static (o, v) => o.Change = v);

        public Models.Change Change
        {
            get => _change;
            set => SetAndRaise(ChangeProperty, ref _change, value);
        }

        public override void Render(DrawingContext context)
        {
            if (_change == null || Bounds.Width <= 0)
                return;

            var typeface = new Typeface("fonts:SourceGit#JetBrains Mono NL");
            var idx = (int)(_isUnstagedChange ? _change.WorkTree : _change.Index);
            var indicator = INDICATOR[idx];
            var color = COLOR[idx];
            var hsl = color.ToHsl();
            var color2 = ActualThemeVariant == ThemeVariant.Dark
                ? new HslColor(hsl.A, hsl.H, hsl.S, hsl.L - 0.1).ToRgb()
                : new HslColor(hsl.A, hsl.H, hsl.S, hsl.L + 0.1).ToRgb();

            var background = new LinearGradientBrush
            {
                GradientStops = [new GradientStop(color, 0), new GradientStop(color2, 1)],
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            };

            var txt = new FormattedText(
                indicator,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                Bounds.Width * 0.8,
                Brushes.White);

            var corner = (float)Math.Max(2, Bounds.Width / 16);
            var textOrigin = new Point((Bounds.Width - txt.Width) * 0.5, (Bounds.Height - txt.Height) * 0.5);
            context.DrawRectangle(background, null, new Rect(0, 0, Bounds.Width, Bounds.Height), corner, corner);
            context.DrawText(txt, textOrigin);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsUnstagedChangeProperty || change.Property == ChangeProperty)
                InvalidateVisual();
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
                InvalidateVisual();
        }

        private bool _isUnstagedChange = false;
        private Models.Change _change = null;
    }
}
