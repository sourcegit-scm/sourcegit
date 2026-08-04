using System;
using System.Globalization;
using System.Text;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class BinaryFileAddressTextBox : TextBox
    {
        protected override Type StyleKeyOverride => typeof(TextBox);

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            PastingFromClipboard += OnPastingFromClipboard;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            PastingFromClipboard -= OnPastingFromClipboard;
            base.OnUnloaded(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;

            var builder = new StringBuilder(e.Text.Length);
            var chars = e.Text.ToCharArray();
            foreach (var ch in chars)
            {
                if ((ch >= '0' && ch <= '9') ||
                    (ch >= 'a' && ch <= 'f') ||
                    (ch >= 'A' && ch <= 'F'))
                    builder.Append(ch);

                if (builder.Length >= 8)
                    break;
            }

            e.Text = builder.ToString();
            base.OnTextInput(e);
        }

        private async void OnPastingFromClipboard(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    var text = await clipboard.TryGetTextAsync();
                    if (!string.IsNullOrEmpty(text))
                        OnTextInput(new TextInputEventArgs() { Text = text });
                }
            }
            catch
            {
                // Ignore exceptions
            }
        }
    }

    public class HexViewer : Control
    {
        public static readonly DirectProperty<HexViewer, long> OffsetProperty =
            AvaloniaProperty.RegisterDirect<HexViewer, long>(
                nameof(Offset),
                static o => o.Offset,
                static (o, v) => o.Offset = v);

        public long Offset
        {
            get => _offset;
            set
            {
                if (SetAndRaise(OffsetProperty, ref _offset, value))
                    InvalidateVisual();
            }
        }

        public static readonly StyledProperty<IBrush> HeaderForegroundProperty =
            AvaloniaProperty.Register<HexViewer, IBrush>(nameof(HeaderForeground), Brushes.White);

        public IBrush HeaderForeground
        {
            get => GetValue(HeaderForegroundProperty);
            set => SetValue(HeaderForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<HexViewer, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public const double HEADER_HEIGHT = 24.0;
        public const double LINE_HEIGHT = 18.0;
        public const double BYTE_CELL_WIDTH = 22.0;
        public const int BYTES_PER_LINE = 16;
        public const int BYTES_PER_LINE_HALF = 8;
        public const double FONT_SIZE = 13;

        public void SetHighlightedIndex(long idx)
        {
            if (idx == _highlightedIdx)
                return;

            _highlightedIdx = idx;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (DataContext is not ViewModels.BinaryFile vm)
                return;

            context.FillRectangle(Brushes.Transparent, new Rect(0, 0, Bounds.Width, Bounds.Height));

            var typeface = new Typeface("fonts:SourceGit#JetBrains Mono NL");
            var foreground = Foreground;
            var headerForeground = HeaderForeground;
            var highlightedBackground = new SolidColorBrush(Colors.Red, 0.2);
            var highlightedBorder = new Pen(Brushes.Red, 0.5);

            var test = new FormattedText(
                "F",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                FONT_SIZE,
                foreground);

            var addrColumnWidth = test.Width * 9;
            var x = addrColumnWidth + 16.0;

            _charWidth = test.Width;
            _first8BytesX = x;
            _second8BytesX = x + (BYTE_CELL_WIDTH * BYTES_PER_LINE_HALF) + 16.0;
            _asciiColumnX = _second8BytesX + (BYTE_CELL_WIDTH * BYTES_PER_LINE_HALF) + 16.0;

            for (int i = 0; i < BYTES_PER_LINE; i++)
            {
                var hexLabel = new FormattedText(
                    i.ToString("X1"),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    FONT_SIZE,
                    headerForeground);

                var startX = x + (BYTE_CELL_WIDTH - hexLabel.Width) * 0.5;
                var startY = (HEADER_HEIGHT - hexLabel.Height) * 0.5;
                context.DrawText(hexLabel, new Point(startX, startY));
                x += BYTE_CELL_WIDTH;

                if (i == 7 || i == 15)
                    x += 16.0;
            }

            var dataSize = (long)Math.Ceiling((Bounds.Height - HEADER_HEIGHT) / LINE_HEIGHT) * BYTES_PER_LINE;
            var data = vm.Read(_offset, dataSize);
            if (data.Count == 0)
                return;

            x = 0.0;
            var y = HEADER_HEIGHT - LINE_HEIGHT;
            var columnIdx = 0;
            for (var i = 0; i < data.Count; i++)
            {
                if (i % BYTES_PER_LINE == 0)
                {
                    columnIdx = 0;
                    x = 0;
                    y += LINE_HEIGHT;

                    var addr = new FormattedText(
                        (_offset + i).ToString("X8") + ":",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        FONT_SIZE,
                        headerForeground);

                    context.DrawText(addr, new Point(x, y + (LINE_HEIGHT - addr.Height) * 0.5));
                    x += addrColumnWidth + 16.0;
                }

                var isHighlighted = (i + _offset) == _highlightedIdx;
                var hex = data[i];
                var hexLabel = new FormattedText(
                    hex.ToString("X2"),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    FONT_SIZE,
                    hex == 0 ? Brushes.Gray : foreground);

                var ch = (char)hex;
                var isPrintable = (hex >= 0x20 && hex <= 0x7E);
                var chLabel = new FormattedText(
                    isPrintable ? ch.ToString() : ".",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    FONT_SIZE,
                    isPrintable ? Brushes.Green : Brushes.Gray);

                var hexX = x + (BYTE_CELL_WIDTH - hexLabel.Width) * 0.5;
                var hexY = y + (LINE_HEIGHT - hexLabel.Height) * 0.5;
                if (isHighlighted)
                    context.DrawRectangle(highlightedBackground, highlightedBorder, new Rect(x, y, BYTE_CELL_WIDTH, LINE_HEIGHT), 2, 2);

                context.DrawText(hexLabel, new Point(hexX, hexY));

                var asciiX = _asciiColumnX + columnIdx * test.Width;
                var asciiY = y + (LINE_HEIGHT - chLabel.Height) * 0.5;
                if (isHighlighted)
                    context.DrawRectangle(highlightedBackground, highlightedBorder, new Rect(asciiX, y, test.Width, LINE_HEIGHT), 2, 2);

                context.DrawText(chLabel, new Point(asciiX, asciiY));

                x += BYTE_CELL_WIDTH;

                if (columnIdx == 7 || columnIdx == 15)
                    x += 16.0;

                columnIdx++;
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (DataContext is not ViewModels.BinaryFile vm)
                return;

            var pos = e.GetPosition(this);
            var testX = pos.X;
            var testY = pos.Y - HEADER_HEIGHT;
            if (testX <= _first8BytesX || testY <= 0)
                return;

            long columnIdx = -1;

            if (testX >= _asciiColumnX)
            {
                var column = (testX - _asciiColumnX) / _charWidth;
                if (column < 16)
                    columnIdx = (long)Math.Floor(column);
            }
            else if (testX >= _second8BytesX)
            {
                var column = (testX - _second8BytesX) / BYTE_CELL_WIDTH;
                if (column < 8)
                    columnIdx = (long)Math.Floor(column) + 8;
            }
            else
            {
                var column = (testX - _first8BytesX) / BYTE_CELL_WIDTH;
                if (column < 8)
                    columnIdx = (long)Math.Floor(column);
            }

            if (columnIdx < 0)
                return;

            var rowIdx = (long)Math.Floor(testY / LINE_HEIGHT);
            var idx = _offset + rowIdx * BYTES_PER_LINE + columnIdx;
            if (idx >= vm.FileSize)
                return;

            SetHighlightedIndex(idx);
        }

        private long _offset = 0;
        private double _charWidth = 0;
        private double _first8BytesX = 0;
        private double _second8BytesX = 0;
        private double _asciiColumnX = 0;
        private long _highlightedIdx = -1;
    }

    public partial class BinaryFileViewer : UserControl
    {
        public BinaryFileViewer()
        {
            InitializeComponent();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            _cancellation?.Cancel();

            if (Content is ViewModels.BinaryFile old)
            {
                Content = null;
                old.Dispose();
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            var old = Content;
            Content = DataContext;
            _cancellation?.Cancel();

            if (old is ViewModels.BinaryFile oldFile)
                oldFile.Dispose();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (Content is not ViewModels.BinaryFile)
                return;

            // TextBox itself contains a ScrollBar.
            var scroller = this.FindDescendantOfType<ScrollBar>(false, s => s.Name.Equals("HexViewerScroller", StringComparison.Ordinal));
            if (scroller == null)
                return;

            scroller.Value -= e.Delta.Y * HexViewer.LINE_HEIGHT;
        }

        private void OnScrollBarValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is ScrollBar scroller)
            {
                var viewer = this.FindDescendantOfType<HexViewer>();
                if (viewer != null)
                    viewer.Offset = (long)(Math.Ceiling(scroller.Value / HexViewer.LINE_HEIGHT) * HexViewer.BYTES_PER_LINE);
            }
        }

        private void OnScrollBarSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is ScrollBar { DataContext: ViewModels.BinaryFile file } scroller)
            {
                var viewport = scroller.Bounds.Height;
                var max = Math.Ceiling(file.FileSize / (double)HexViewer.BYTES_PER_LINE) * HexViewer.LINE_HEIGHT - viewport;

                scroller.ViewportSize = viewport;
                scroller.Maximum = Math.Max(viewport, max);
            }
        }

        private async void OnOpenHexViewer(object sender, RoutedEventArgs e)
        {
            if (DataContext is not Models.RevisionBinaryFile vm)
                return;

            Content = new Models.Null();
            _cancellation = new();

            var token = _cancellation.Token;
            var file = await ViewModels.BinaryFile.LoadAsync(vm.Repository, vm.File, vm.Revision);
            if (token.IsCancellationRequested)
                return;

            Content = file;
        }

        private void OnGotoAddressTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is BinaryFileAddressTextBox { DataContext: ViewModels.BinaryFile file } textBox)
            {
                var text = textBox.Text;
                if (string.IsNullOrEmpty(text))
                    return;

                long idx = 0;
                try
                {
                    idx = Convert.ToInt64(text, 16);
                }
                catch
                {
                    // Ignore parsing errors
                }

                if (idx < 0 || idx >= file.FileSize)
                    return;

                // TextBox itself contains a ScrollBar.
                var scroller = this.FindDescendantOfType<ScrollBar>(false, s => s.Name.Equals("HexViewerScroller", StringComparison.Ordinal));
                if (scroller == null)
                    return;

                var offset = Math.Floor(idx / (double)HexViewer.BYTES_PER_LINE) * HexViewer.LINE_HEIGHT - (scroller.ViewportSize * 0.5);
                scroller.Value = Math.Max(0, offset);

                var viewer = this.FindDescendantOfType<HexViewer>(false);
                viewer?.SetHighlightedIndex(idx);
            }
        }

        private CancellationTokenSource _cancellation = new();
    }
}
