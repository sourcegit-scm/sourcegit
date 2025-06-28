using System;
using System.Globalization;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public partial class StashSubjectPresenter : Control
    {
        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<StashSubjectPresenter, FontFamily>(nameof(FontFamily));

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           AvaloniaProperty.Register<StashSubjectPresenter, double>(nameof(FontSize), 13);

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<StashSubjectPresenter, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> PrefixBackgroundProperty =
            AvaloniaProperty.Register<StashSubjectPresenter, IBrush>(nameof(PrefixBackground), Brushes.Transparent);

        public IBrush PrefixBackground
        {
            get => GetValue(PrefixBackgroundProperty);
            set => SetValue(PrefixBackgroundProperty, value);
        }

        public static readonly StyledProperty<string> SubjectProperty =
            AvaloniaProperty.Register<StashSubjectPresenter, string>(nameof(Subject));

        public string Subject
        {
            get => GetValue(SubjectProperty);
            set => SetValue(SubjectProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var subject = Subject;
            if (string.IsNullOrEmpty(subject))
                return;

            var typeface = new Typeface(FontFamily);
            var foreground = Foreground;
            var x = 0.0;
            var h = Bounds.Height;
            FormattedText prefix = null;

            var match = REG_KEYWORD_ON().Match(subject);
            if (match.Success)
            {
                prefix = new FormattedText(match.Groups[1].Value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, foreground);
                subject = subject.Substring(match.Length);
            }
            else
            {
                match = REG_KEYWORD_WIP().Match(subject);
                if (match.Success)
                {
                    prefix = new FormattedText($"WIP | {match.Groups[1].Value}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, foreground);
                    subject = subject.Substring(match.Length);
                }
            }

            if (prefix != null)
            {
                var pw = prefix.WidthIncludingTrailingWhitespace;
                var ph = prefix.Height;
                var bh = ph + 4;
                var bw = pw + 12;

                context.DrawRectangle(PrefixBackground, null, new RoundedRect(new Rect(0, (h - bh) * 0.5, bw, bh), new CornerRadius(bh * 0.5)));
                context.DrawText(prefix, new Point(6, (h - ph) * 0.5));
                x = bw + 4;
            }

            var body = new FormattedText(subject, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, foreground);
            context.DrawText(body, new Point(x, (h - body.Height) * 0.5));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SubjectProperty ||
                change.Property == FontFamilyProperty ||
                change.Property == FontSizeProperty ||
                change.Property == ForegroundProperty ||
                change.Property == PrefixBackgroundProperty)
            {
                InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var typeface = new Typeface(FontFamily);
            var test = new FormattedText("fgl|", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, Brushes.White);
            var h = Math.Max(18, test.Height);
            return new Size(availableSize.Width, h);
        }

        [GeneratedRegex(@"^On ([^\s]+)\: ")]
        private static partial Regex REG_KEYWORD_ON();

        [GeneratedRegex(@"^WIP on ([^\s]+)\: ([a-f0-9]{6,40}) ")]
        private static partial Regex REG_KEYWORD_WIP();
    }
}
