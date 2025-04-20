using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class NameHighlightedTextBlock : Control
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<NameHighlightedTextBlock, string>(nameof(Text));

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<NameHighlightedTextBlock>();

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           TextBlock.FontSizeProperty.AddOwner<NameHighlightedTextBlock>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            TextBlock.ForegroundProperty.AddOwner<NameHighlightedTextBlock>();

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        static NameHighlightedTextBlock()
        {
            AffectsMeasure<NameHighlightedTextBlock>(TextProperty);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var text = Text;
            if (string.IsNullOrEmpty(text))
                return base.MeasureOverride(availableSize);

            var typeface = new Typeface(FontFamily);
            var formatted = new FormattedText(
                    Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    FontSize,
                    Foreground);

            return new Size(formatted.Width, formatted.Height);
        }

        public override void Render(DrawingContext context)
        {
            var text = Text;
            if (string.IsNullOrEmpty(text))
                return;

            var normalTypeface = new Typeface(FontFamily);
            var underlinePen = new Pen(Foreground);
            var offsetX = 0.0;

            var parts = text.Split('$');
            var isName = false;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    isName = !isName;
                    continue;
                }

                var formatted = new FormattedText(
                    part,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    normalTypeface,
                    FontSize,
                    Foreground);

                if (isName)
                {
                    var lineY = formatted.Baseline + 2;
                    context.DrawText(formatted, new Point(offsetX, 0));
                    context.DrawLine(underlinePen, new Point(offsetX, lineY), new Point(offsetX + formatted.Width, lineY));
                    offsetX += formatted.WidthIncludingTrailingWhitespace;
                }
                else
                {
                    context.DrawText(formatted, new Point(offsetX, 0));
                    offsetX += formatted.WidthIncludingTrailingWhitespace;
                }

                isName = !isName;
            }
        }
    }
}
