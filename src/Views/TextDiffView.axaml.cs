using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using AvaloniaEdit.Utils;

namespace SourceGit.Views
{
    public class ThemedTextDiffPresenter : TextEditor
    {
        public class VerticalSeparatorMargin : AbstractMargin
        {
            public override void Render(DrawingContext context)
            {
                var presenter = this.FindAncestorOfType<ThemedTextDiffPresenter>();
                if (presenter != null)
                {
                    var pen = new Pen(presenter.LineBrush);
                    context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
                }
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(1, 0);
            }
        }

        public class LineNumberMargin : AbstractMargin
        {
            public LineNumberMargin(bool usePresenter, bool isOld)
            {
                _usePresenter = usePresenter;
                _isOld = isOld;

                Margin = new Thickness(8, 0);
                ClipToBounds = true;
            }

            public override void Render(DrawingContext context)
            {
                var presenter = this.FindAncestorOfType<ThemedTextDiffPresenter>();
                if (presenter == null)
                    return;

                var isOld = _isOld;
                if (_usePresenter)
                    isOld = presenter.IsOld;

                var lines = presenter.GetLines();
                var view = TextView;
                if (view is { VisualLinesValid: true })
                {
                    var typeface = view.CreateTypeface();
                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > lines.Count)
                            break;

                        var info = lines[index - 1];
                        var lineNumber = isOld ? info.OldLine : info.NewLine;
                        if (string.IsNullOrEmpty(lineNumber))
                            continue;

                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) - view.VerticalOffset;
                        var txt = new FormattedText(
                            lineNumber,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            presenter.FontSize,
                            presenter.Foreground);
                        context.DrawText(txt, new Point(Bounds.Width - txt.Width, y - (txt.Height * 0.5)));
                    }
                }
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                var presenter = this.FindAncestorOfType<ThemedTextDiffPresenter>();
                if (presenter is not { DataContext: ViewModels.TextDiffContext ctx })
                    return new Size(32, 0);

                var typeface = TextView.CreateTypeface();
                var test = new FormattedText(
                    $"{ctx.Data.MaxLineNumber}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    presenter.FontSize,
                    Brushes.White);
                return new Size(test.Width, 0);
            }

            protected override void OnDataContextChanged(EventArgs e)
            {
                base.OnDataContextChanged(e);
                InvalidateMeasure();
            }

            private readonly bool _usePresenter;
            private readonly bool _isOld;
        }

        public class LineModifyTypeMargin : AbstractMargin
        {
            public LineModifyTypeMargin()
            {
                Margin = new Thickness(1, 0);
                ClipToBounds = true;
            }

            public override void Render(DrawingContext context)
            {
                var presenter = this.FindAncestorOfType<ThemedTextDiffPresenter>();
                if (presenter == null)
                    return;

                var lines = presenter.GetLines();
                var view = TextView;
                if (view is { VisualLinesValid: true })
                {
                    var typeface = view.CreateTypeface();
                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > lines.Count)
                            break;

                        var info = lines[index - 1];
                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) - view.VerticalOffset;
                        FormattedText indicator = null;
                        if (info.Type == Models.TextDiffLineType.Added)
                        {
                            indicator = new FormattedText(
                                "+",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                presenter.FontSize,
                                Brushes.Green);
                        }
                        else if (info.Type == Models.TextDiffLineType.Deleted)
                        {
                            indicator = new FormattedText(
                                "-",
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typeface,
                                presenter.FontSize,
                                Brushes.Red);
                        }

                        if (indicator != null)
                            context.DrawText(indicator, new Point(0, y - (indicator.Height * 0.5)));
                    }
                }
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                var presenter = this.FindAncestorOfType<ThemedTextDiffPresenter>();
                if (presenter == null)
                    return new Size(0, 0);

                var typeface = TextView.CreateTypeface();
                var test = new FormattedText(
                    "-",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    presenter.FontSize,
                    Brushes.White);
                return new Size(test.Width, 0);
            }

            protected override void OnDataContextChanged(EventArgs e)
            {
                base.OnDataContextChanged(e);
                InvalidateVisual();
            }
        }

        public class LineBackgroundRenderer : IBackgroundRenderer
        {
            public KnownLayer Layer => KnownLayer.Background;

            public LineBackgroundRenderer(ThemedTextDiffPresenter presenter)
            {
                _presenter = presenter;
            }

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                if (_presenter.Document == null || !textView.VisualLinesValid)
                    return;

                var changeBlock = _presenter.BlockNavigation.GetCurrentBlock();
                var changeBlockBG = new SolidColorBrush(Colors.Gray, 0.25);
                var changeBlockFG = new Pen(Brushes.Gray);

                var lines = _presenter.GetLines();
                var width = textView.Bounds.Width;
                foreach (var line in textView.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > lines.Count)
                        break;

                    var info = lines[index - 1];

                    var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
                    var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - textView.VerticalOffset;

                    var bg = GetBrushByLineType(info.Type);
                    if (bg != null)
                    {
                        drawingContext.DrawRectangle(bg, null, new Rect(0, startY, width, endY - startY));

                        if (info.Highlights.Count > 0)
                        {
                            var highlightBG = info.Type == Models.TextDiffLineType.Added ? _presenter.AddedHighlightBrush : _presenter.DeletedHighlightBrush;
                            var processingIdxStart = 0;
                            var processingIdxEnd = 0;
                            var nextHighlight = 0;

                            foreach (var tl in line.TextLines)
                            {
                                processingIdxEnd += tl.Length;

                                var y = line.GetTextLineVisualYPosition(tl, VisualYPosition.LineTop) - textView.VerticalOffset;
                                var h = line.GetTextLineVisualYPosition(tl, VisualYPosition.LineBottom) - textView.VerticalOffset - y;

                                while (nextHighlight < info.Highlights.Count)
                                {
                                    var highlight = info.Highlights[nextHighlight];
                                    if (highlight.Start >= processingIdxEnd)
                                        break;

                                    var start = line.GetVisualColumn(highlight.Start < processingIdxStart ? processingIdxStart : highlight.Start);
                                    var end = line.GetVisualColumn(highlight.End >= processingIdxEnd ? processingIdxEnd : highlight.End + 1);

                                    var x = line.GetTextLineVisualXPosition(tl, start) - textView.HorizontalOffset;
                                    var w = line.GetTextLineVisualXPosition(tl, end) - textView.HorizontalOffset - x;
                                    var rect = new Rect(x, y, w, h);
                                    drawingContext.DrawRectangle(highlightBG, null, rect);

                                    if (highlight.End >= processingIdxEnd)
                                        break;

                                    nextHighlight++;
                                }

                                processingIdxStart = processingIdxEnd;
                            }
                        }
                    }

                    if (changeBlock != null && changeBlock.Contains(index))
                    {
                        drawingContext.DrawRectangle(changeBlockBG, null, new Rect(0, startY, width, endY - startY));
                        if (index == changeBlock.Start)
                            drawingContext.DrawLine(changeBlockFG, new Point(0, startY), new Point(width, startY));
                        if (index == changeBlock.End)
                            drawingContext.DrawLine(changeBlockFG, new Point(0, endY), new Point(width, endY));
                    }
                }
            }

            private IBrush GetBrushByLineType(Models.TextDiffLineType type)
            {
                return type switch
                {
                    Models.TextDiffLineType.None => _presenter.EmptyContentBackground,
                    Models.TextDiffLineType.Added => _presenter.AddedContentBackground,
                    Models.TextDiffLineType.Deleted => _presenter.DeletedContentBackground,
                    _ => null,
                };
            }

            private readonly ThemedTextDiffPresenter _presenter;
        }

        public class LineStyleTransformer(ThemedTextDiffPresenter presenter) : DocumentColorizingTransformer
        {
            protected override void ColorizeLine(DocumentLine line)
            {
                var lines = presenter.GetLines();
                var idx = line.LineNumber;
                if (idx > lines.Count)
                    return;

                var info = lines[idx - 1];
                if (info.Type == Models.TextDiffLineType.Indicator)
                {
                    ChangeLinePart(line.Offset, line.EndOffset, v =>
                    {
                        v.TextRunProperties.SetForegroundBrush(presenter.IndicatorForeground);
                        v.TextRunProperties.SetTypeface(new Typeface(presenter.FontFamily, FontStyle.Italic));
                    });
                }
            }
        }

        public static readonly StyledProperty<string> FileNameProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, string>(nameof(FileName), string.Empty);

        public string FileName
        {
            get => GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        public static readonly StyledProperty<bool> IsOldProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, bool>(nameof(IsOld));

        public bool IsOld
        {
            get => GetValue(IsOldProperty);
            set => SetValue(IsOldProperty, value);
        }

        public static readonly StyledProperty<IBrush> LineBrushProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, IBrush>(nameof(LineBrush), new SolidColorBrush(Colors.DarkGray));

        public IBrush LineBrush
        {
            get => GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> EmptyContentBackgroundProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, IBrush>(nameof(EmptyContentBackground), new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)));

        public IBrush EmptyContentBackground
        {
            get => GetValue(EmptyContentBackgroundProperty);
            set => SetValue(EmptyContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> AddedContentBackgroundProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, IBrush>(nameof(AddedContentBackground), new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)));

        public IBrush AddedContentBackground
        {
            get => GetValue(AddedContentBackgroundProperty);
            set => SetValue(AddedContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> DeletedContentBackgroundProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, IBrush>(nameof(DeletedContentBackground), new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)));

        public IBrush DeletedContentBackground
        {
            get => GetValue(DeletedContentBackgroundProperty);
            set => SetValue(DeletedContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> AddedHighlightBrushProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, IBrush>(nameof(AddedHighlightBrush), new SolidColorBrush(Color.FromArgb(90, 0, 255, 0)));

        public IBrush AddedHighlightBrush
        {
            get => GetValue(AddedHighlightBrushProperty);
            set => SetValue(AddedHighlightBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> DeletedHighlightBrushProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, IBrush>(nameof(DeletedHighlightBrush), new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)));

        public IBrush DeletedHighlightBrush
        {
            get => GetValue(DeletedHighlightBrushProperty);
            set => SetValue(DeletedHighlightBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> IndicatorForegroundProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, IBrush>(nameof(IndicatorForeground), Brushes.Gray);

        public IBrush IndicatorForeground
        {
            get => GetValue(IndicatorForegroundProperty);
            set => SetValue(IndicatorForegroundProperty, value);
        }

        public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, bool>(nameof(UseSyntaxHighlighting));

        public bool UseSyntaxHighlighting
        {
            get => GetValue(UseSyntaxHighlightingProperty);
            set => SetValue(UseSyntaxHighlightingProperty, value);
        }

        public static readonly StyledProperty<bool> ShowHiddenSymbolsProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, bool>(nameof(ShowHiddenSymbols));

        public bool ShowHiddenSymbols
        {
            get => GetValue(ShowHiddenSymbolsProperty);
            set => SetValue(ShowHiddenSymbolsProperty, value);
        }

        public static readonly StyledProperty<int> TabWidthProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, int>(nameof(TabWidth), 4);

        public int TabWidth
        {
            get => GetValue(TabWidthProperty);
            set => SetValue(TabWidthProperty, value);
        }

        public static readonly StyledProperty<ViewModels.TextDiffSelectedChunk> SelectedChunkProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, ViewModels.TextDiffSelectedChunk>(nameof(SelectedChunk));

        public ViewModels.TextDiffSelectedChunk SelectedChunk
        {
            get => GetValue(SelectedChunkProperty);
            set => SetValue(SelectedChunkProperty, value);
        }

        public static readonly StyledProperty<ViewModels.BlockNavigation> BlockNavigationProperty =
            AvaloniaProperty.Register<ThemedTextDiffPresenter, ViewModels.BlockNavigation>(nameof(BlockNavigation));

        public ViewModels.BlockNavigation BlockNavigation
        {
            get => GetValue(BlockNavigationProperty);
            set => SetValue(BlockNavigationProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public ThemedTextDiffPresenter(TextArea area, TextDocument doc) : base(area, doc)
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            BorderThickness = new Thickness(0);

            Options.IndentationSize = TabWidth;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;
            Options.ShowEndOfLine = false;
            Options.AllowScrollBelowDocument = false;

            _lineStyleTransformer = new LineStyleTransformer(this);

            TextArea.TextView.Margin = new Thickness(2, 0);
            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
            TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);
        }

        public void GotoChange(ViewModels.BlockNavigationDirection direction)
        {
            if (DataContext is not ViewModels.TextDiffContext)
                return;

            var block = BlockNavigation.Goto(direction);
            if (block != null)
            {
                TextArea.Caret.Line = block.Start;
                ScrollToLine(block.Start);
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var chunk = SelectedChunk;
            if (chunk == null || (!chunk.Combined && chunk.IsOldSide != IsOld))
                return;

            var color = (Color)this.FindResource("SystemAccentColor")!;
            var brush = new SolidColorBrush(color, 0.1);
            var pen = new Pen(color.ToUInt32());
            var rect = new Rect(0, chunk.Y, Bounds.Width, chunk.Height);

            context.DrawRectangle(brush, null, rect);
            context.DrawLine(pen, rect.TopLeft, rect.TopRight);
            context.DrawLine(pen, rect.BottomLeft, rect.BottomRight);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            TextArea.Caret.PositionChanged += OnTextAreaCaretPositionChanged;
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.PointerEntered += OnTextViewPointerChanged;
            TextArea.TextView.PointerMoved += OnTextViewPointerChanged;
            TextArea.TextView.PointerWheelChanged += OnTextViewPointerWheelChanged;
            TextArea.TextView.VisualLinesChanged += OnTextViewVisualLinesChanged;

            TextArea.AddHandler(KeyDownEvent, OnTextAreaKeyDown, RoutingStrategies.Tunnel);

            UpdateTextMate();
            OnTextViewVisualLinesChanged(null, null);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.RemoveHandler(KeyDownEvent, OnTextAreaKeyDown);

            TextArea.Caret.PositionChanged -= OnTextAreaCaretPositionChanged;
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            TextArea.TextView.PointerEntered -= OnTextViewPointerChanged;
            TextArea.TextView.PointerMoved -= OnTextViewPointerChanged;
            TextArea.TextView.PointerWheelChanged -= OnTextViewPointerWheelChanged;
            TextArea.TextView.VisualLinesChanged -= OnTextViewVisualLinesChanged;

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UseSyntaxHighlightingProperty)
            {
                UpdateTextMate();
            }
            else if (change.Property == ShowHiddenSymbolsProperty)
            {
                var val = ShowHiddenSymbols;
                Options.ShowTabs = val;
                Options.ShowSpaces = val;
            }
            else if (change.Property == TabWidthProperty)
            {
                Options.IndentationSize = TabWidth;
            }
            else if (change.Property == FileNameProperty)
            {
                Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);
            }
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
            else if (change.Property == SelectedChunkProperty)
            {
                InvalidateVisual();
            }
            else if (change.Property == BlockNavigationProperty)
            {
                TextArea?.TextView?.Redraw();
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            AutoScrollToFirstChange();
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            if (!_execSizeChanged)
            {
                _execSizeChanged = true;
                AutoScrollToFirstChange();
            }
        }

        protected virtual void UpdateSelectedChunk(double y)
        {
        }

        private async void OnTextAreaKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyModifiers.Equals(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                if (e.Key == Key.C)
                {
                    await CopyWithoutIndicatorsAsync();
                    e.Handled = true;
                }
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }

        private void OnTextAreaCaretPositionChanged(object sender, EventArgs e)
        {
            BlockNavigation.UpdateByCaretPosition(TextArea?.Caret?.Line ?? 0);
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selection = TextArea.Selection;
            if (selection.IsEmpty)
                return;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await CopyWithoutIndicatorsAsync();
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private void OnTextViewPointerChanged(object sender, PointerEventArgs e)
        {
            if (DataContext is not ViewModels.TextDiffContext { Option: { WorkingCopyChange: { } } })
                return;

            if (sender is not TextView view)
                return;

            var selection = TextArea.Selection;
            if (selection == null || selection.IsEmpty)
            {
                if (_lastSelectStart != _lastSelectEnd)
                {
                    _lastSelectStart = TextLocation.Empty;
                    _lastSelectEnd = TextLocation.Empty;
                }

                var chunk = SelectedChunk;
                if (chunk != null)
                {
                    var rect = new Rect(0, chunk.Y, Bounds.Width, chunk.Height);
                    if (rect.Contains(e.GetPosition(this)))
                        return;
                }

                UpdateSelectedChunk(e.GetPosition(view).Y + view.VerticalOffset);
                return;
            }

            var start = selection.StartPosition.Location;
            var end = selection.EndPosition.Location;
            if (_lastSelectStart != start || _lastSelectEnd != end)
            {
                _lastSelectStart = start;
                _lastSelectEnd = end;
                UpdateSelectedChunk(e.GetPosition(view).Y + view.VerticalOffset);
                return;
            }

            if (SelectedChunk == null)
                UpdateSelectedChunk(e.GetPosition(view).Y + view.VerticalOffset);
        }

        private void OnTextViewPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (DataContext is not ViewModels.TextDiffContext { Option: { WorkingCopyChange: { } } })
                return;

            if (sender is not TextView view)
                return;

            var y = e.GetPosition(view).Y + view.VerticalOffset;
            Dispatcher.UIThread.Post(() => UpdateSelectedChunk(y));
        }

        private void OnTextViewVisualLinesChanged(object sender, EventArgs e)
        {
            if (DataContext is not ViewModels.TextDiffContext ctx)
                return;

            if (ctx.IsSideBySide() && !IsOld)
                return;

            if (!TextArea.TextView.VisualLinesValid)
            {
                ctx.DisplayRange = null;
                return;
            }

            var lines = GetLines();
            var start = int.MaxValue;
            var count = 0;
            foreach (var line in TextArea.TextView.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var index = line.FirstDocumentLine.LineNumber - 1;
                if (index >= lines.Count)
                    continue;

                count++;
                if (start > index)
                    start = index;
            }

            ctx.DisplayRange = new ViewModels.TextDiffDisplayRange(start, start + count);
        }

        protected void TrySetChunk(ViewModels.TextDiffSelectedChunk chunk)
        {
            if (ViewModels.TextDiffSelectedChunk.IsChanged(SelectedChunk, chunk))
                SetCurrentValue(SelectedChunkProperty, chunk);
        }

        private List<Models.TextDiffLine> GetLines()
        {
            if (DataContext is ViewModels.CombinedTextDiff combined)
                return combined.Data.Lines;

            if (DataContext is ViewModels.TwoSideTextDiff twoSides)
                return IsOld ? twoSides.Old : twoSides.New;

            return [];
        }

        private void UpdateTextMate()
        {
            if (UseSyntaxHighlighting)
            {
                if (_textMate == null)
                {
                    TextArea.TextView.LineTransformers.Remove(_lineStyleTransformer);
                    _textMate = Models.TextMateHelper.CreateForEditor(this);
                    TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);
                }
            }
            else
            {
                if (_textMate != null)
                {
                    _textMate.Dispose();
                    _textMate = null;
                    GC.Collect();

                    TextArea.TextView.Redraw();
                }
            }
        }

        private void AutoScrollToFirstChange()
        {
            if (Bounds.Height < 0.1)
                return;

            if (DataContext is not ViewModels.TextDiffContext ctx)
                return;

            var curBlock = ctx.BlockNavigation.GetCurrentBlock();
            if (curBlock == null)
                return;

            var lineHeight = TextArea.TextView.DefaultLineHeight;
            var vOffset = lineHeight * (curBlock.Start - 1) - Bounds.Height * 0.5;
            if (vOffset >= 0)
            {
                var scroller = this.FindDescendantOfType<ScrollViewer>();
                if (scroller != null)
                {
                    var scrollOffset = new Vector(0, vOffset);
                    scroller.Offset = scrollOffset;
                    ctx.ScrollOffset = scrollOffset;
                }
            }
        }

        private async Task CopyWithoutIndicatorsAsync()
        {
            var selection = TextArea.Selection;
            if (selection.IsEmpty)
            {
                await App.CopyTextAsync(string.Empty);
                return;
            }

            var lines = GetLines();

            var startPosition = selection.StartPosition;
            var endPosition = selection.EndPosition;

            if (startPosition.Location > endPosition.Location)
                (startPosition, endPosition) = (endPosition, startPosition);

            var startIdx = startPosition.Line - 1;
            var endIdx = endPosition.Line - 1;

            if (startIdx == endIdx)
            {
                if (lines[startIdx].Type is Models.TextDiffLineType.Indicator or Models.TextDiffLineType.None)
                    await App.CopyTextAsync(string.Empty);
                else
                    await App.CopyTextAsync(SelectedText);
                return;
            }

            var builder = new StringBuilder();
            for (var i = startIdx; i <= endIdx && i <= lines.Count - 1; i++)
            {
                var line = lines[i];
                if (line.Type is Models.TextDiffLineType.Indicator or Models.TextDiffLineType.None)
                    continue;

                // The first selected line (partial selection)
                if (i == startIdx && startPosition.Column > 1)
                {
                    builder.Append(line.Content.AsSpan(startPosition.Column - 1));
                    builder.Append(Environment.NewLine);
                    continue;
                }

                // The selection range is larger than original source.
                if (i == lines.Count - 1 && i < endIdx)
                {
                    builder.Append(line.Content);
                    break;
                }

                // For the last line (selection range is within original source)
                if (i == endIdx)
                {
                    if (endPosition.Column - 1 < line.Content.Length)
                        builder.Append(line.Content.AsSpan(0, endPosition.Column - 1));
                    else
                        builder.Append(line.Content);

                    break;
                }

                // Other lines.
                builder.AppendLine(line.Content);
            }

            await App.CopyTextAsync(builder.ToString());
        }

        private bool _execSizeChanged;
        private TextMate.Installation _textMate;
        private TextLocation _lastSelectStart = TextLocation.Empty;
        private TextLocation _lastSelectEnd = TextLocation.Empty;
        private LineStyleTransformer _lineStyleTransformer;
    }

    public class CombinedTextDiffPresenter : ThemedTextDiffPresenter
    {
        public CombinedTextDiffPresenter() : base(new TextArea(), new TextDocument())
        {
            TextArea.LeftMargins.Add(new LineNumberMargin(false, true));
            TextArea.LeftMargins.Add(new VerticalSeparatorMargin());
            TextArea.LeftMargins.Add(new LineNumberMargin(false, false));
            TextArea.LeftMargins.Add(new VerticalSeparatorMargin());
            TextArea.LeftMargins.Add(new LineModifyTypeMargin());
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _scrollViewer = this.FindDescendantOfType<ScrollViewer>();
            if (_scrollViewer != null)
            {
                _scrollViewer.Bind(ScrollViewer.OffsetProperty, new Binding("ScrollOffset", BindingMode.TwoWay));
                _scrollViewer.ScrollChanged += OnTextViewScrollChanged;
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            if (_scrollViewer != null)
                _scrollViewer.ScrollChanged -= OnTextViewScrollChanged;

            base.OnUnloaded(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.CombinedTextDiff { Data: { } diff })
            {
                var builder = new StringBuilder();
                foreach (var line in diff.Lines)
                {
                    if (line.Content.Length > 1000)
                    {
                        builder.Append(line.Content.AsSpan(0, 1000));
                        builder.Append($"...({line.Content.Length - 1000} characters trimmed)");
                    }
                    else
                    {
                        builder.Append(line.Content);
                    }

                    if (line.NoNewLineEndOfFile)
                        builder.Append("\u26D4");

                    builder.Append('\n');
                }

                Text = builder.ToString();
            }
            else
            {
                Text = string.Empty;
            }

            GC.Collect();
        }

        protected override void UpdateSelectedChunk(double y)
        {
            if (DataContext is not ViewModels.CombinedTextDiff { Data: { } diff } combined)
                return;

            var view = TextArea.TextView;
            var selection = TextArea.Selection;
            if (!selection.IsEmpty)
            {
                var startIdx = Math.Min(selection.StartPosition.Line - 1, diff.Lines.Count - 1);
                var endIdx = Math.Min(selection.EndPosition.Line - 1, diff.Lines.Count - 1);

                if (startIdx > endIdx)
                    (startIdx, endIdx) = (endIdx, startIdx);

                var hasChanges = false;
                for (var i = startIdx; i <= endIdx; i++)
                {
                    var line = diff.Lines[i];
                    if (line.Type == Models.TextDiffLineType.Added || line.Type == Models.TextDiffLineType.Deleted)
                    {
                        hasChanges = true;
                        break;
                    }
                }

                if (!hasChanges)
                {
                    TrySetChunk(null);
                    return;
                }

                var firstLineIdx = view.VisualLines[0].FirstDocumentLine.LineNumber - 1;
                var lastLineIdx = view.VisualLines[^1].FirstDocumentLine.LineNumber - 1;
                if (endIdx < firstLineIdx || startIdx > lastLineIdx)
                {
                    TrySetChunk(null);
                    return;
                }

                var startLine = view.GetVisualLine(startIdx + 1);
                var endLine = view.GetVisualLine(endIdx + 1);

                var rectStartY = startLine != null ?
                    startLine.GetTextLineVisualYPosition(startLine.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset :
                    0;
                var rectEndY = endLine != null ?
                    endLine.GetTextLineVisualYPosition(endLine.TextLines[^1], VisualYPosition.TextBottom) - view.VerticalOffset :
                    view.Bounds.Height;

                TrySetChunk(new(rectStartY, rectEndY - rectStartY, startIdx, endIdx, true, false));
            }
            else
            {
                var lineIdx = -1;
                foreach (var line in view.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > diff.Lines.Count)
                        break;

                    var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.TextBottom);
                    if (endY > y)
                    {
                        lineIdx = index - 1;
                        break;
                    }
                }

                if (lineIdx == -1)
                {
                    TrySetChunk(null);
                    return;
                }

                var (startIdx, endIdx) = combined.FindRangeByIndex(diff.Lines, lineIdx);
                if (startIdx == -1)
                {
                    TrySetChunk(null);
                    return;
                }

                var startLine = view.GetVisualLine(startIdx + 1);
                var endLine = view.GetVisualLine(endIdx + 1);

                var rectStartY = startLine != null ?
                    startLine.GetTextLineVisualYPosition(startLine.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset :
                    0;
                var rectEndY = endLine != null ?
                    endLine.GetTextLineVisualYPosition(endLine.TextLines[^1], VisualYPosition.TextBottom) - view.VerticalOffset :
                    view.Bounds.Height;

                TrySetChunk(new(rectStartY, rectEndY - rectStartY, startIdx, endIdx, true, false));
            }
        }

        private void OnTextViewScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!TextArea.TextView.IsPointerOver)
                TrySetChunk(null);
        }

        private ScrollViewer _scrollViewer;
    }

    public class SingleSideTextDiffPresenter : ThemedTextDiffPresenter
    {
        public SingleSideTextDiffPresenter() : base(new TextArea(), new TextDocument())
        {
            TextArea.LeftMargins.Add(new LineNumberMargin(true, false));
            TextArea.LeftMargins.Add(new VerticalSeparatorMargin());
            TextArea.LeftMargins.Add(new LineModifyTypeMargin());
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BlockNavigationProperty)
            {
                if (change.OldValue is ViewModels.BlockNavigation oldValue)
                    oldValue.PropertyChanged -= OnBlockNavigationPropertyChanged;
                if (change.NewValue is ViewModels.BlockNavigation newValue)
                    newValue.PropertyChanged += OnBlockNavigationPropertyChanged;
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _scrollViewer = this.FindDescendantOfType<ScrollViewer>();
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnTextViewScrollChanged;
                _scrollViewer.Bind(ScrollViewer.OffsetProperty, new Binding("ScrollOffset", BindingMode.OneWay));
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnTextViewScrollChanged;
                _scrollViewer = null;
            }

            base.OnUnloaded(e);
            GC.Collect();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.TwoSideTextDiff diff)
            {
                var builder = new StringBuilder();
                var lines = IsOld ? diff.Old : diff.New;
                foreach (var line in lines)
                {
                    if (line.Content.Length > 1000)
                    {
                        builder.Append(line.Content.AsSpan(0, 1000));
                        builder.Append($"...({line.Content.Length - 1000} characters trimmed)");
                    }
                    else
                    {
                        builder.Append(line.Content);
                    }

                    if (line.NoNewLineEndOfFile)
                        builder.Append("\u26D4");

                    builder.Append('\n');
                }

                Text = builder.ToString();
            }
            else
            {
                Text = string.Empty;
            }
        }

        protected override void UpdateSelectedChunk(double y)
        {
            if (DataContext is not ViewModels.TwoSideTextDiff diff)
                return;

            var view = TextArea.TextView;
            var lines = IsOld ? diff.Old : diff.New;
            var selection = TextArea.Selection;
            if (!selection.IsEmpty)
            {
                var startIdx = Math.Min(selection.StartPosition.Line - 1, lines.Count - 1);
                var endIdx = Math.Min(selection.EndPosition.Line - 1, lines.Count - 1);

                if (startIdx > endIdx)
                    (startIdx, endIdx) = (endIdx, startIdx);

                var hasChanges = false;
                for (var i = startIdx; i <= endIdx; i++)
                {
                    var line = lines[i];
                    if (line.Type == Models.TextDiffLineType.Added || line.Type == Models.TextDiffLineType.Deleted)
                    {
                        hasChanges = true;
                        break;
                    }
                }

                if (!hasChanges)
                {
                    TrySetChunk(null);
                    return;
                }

                var firstLineIdx = view.VisualLines[0].FirstDocumentLine.LineNumber - 1;
                var lastLineIdx = view.VisualLines[^1].FirstDocumentLine.LineNumber - 1;
                if (endIdx < firstLineIdx || startIdx > lastLineIdx)
                {
                    TrySetChunk(null);
                    return;
                }

                var startLine = view.GetVisualLine(startIdx + 1);
                var endLine = view.GetVisualLine(endIdx + 1);

                var rectStartY = startLine != null ?
                    startLine.GetTextLineVisualYPosition(startLine.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset :
                    0;
                var rectEndY = endLine != null ?
                    endLine.GetTextLineVisualYPosition(endLine.TextLines[^1], VisualYPosition.TextBottom) - view.VerticalOffset :
                    view.Bounds.Height;

                diff.GetCombinedRangeForSingleSide(ref startIdx, ref endIdx, IsOld);
                TrySetChunk(new(rectStartY, rectEndY - rectStartY, startIdx, endIdx, false, IsOld));
            }
            else
            {
                var lineIdx = -1;
                foreach (var line in view.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > lines.Count)
                        break;

                    var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.TextBottom);
                    if (endY > y)
                    {
                        lineIdx = index - 1;
                        break;
                    }
                }

                if (lineIdx == -1)
                {
                    TrySetChunk(null);
                    return;
                }

                var (startIdx, endIdx) = diff.FindRangeByIndex(lines, lineIdx);
                if (startIdx == -1)
                {
                    TrySetChunk(null);
                    return;
                }

                var startLine = view.GetVisualLine(startIdx + 1);
                var endLine = view.GetVisualLine(endIdx + 1);

                var rectStartY = startLine != null ?
                    startLine.GetTextLineVisualYPosition(startLine.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset :
                    0;
                var rectEndY = endLine != null ?
                    endLine.GetTextLineVisualYPosition(endLine.TextLines[^1], VisualYPosition.TextBottom) - view.VerticalOffset :
                    view.Bounds.Height;

                diff.GetCombinedRangeForBothSides(ref startIdx, ref endIdx, IsOld);
                TrySetChunk(new(rectStartY, rectEndY - rectStartY, startIdx, endIdx, true, false));
            }
        }

        private void OnTextViewScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scrollViewer == null || DataContext is not ViewModels.TwoSideTextDiff diff)
                return;

            if (diff.ScrollOffset.NearlyEquals(_scrollViewer.Offset))
                return;

            if (IsPointerOver || e.OffsetDelta.SquaredLength > 1.0f)
            {
                diff.ScrollOffset = _scrollViewer.Offset;

                if (!TextArea.TextView.IsPointerOver)
                    TrySetChunk(null);
            }
        }

        private void OnBlockNavigationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if ("Indicator".Equals(e.PropertyName, StringComparison.Ordinal))
                TextArea?.TextView?.Redraw();
        }

        private ScrollViewer _scrollViewer;
    }

    public class TextDiffViewMinimap : Control
    {
        public static readonly StyledProperty<IBrush> AddedLineBrushProperty =
            AvaloniaProperty.Register<TextDiffViewMinimap, IBrush>(nameof(AddedLineBrush), new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)));

        public IBrush AddedLineBrush
        {
            get => GetValue(AddedLineBrushProperty);
            set => SetValue(AddedLineBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> DeletedLineBrushProperty =
            AvaloniaProperty.Register<TextDiffViewMinimap, IBrush>(nameof(DeletedLineBrush), new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)));

        public IBrush DeletedLineBrush
        {
            get => GetValue(DeletedLineBrushProperty);
            set => SetValue(DeletedLineBrushProperty, value);
        }

        public static readonly StyledProperty<ViewModels.TextDiffDisplayRange> DisplayRangeProperty =
            AvaloniaProperty.Register<TextDiffViewMinimap, ViewModels.TextDiffDisplayRange>(nameof(DisplayRange));

        public ViewModels.TextDiffDisplayRange DisplayRange
        {
            get => GetValue(DisplayRangeProperty);
            set => SetValue(DisplayRangeProperty, value);
        }

        public static readonly StyledProperty<Color> DisplayRangeColorProperty =
            AvaloniaProperty.Register<TextDiffViewMinimap, Color>(nameof(DisplayRangeColor), Colors.RoyalBlue);

        public Color DisplayRangeColor
        {
            get => GetValue(DisplayRangeColorProperty);
            set => SetValue(DisplayRangeColorProperty, value);
        }

        static TextDiffViewMinimap()
        {
            AffectsRender<TextDiffViewMinimap>(
                AddedLineBrushProperty,
                DeletedLineBrushProperty,
                DisplayRangeProperty,
                DisplayRangeColorProperty);
        }

        public override void Render(DrawingContext context)
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

            var total = 0;
            if (DataContext is ViewModels.TwoSideTextDiff twoSideDiff)
            {
                var halfWidth = Bounds.Width * 0.5;
                total = Math.Max(twoSideDiff.Old.Count, twoSideDiff.New.Count);
                RenderSingleSide(context, twoSideDiff.Old, 0, halfWidth);
                RenderSingleSide(context, twoSideDiff.New, halfWidth, halfWidth);
            }
            else if (DataContext is ViewModels.CombinedTextDiff combined)
            {
                var data = combined.Data;
                total = data.Lines.Count;
                RenderSingleSide(context, data.Lines, 0, Bounds.Width);
            }

            var range = DisplayRange;
            if (range == null || range.End == 0)
                return;

            var startY = range.Start / (total * 1.0) * Bounds.Height;
            var endY = range.End / (total * 1.0) * Bounds.Height;
            var color = DisplayRangeColor;
            var brush = new SolidColorBrush(color, 0.2);
            var pen = new Pen(color.ToUInt32());
            var rect = new Rect(0, startY, Bounds.Width, endY - startY);

            context.DrawRectangle(brush, null, rect);
            context.DrawLine(pen, rect.TopLeft, rect.TopRight);
            context.DrawLine(pen, rect.BottomLeft, rect.BottomRight);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var range = DisplayRange;
            if (range == null || range.End == 0)
                return;

            int total;
            if (DataContext is ViewModels.TwoSideTextDiff twoSideDiff)
            {
                total = Math.Max(twoSideDiff.Old.Count, twoSideDiff.New.Count);
            }
            else if (DataContext is ViewModels.CombinedTextDiff combined)
            {
                var data = combined.Data;
                total = data.Lines.Count;
            }
            else
            {
                return;
            }

            var height = Bounds.Height;
            var startY = range.Start / (total * 1.0) * height;
            var endY = range.End / (total * 1.0) * height;
            var pressedY = e.GetPosition(this).Y;
            if (pressedY >= startY && pressedY <= endY)
                return;

            var line = Math.Max(1, Math.Min(total, (int)Math.Ceiling(pressedY * total / height)));
            if (Parent is Control parent)
                parent.FindLogicalDescendantOfType<ThemedTextDiffPresenter>()?.ScrollToLine(line);

            e.Handled = true;
        }

        private void RenderSingleSide(DrawingContext context, List<Models.TextDiffLine> lines, double x, double width)
        {
            var total = lines.Count;
            var lastLineType = Models.TextDiffLineType.Indicator;
            var lastLineTypeStart = 0;

            for (int i = 0; i < total; i++)
            {
                var line = lines[i];
                if (line.Type != lastLineType)
                {
                    RenderBlock(context, lastLineType, lastLineTypeStart, i - lastLineTypeStart, total, x, width);

                    lastLineType = line.Type;
                    lastLineTypeStart = i;
                }
            }

            RenderBlock(context, lastLineType, lastLineTypeStart, total - lastLineTypeStart, total, x, width);
        }

        private void RenderBlock(DrawingContext context, Models.TextDiffLineType type, int start, int count, int total, double x, double width)
        {
            if (type == Models.TextDiffLineType.Added || type == Models.TextDiffLineType.Deleted)
            {
                var brush = type == Models.TextDiffLineType.Added ? AddedLineBrush : DeletedLineBrush;
                var y = start / (total * 1.0) * Bounds.Height;
                var h = Math.Max(0.5, count / (total * 1.0) * Bounds.Height);
                context.DrawRectangle(brush, null, new Rect(x, y, width, h));
            }
        }
    }

    public partial class TextDiffView : UserControl
    {
        public static readonly StyledProperty<ViewModels.TextDiffSelectedChunk> SelectedChunkProperty =
            AvaloniaProperty.Register<TextDiffView, ViewModels.TextDiffSelectedChunk>(nameof(SelectedChunk));

        public ViewModels.TextDiffSelectedChunk SelectedChunk
        {
            get => GetValue(SelectedChunkProperty);
            set => SetValue(SelectedChunkProperty, value);
        }

        public TextDiffView()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedChunkProperty)
            {
                if (SelectedChunk is { } chunk)
                {
                    var top = chunk.Y + (chunk.Height >= 36 ? 8 : 2);
                    var right = (chunk.Combined || !chunk.IsOldSide) ? 26 : (Bounds.Width * 0.5f) + 26;
                    Popup.Margin = new Thickness(0, top, right, 0);
                    Popup.IsVisible = true;
                }
                else
                {
                    Popup.IsVisible = false;
                }
            }
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);

            if (DataContext is ViewModels.TextDiffContext ctx)
                ctx.SelectedChunk = null;
        }

        private async void OnStageChunk(object _1, RoutedEventArgs _2)
        {
            if (DataContext is not ViewModels.TextDiffContext { SelectedChunk: { } chunk, Data: { } diff, Option: { IsUnstaged: true, WorkingCopyChange: { } change } })
                return;

            var selection = diff.MakeSelection(chunk.StartIdx + 1, chunk.EndIdx + 1, chunk.Combined, chunk.IsOldSide);
            if (!selection.HasChanges)
                return;

            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView?.DataContext is not ViewModels.Repository repo)
                return;

            using var lockWatcher = repo.LockWatcher();

            var tmpFile = Path.GetTempFileName();
            if (change.WorkTree == Models.ChangeState.Untracked)
            {
                diff.GenerateNewPatchFromSelection(change, null, selection, false, tmpFile);
            }
            else if (chunk.Combined)
            {
                var treeGuid = await new Commands.QueryStagedFileBlobGuid(repo.FullPath, change.Path).GetResultAsync();
                diff.GeneratePatchFromSelection(change, treeGuid, selection, false, tmpFile);
            }
            else
            {
                var treeGuid = await new Commands.QueryStagedFileBlobGuid(repo.FullPath, change.Path).GetResultAsync();
                diff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, false, chunk.IsOldSide, tmpFile);
            }

            await new Commands.Apply(repo.FullPath, tmpFile, true, "nowarn", "--cache --index").ExecAsync();
            File.Delete(tmpFile);

            repo.MarkWorkingCopyDirtyManually();
        }

        private async void OnUnstageChunk(object _1, RoutedEventArgs _2)
        {
            if (DataContext is not ViewModels.TextDiffContext { SelectedChunk: { } chunk, Data: { } diff, Option: { IsUnstaged: false, WorkingCopyChange: { } change } })
                return;

            var selection = diff.MakeSelection(chunk.StartIdx + 1, chunk.EndIdx + 1, chunk.Combined, chunk.IsOldSide);
            if (!selection.HasChanges)
                return;

            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView?.DataContext is not ViewModels.Repository repo)
                return;

            using var lockWatcher = repo.LockWatcher();

            var treeGuid = await new Commands.QueryStagedFileBlobGuid(repo.FullPath, change.Path).GetResultAsync();
            var tmpFile = Path.GetTempFileName();
            if (change.Index == Models.ChangeState.Added)
                diff.GenerateNewPatchFromSelection(change, treeGuid, selection, true, tmpFile);
            else if (chunk.Combined)
                diff.GeneratePatchFromSelection(change, treeGuid, selection, true, tmpFile);
            else
                diff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, true, chunk.IsOldSide, tmpFile);

            await new Commands.Apply(repo.FullPath, tmpFile, true, "nowarn", "--cache --index --reverse").ExecAsync();
            File.Delete(tmpFile);

            repo.MarkWorkingCopyDirtyManually();
        }

        private async void OnDiscardChunk(object _1, RoutedEventArgs _2)
        {
            if (DataContext is not ViewModels.TextDiffContext { SelectedChunk: { } chunk, Data: { } diff, Option: { IsUnstaged: true, WorkingCopyChange: { } change } })
                return;

            var selection = diff.MakeSelection(chunk.StartIdx + 1, chunk.EndIdx + 1, chunk.Combined, chunk.IsOldSide);
            if (!selection.HasChanges)
                return;

            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView?.DataContext is not ViewModels.Repository repo)
                return;

            using var lockWatcher = repo.LockWatcher();

            var tmpFile = Path.GetTempFileName();
            if (change.Index == Models.ChangeState.Added)
            {
                diff.GenerateNewPatchFromSelection(change, null, selection, true, tmpFile);
            }
            else if (chunk.Combined)
            {
                var treeGuid = await new Commands.QueryStagedFileBlobGuid(repo.FullPath, change.Path).GetResultAsync();
                diff.GeneratePatchFromSelection(change, treeGuid, selection, true, tmpFile);
            }
            else
            {
                var treeGuid = await new Commands.QueryStagedFileBlobGuid(repo.FullPath, change.Path).GetResultAsync();
                diff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, true, chunk.IsOldSide, tmpFile);
            }

            await new Commands.Apply(repo.FullPath, tmpFile, true, "nowarn", "--reverse").ExecAsync();
            File.Delete(tmpFile);

            repo.MarkWorkingCopyDirtyManually();
        }
    }
}
