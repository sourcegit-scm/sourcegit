using System;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    // Measure against the viewport while allowing an exceptionally long filename
    // to contribute its full width to the horizontal scroll extent.
    public class ChangeListRow : Grid
    {
        public static readonly StyledProperty<bool> TrimLeadingPathsProperty =
            AvaloniaProperty.Register<ChangeListRow, bool>(nameof(TrimLeadingPaths), true);

        public bool TrimLeadingPaths
        {
            get => GetValue(TrimLeadingPathsProperty);
            set => SetValue(TrimLeadingPathsProperty, value);
        }
        public static readonly StyledProperty<double> ViewportWidthProperty =
            AvaloniaProperty.Register<ChangeListRow, double>(nameof(ViewportWidth), double.PositiveInfinity);

        public double ViewportWidth
        {
            get => GetValue(ViewportWidthProperty);
            set => SetValue(ViewportWidthProperty, value);
        }

        static ChangeListRow()
        {
            AffectsMeasure<ChangeListRow>(ViewportWidthProperty, TrimLeadingPathsProperty);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _scrollViewer = this.FindAncestorOfType<ScrollViewer>();
            if (_scrollViewer != null)
            {
                _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
                SetCurrentValue(ViewportWidthProperty, _scrollViewer.Viewport.Width);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_scrollViewer != null)
                _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
            _scrollViewer = null;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnScrollViewerPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.ViewportProperty)
                SetCurrentValue(ViewportWidthProperty, _scrollViewer.Viewport.Width);
        }

        private ScrollViewer _scrollViewer;
        protected override Size MeasureOverride(Size availableSize)
        {
            if (!TrimLeadingPaths)
                return base.MeasureOverride(availableSize);

            var size = base.MeasureOverride(new Size(Math.Min(availableSize.Width, Math.Max(0, ViewportWidth)), availableSize.Height));
            var minimum = 0.0;
            foreach (var child in Children)
            {
                if (child.IsVisible)
                    minimum += child is ChangePathTextBlock path ? path.MinWidth + path.Margin.Left + path.Margin.Right : child.DesiredSize.Width;
            }
            return new Size(Math.Max(size.Width, minimum), size.Height);
        }
    }

    public class ChangePathTextBlock : TextBlock
    {
        public static readonly StyledProperty<string> PathProperty =
            AvaloniaProperty.Register<ChangePathTextBlock, string>(nameof(Path), string.Empty);

        public string Path
        {
            get => GetValue(PathProperty);
            set => SetValue(PathProperty, value);
        }

        static ChangePathTextBlock()
        {
            AffectsMeasure<ChangePathTextBlock>(PathProperty);
        }

        private Size MeasureText(string text)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Foreground);
            return new Size(formatted.WidthIncludingTrailingWhitespace, formatted.Height);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var path = Path ?? string.Empty;
            var fileStart = path.LastIndexOf('/') + 1;
            Text = (fileStart > 0 ? "…" : string.Empty) + path[fileStart..];
            MinWidth = MeasureText(Text).Width;
            Text = path;
            var fullSize = MeasureText(Text);
            var separator = path.LastIndexOf('/');
            if (fullSize.Width <= availableSize.Width || separator < 0)
                return base.MeasureOverride(Size.Infinity);

            // Search text-element boundaries so trimming cannot split a surrogate
            // pair or a combining sequence. Only the directory may be removed.
            var starts = StringInfo.ParseCombiningCharacters(path[..(separator + 1)]);
            var low = 1;
            var high = starts.Length;
            while (low < high)
            {
                var mid = low + (high - low) / 2;
                Text = "…" + path[starts[mid]..];
                if (MeasureText(Text).Width <= availableSize.Width)
                    high = mid;
                else
                    low = mid + 1;
            }

            Text = "…" + path[(low < starts.Length ? starts[low] : separator + 1)..];
            return base.MeasureOverride(Size.Infinity);
        }
    }
}
