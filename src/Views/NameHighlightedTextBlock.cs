using System;
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

        public NameHighlightedTextBlock(string nameKey, params object[] args)
        {
            Text = App.Text(nameKey, args);
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var text = Text;
            if (string.IsNullOrEmpty(text)) return base.MeasureOverride(availableSize);

            var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
            var formatted = new FormattedText(
                    Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    FontSize,
                    Foreground);

            return new Size(formatted.Width - 16, formatted.Height);
        }

        public override void Render(DrawingContext context)
        {
            var text = Text;
            if (string.IsNullOrEmpty(text)) return;

            var normalTypeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
            //var highlightTypeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold, FontStretch.Normal);
            var underlinePen = new Pen(Foreground, 1);
            var offsetX = 0.0;

            var parts = text.Split('$', StringSplitOptions.None);
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
                    //isName ? highlightTypeface : normalTypeface,
                    normalTypeface,
                    FontSize,
                    Foreground);

                if (isName)
                {
                    var lineY = formatted.Baseline + 2;
                    offsetX += 4;
                    context.DrawText(formatted, new Point(offsetX, 0));
                    context.DrawLine(underlinePen, new Point(offsetX, lineY), new Point(offsetX + formatted.Width, lineY));
                    offsetX += formatted.Width + 4;
                }
                else
                {
                    context.DrawText(formatted, new Point(offsetX, 0));
                    offsetX += formatted.Width;
                }

                isName = !isName;
            }
        }
    }
}