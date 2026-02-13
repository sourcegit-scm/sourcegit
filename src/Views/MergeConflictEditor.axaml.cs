using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    public class MergeConflictTextPresenter : TextEditor
    {
        public class LineNumberMargin : AbstractMargin
        {
            public LineNumberMargin(MergeConflictTextPresenter presenter)
            {
                _presenter = presenter;
                Margin = new Thickness(8, 0);
                ClipToBounds = true;
            }

            public override void Render(DrawingContext context)
            {
                var lines = _presenter.Lines;
                if (lines == null)
                    return;

                var view = TextView;
                if (view is not { VisualLinesValid: true })
                    return;

                var typeface = view.CreateTypeface();
                foreach (var line in view.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > lines.Count)
                        break;

                    var lineNumber = lines[index - 1].LineNumber;
                    if (string.IsNullOrEmpty(lineNumber))
                        continue;

                    var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) - view.VerticalOffset;
                    var txt = new FormattedText(
                        lineNumber,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _presenter.FontSize,
                        _presenter.Foreground);
                    context.DrawText(txt, new Point(Bounds.Width - txt.Width, y - (txt.Height * 0.5)));
                }
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                var maxLine = _presenter.MaxLineNumber;
                if (maxLine == 0)
                    return new Size(32, 0);

                var typeface = TextView.CreateTypeface();
                var test = new FormattedText(
                    $"{maxLine}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    _presenter.FontSize,
                    Brushes.White);
                return new Size(test.Width, 0);
            }

            private readonly MergeConflictTextPresenter _presenter;
        }

        public class VerticalSeparatorMargin : AbstractMargin
        {
            public override void Render(DrawingContext context)
            {
                var pen = new Pen(Brushes.DarkGray);
                context.DrawLine(pen, new Point(0.5, 0), new Point(0.5, Bounds.Height));
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(1, 0);
            }
        }

        public class ConflictMarkerTransformer : DocumentColorizingTransformer
        {
            public ConflictMarkerTransformer(MergeConflictTextPresenter presenter)
            {
                _presenter = presenter;
            }

            protected override void ColorizeLine(DocumentLine line)
            {
                var lines = _presenter.Lines;
                if (lines == null || line.LineNumber > lines.Count)
                    return;

                var info = lines[line.LineNumber - 1];
                if (info.Type == Models.ConflictLineType.Marker)
                {
                    ChangeLinePart(line.Offset, line.EndOffset, element =>
                    {
                        element.TextRunProperties.SetTypeface(new Typeface(_presenter.FontFamily, FontStyle.Italic, FontWeight.Normal));
                        element.TextRunProperties.SetForegroundBrush(Brushes.Gray);
                    });
                }
            }

            private readonly MergeConflictTextPresenter _presenter;
        }

        public class LineBackgroundRenderer : IBackgroundRenderer
        {
            public KnownLayer Layer => KnownLayer.Background;

            public LineBackgroundRenderer(MergeConflictTextPresenter presenter)
            {
                _presenter = presenter;
            }

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                var lines = _presenter.Lines;
                if (lines == null || _presenter.Document == null || !textView.VisualLinesValid)
                    return;

                if (_presenter.DataContext is not ViewModels.MergeConflictEditor vm)
                    return;

                var width = textView.Bounds.Width;
                var pixelHeight = PixelSnapHelpers.GetPixelSize(_presenter).Height;
                foreach (var line in textView.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > lines.Count)
                        break;

                    var lineIndex = index - 1;
                    var info = lines[lineIndex];
                    if (info.Type == Models.ConflictLineType.Common)
                        continue;

                    var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
                    var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - textView.VerticalOffset;
                    var rect = new Rect(0, startY, width, endY - startY);

                    var alignedTop = PixelSnapHelpers.PixelAlign(startY, pixelHeight);
                    var alignedBottom = PixelSnapHelpers.PixelAlign(endY, pixelHeight);

                    var lineState = vm.GetLineState(lineIndex);
                    if (lineState == Models.ConflictLineState.ConflictBlockStart)
                        drawingContext.DrawLine(new Pen(new SolidColorBrush(Colors.Red, 0.6)), new Point(0, alignedTop), new Point(width, alignedTop));
                    else if (lineState == Models.ConflictLineState.ConflictBlockEnd)
                        drawingContext.DrawLine(new Pen(new SolidColorBrush(Colors.Red, 0.6)), new Point(0, alignedBottom), new Point(width, alignedBottom));
                    else if (lineState == Models.ConflictLineState.ResolvedBlockStart)
                        drawingContext.DrawLine(new Pen(new SolidColorBrush(Colors.Green, 0.6)), new Point(0, alignedTop), new Point(width, alignedTop));
                    else if (lineState == Models.ConflictLineState.ResolvedBlockEnd)
                        drawingContext.DrawLine(new Pen(new SolidColorBrush(Colors.Green, 0.6)), new Point(0, alignedBottom), new Point(width, alignedBottom));

                    if (lineState >= Models.ConflictLineState.ResolvedBlockStart)
                        drawingContext.DrawRectangle(new SolidColorBrush(Colors.Green, 0.1), null, rect);
                    else if (lineState >= Models.ConflictLineState.ConflictBlockStart)
                        drawingContext.DrawRectangle(new SolidColorBrush(Colors.Red, 0.1), null, rect);

                    var bg = info.Type switch
                    {
                        Models.ConflictLineType.Ours => _presenter.OursContentBackground,
                        Models.ConflictLineType.Theirs => _presenter.TheirsContentBackground,
                        _ => null,
                    };

                    if (bg != null)
                        drawingContext.DrawRectangle(bg, null, rect);
                }
            }

            private readonly MergeConflictTextPresenter _presenter;
        }

        public static readonly StyledProperty<string> FileNameProperty =
            AvaloniaProperty.Register<MergeConflictTextPresenter, string>(nameof(FileName), string.Empty);

        public string FileName
        {
            get => GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        public static readonly StyledProperty<Models.ConflictPanelType> PanelTypeProperty =
            AvaloniaProperty.Register<MergeConflictTextPresenter, Models.ConflictPanelType>(nameof(PanelType));

        public Models.ConflictPanelType PanelType
        {
            get => GetValue(PanelTypeProperty);
            set => SetValue(PanelTypeProperty, value);
        }

        public static readonly StyledProperty<List<Models.ConflictLine>> LinesProperty =
            AvaloniaProperty.Register<MergeConflictTextPresenter, List<Models.ConflictLine>>(nameof(Lines));

        public List<Models.ConflictLine> Lines
        {
            get => GetValue(LinesProperty);
            set => SetValue(LinesProperty, value);
        }

        public static readonly StyledProperty<int> MaxLineNumberProperty =
            AvaloniaProperty.Register<MergeConflictTextPresenter, int>(nameof(MaxLineNumber));

        public int MaxLineNumber
        {
            get => GetValue(MaxLineNumberProperty);
            set => SetValue(MaxLineNumberProperty, value);
        }

        public static readonly StyledProperty<IBrush> OursContentBackgroundProperty =
            AvaloniaProperty.Register<MergeConflictTextPresenter, IBrush>(nameof(OursContentBackground), new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)));

        public IBrush OursContentBackground
        {
            get => GetValue(OursContentBackgroundProperty);
            set => SetValue(OursContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> TheirsContentBackgroundProperty =
            AvaloniaProperty.Register<MergeConflictTextPresenter, IBrush>(nameof(TheirsContentBackground), new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)));

        public IBrush TheirsContentBackground
        {
            get => GetValue(TheirsContentBackgroundProperty);
            set => SetValue(TheirsContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<Models.ConflictSelectedChunk> SelectedChunkProperty =
            AvaloniaProperty.Register<MergeConflictTextPresenter, Models.ConflictSelectedChunk>(nameof(SelectedChunk));

        public Models.ConflictSelectedChunk SelectedChunk
        {
            get => GetValue(SelectedChunkProperty);
            set => SetValue(SelectedChunkProperty, value);
        }

        public static readonly StyledProperty<ViewModels.TextLineRange> DisplayRangeProperty =
            AvaloniaProperty.Register<MergeConflictTextPresenter, ViewModels.TextLineRange>(nameof(DisplayRange));

        public ViewModels.TextLineRange DisplayRange
        {
            get => GetValue(DisplayRangeProperty);
            set => SetValue(DisplayRangeProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public MergeConflictTextPresenter() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            BorderThickness = new Thickness(0);

            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;
            Options.AllowScrollBelowDocument = false;

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.LeftMargins.Add(new LineNumberMargin(this));
            TextArea.LeftMargins.Add(new VerticalSeparatorMargin());
            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnTextViewScrollChanged;
                _scrollViewer.Bind(ScrollViewer.OffsetProperty, new Binding("ScrollOffset", BindingMode.OneWay));
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _textMate = Models.TextMateHelper.CreateForEditor(this);
            if (!string.IsNullOrEmpty(FileName))
                Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.PointerEntered += OnTextViewPointerChanged;
            TextArea.TextView.PointerMoved += OnTextViewPointerChanged;
            TextArea.TextView.PointerWheelChanged += OnTextViewPointerWheelChanged;
            TextArea.TextView.VisualLinesChanged += OnTextViewVisualLinesChanged;
            TextArea.TextView.LineTransformers.Add(new ConflictMarkerTransformer(this));

            OnTextViewVisualLinesChanged(null, null);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
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

            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == LinesProperty)
                UpdateContent();
            else if (change.Property == FileNameProperty)
                Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
                Models.TextMateHelper.SetThemeByApp(_textMate);
            else if (change.Property == SelectedChunkProperty)
                TextArea.TextView.InvalidateVisual();
            else if (change.Property == MaxLineNumberProperty)
                TextArea.LeftMargins[0].InvalidateMeasure();
        }

        private void UpdateContent()
        {
            var lines = Lines;
            if (lines == null || lines.Count == 0)
            {
                Text = string.Empty;
                return;
            }

            var builder = new StringBuilder();
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

                builder.Append('\n');
            }

            Text = builder.ToString();
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private void OnTextViewPointerChanged(object sender, PointerEventArgs e)
        {
            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (sender is not TextView view)
                return;

            UpdateSelectedChunkPosition(vm, e.GetPosition(view).Y + view.VerticalOffset);
        }

        private void OnTextViewPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (sender is not TextView view)
                return;

            var y = e.GetPosition(view).Y + view.VerticalOffset;
            Dispatcher.UIThread.Post(() => UpdateSelectedChunkPosition(vm, y));
        }

        private void OnTextViewVisualLinesChanged(object sender, EventArgs e)
        {
            if (Design.IsDesignMode)
                return;

            if (!TextArea.TextView.VisualLinesValid)
            {
                SetCurrentValue(DisplayRangeProperty, null);
                return;
            }

            var lines = Lines;
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

            SetCurrentValue(DisplayRangeProperty, new ViewModels.TextLineRange(start, start + count));
        }

        private void OnTextViewScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scrollViewer == null || DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (vm.ScrollOffset.NearlyEquals(_scrollViewer.Offset))
                return;

            if (IsPointerOver || e.OffsetDelta.SquaredLength > 1.0f)
            {
                vm.ScrollOffset = _scrollViewer.Offset;

                if (!TextArea.TextView.IsPointerOver)
                    vm.SelectedChunk = null;
            }
        }

        private void UpdateSelectedChunkPosition(ViewModels.MergeConflictEditor vm, double y)
        {
            var lines = Lines;
            var panel = PanelType;
            var view = TextArea.TextView;
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
                vm.SelectedChunk = null;
                return;
            }

            for (var i = 0; i < vm.ConflictRegions.Count; i++)
            {
                var r = vm.ConflictRegions[i];
                if (r.StartLineInOriginal <= lineIdx && r.EndLineInOriginal >= lineIdx)
                {
                    if (r.IsResolved && panel != Models.ConflictPanelType.Result)
                    {
                        vm.SelectedChunk = null;
                        return;
                    }

                    var startLine = r.StartLineInOriginal + 1;
                    var endLine = r.EndLineInOriginal + 1;
                    if (startLine > Document.LineCount || endLine > Document.LineCount)
                    {
                        vm.SelectedChunk = null;
                        return;
                    }

                    var vOffset = view.VerticalOffset;
                    var startVisualLine = view.GetVisualLine(startLine);
                    var endVisualLine = view.GetVisualLine(endLine);
                    var topY = startVisualLine?.GetTextLineVisualYPosition(startVisualLine.TextLines[0], VisualYPosition.LineTop) ?? vOffset;
                    var bottomY = endVisualLine?.GetTextLineVisualYPosition(endVisualLine.TextLines[^1], VisualYPosition.LineBottom) ?? (view.Bounds.Height + vOffset);
                    vm.SelectedChunk = new Models.ConflictSelectedChunk(topY - vOffset, bottomY - topY, i, panel, r.IsResolved);
                    return;
                }
            }

            vm.SelectedChunk = null;
        }

        private TextMate.Installation _textMate;
        private ScrollViewer _scrollViewer;
    }

    public class MergeConflictMinimap : Control
    {
        public static readonly StyledProperty<ViewModels.TextLineRange> DisplayRangeProperty =
            AvaloniaProperty.Register<MergeConflictMinimap, ViewModels.TextLineRange>(nameof(DisplayRange));

        public ViewModels.TextLineRange DisplayRange
        {
            get => GetValue(DisplayRangeProperty);
            set => SetValue(DisplayRangeProperty, value);
        }

        public static readonly StyledProperty<int> UnsolvedCountProperty =
            AvaloniaProperty.Register<MergeConflictMinimap, int>(nameof(UnsolvedCount));

        public int UnsolvedCount
        {
            get => GetValue(UnsolvedCountProperty);
            set => SetValue(UnsolvedCountProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var total = vm.OursLines.Count;
            var unitHeight = Bounds.Height / (total * 1.0);
            var conflicts = vm.ConflictRegions;
            var blockBGs = new SolidColorBrush[] { new SolidColorBrush(Colors.Red, 0.6), new SolidColorBrush(Colors.Green, 0.6) };
            foreach (var c in conflicts)
            {
                var topY = c.StartLineInOriginal * unitHeight;
                var bottomY = (c.EndLineInOriginal + 1) * unitHeight;
                var bg = blockBGs[c.IsResolved ? 1 : 0];
                context.DrawRectangle(bg, null, new Rect(0, topY, Bounds.Width, bottomY - topY));
            }

            var range = DisplayRange;
            if (range == null || range.End == 0)
                return;

            var startY = range.Start * unitHeight;
            var endY = range.End * unitHeight;
            var color = (Color)this.FindResource("SystemAccentColor");
            var brush = new SolidColorBrush(color, 0.2);
            var pen = new Pen(color.ToUInt32());
            var rect = new Rect(0, startY, Bounds.Width, endY - startY);

            context.DrawRectangle(brush, null, rect);
            context.DrawLine(pen, rect.TopLeft, rect.TopRight);
            context.DrawLine(pen, rect.BottomLeft, rect.BottomRight);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DisplayRangeProperty ||
                change.Property == UnsolvedCountProperty ||
                change.Property.Name.Equals(nameof(ActualThemeVariant), StringComparison.Ordinal))
                InvalidateVisual();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var total = vm.OursLines.Count;
            var range = DisplayRange;
            if (range == null || range.End == 0)
                return;

            var unitHeight = Bounds.Height / (total * 1.0);
            var startY = range.Start * unitHeight;
            var endY = range.End * unitHeight;
            var pressedY = e.GetPosition(this).Y;
            if (pressedY >= startY && pressedY <= endY)
                return;

            var line = Math.Max(1, Math.Min(total, (int)Math.Ceiling(pressedY / unitHeight)));
            var editor = this.FindAncestorOfType<MergeConflictEditor>();
            if (editor != null)
                editor.OursPresenter.ScrollToLine(line);
        }
    }

    public partial class MergeConflictEditor : ChromelessWindow
    {
        public MergeConflictEditor()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.MergeConflictEditor vm)
                vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (_forceClose || vm.UnsolvedCount == vm.ConflictRegions.Count)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                return;
            }

            e.Cancel = true;

            var confirm = new Confirm();
            confirm.SetData(App.Text("MergeConflictEditor.UnsavedChanges"), Models.ConfirmButtonType.OkCancel);

            var result = await confirm.ShowDialog<bool>(this);
            if (result)
            {
                _forceClose = true;
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            GC.Collect();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MergeConflictEditor.SelectedChunk))
                UpdatePopupVisibility();
        }

        private void OnGotoPrevConflict(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && DataContext is ViewModels.MergeConflictEditor vm && vm.UnsolvedCount > 0)
            {
                var view = OursPresenter.TextArea?.TextView;
                var lines = vm.OursLines;
                var minY = double.MaxValue;
                var minLineIdx = lines.Count;
                if (view is { VisualLinesValid: true })
                {
                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > lines.Count)
                            break;

                        var lineIndex = index - 1;
                        var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                        if (startY < minY)
                        {
                            minY = startY;
                            minLineIdx = lineIndex;
                        }
                    }

                    for (var i = vm.ConflictRegions.Count - 1; i >= 0; i--)
                    {
                        var r = vm.ConflictRegions[i];
                        if (r.StartLineInOriginal < minLineIdx && !r.IsResolved)
                        {
                            OursPresenter.ScrollToLine(r.StartLineInOriginal + 1);
                            break;
                        }
                    }
                }
            }

            e.Handled = true;
        }

        private void OnGotoNextConflict(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && DataContext is ViewModels.MergeConflictEditor vm && vm.UnsolvedCount > 0)
            {
                var view = OursPresenter.TextArea?.TextView;
                var lines = vm.OursLines;
                var maxY = 0.0;
                var maxLineIdx = 0;
                if (view is { VisualLinesValid: true })
                {
                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > lines.Count)
                            break;

                        var lineIndex = index - 1;
                        var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                        if (startY > maxY)
                        {
                            maxY = startY;
                            maxLineIdx = lineIndex;
                        }
                    }

                    for (var i = 0; i < vm.ConflictRegions.Count; i++)
                    {
                        var r = vm.ConflictRegions[i];
                        if (r.StartLineInOriginal > maxLineIdx && !r.IsResolved)
                        {
                            OursPresenter.ScrollToLine(r.StartLineInOriginal + 1);
                            break;
                        }
                    }
                }
            }

            e.Handled = true;
        }

        private async void OnSaveAndStage(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MergeConflictEditor vm)
            {
                var success = await vm.SaveAndStageAsync();
                if (success)
                {
                    _forceClose = true;
                    Close();
                }
            }
        }

        private void UpdatePopupVisibility()
        {
            // Hide all popups first
            MinePopup.IsVisible = false;
            TheirsPopup.IsVisible = false;
            ResultPopup.IsVisible = false;
            ResultUndoPopup.IsVisible = false;

            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var chunk = vm.SelectedChunk;
            if (chunk == null)
                return;

            // Get the presenter for bounds checking
            MergeConflictTextPresenter presenter = chunk.Panel switch
            {
                Models.ConflictPanelType.Ours => OursPresenter,
                Models.ConflictPanelType.Theirs => TheirsPresenter,
                Models.ConflictPanelType.Result => ResultPresenter,
                _ => null
            };

            // Show the appropriate popup based on panel type and resolved state
            Border popup = chunk.Panel switch
            {
                Models.ConflictPanelType.Ours => MinePopup,
                Models.ConflictPanelType.Theirs => TheirsPopup,
                Models.ConflictPanelType.Result => chunk.IsResolved ? ResultUndoPopup : ResultPopup,
                _ => null
            };

            if (popup != null && presenter != null)
            {
                // Position popup - clamp to visible area
                var top = chunk.Y + (chunk.Height >= 36 ? 8 : 2);

                // Clamp top to ensure popup is visible
                var popupHeight = popup.Bounds.Height > 0 ? popup.Bounds.Height : 32;
                var presenterHeight = presenter.Bounds.Height;
                top = Math.Max(4, Math.Min(top, presenterHeight - popupHeight - 4));

                popup.Margin = new Thickness(0, top, 24, 0);
                popup.IsVisible = true;
            }
        }

        private bool _forceClose = false;
    }
}
